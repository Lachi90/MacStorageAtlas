using System.Buffers;
using System.Security.Cryptography;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Insights;

public sealed class DuplicateAnalyzer(
    IDuplicateCandidateMetadataReader metadataReader,
    IDuplicateContentReader contentReader)
{
    private const int ProgressReportEveryCandidates = 128;

    public async Task<DuplicateAnalysisResult> AnalyzeAsync(
        DiskItem root,
        DuplicateAnalysisOptions? options = null,
        IProgress<DuplicateAnalysisProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);

        options ??= DuplicateAnalysisOptions.Default;
        ValidateOptions(options);

        progress?.Report(DuplicateAnalysisProgress.Start);

        var skipped = new List<DuplicateSkippedCandidate>();
        var candidates = await CollectCandidatesAsync(
                root,
                options,
                skipped,
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        var sameLengthBuckets = candidates
            .GroupBy(candidate => candidate.Metadata.LogicalLengthBytes)
            .Where(group => group.Count() > 1)
            .ToArray();

        if (sameLengthBuckets.Length == 0)
        {
            var empty = new DuplicateAnalysisResult([], skipped);
            progress?.Report(new DuplicateAnalysisProgress(
                DuplicateAnalysisStage.Completed,
                CurrentPath: null,
                candidates.Count,
                candidates.Count,
                BytesRead: 0,
                empty.Summary.GroupCount));
            return empty;
        }

        var sampledCandidates = await SampleCandidatesAsync(
                sameLengthBuckets,
                options,
                skipped,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        var hashedCandidates = await HashCandidatesAsync(
                sampledCandidates,
                options,
                skipped,
                progress,
                cancellationToken)
            .ConfigureAwait(false);
        var groups = await ConfirmGroupsAsync(
                hashedCandidates,
                options,
                skipped,
                progress,
                cancellationToken)
            .ConfigureAwait(false);

        var result = new DuplicateAnalysisResult(groups, skipped);
        progress?.Report(new DuplicateAnalysisProgress(
            DuplicateAnalysisStage.Completed,
            CurrentPath: null,
            candidates.Count,
            candidates.Count,
            BytesRead: 0,
            result.Summary.GroupCount));
        return result;
    }

    private async Task<List<Candidate>> CollectCandidatesAsync(
        DiskItem root,
        DuplicateAnalysisOptions options,
        List<DuplicateSkippedCandidate> skipped,
        IProgress<DuplicateAnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        var candidates = new List<Candidate>();
        var pending = new Stack<DiskItem>();
        pending.Push(root);
        var examined = 0L;

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = pending.Pop();
            if (item.IsDirectory)
            {
                for (var index = item.Children.Count - 1; index >= 0; index--)
                {
                    pending.Push(item.Children[index]);
                }

                continue;
            }

            examined++;
            if (ShouldReportProgress(examined))
            {
                progress?.Report(new DuplicateAnalysisProgress(
                    DuplicateAnalysisStage.CollectingCandidates,
                    item.Path,
                    examined,
                    CandidateCount: 0,
                    BytesRead: 0,
                    GroupsFound: 0));
            }

            var metadata = await TryReadMetadataAsync(item, skipped, cancellationToken)
                .ConfigureAwait(false);
            if (metadata is null)
            {
                continue;
            }

            if (metadata.ContentAvailability == DuplicateContentAvailability.NotLocal)
            {
                skipped.Add(Skipped(
                    item,
                    DuplicateSkipReason.ContentsNotLocal,
                    "The file contents are not local."));
                continue;
            }

            if (metadata.LogicalLengthBytes == 0 && !options.IncludeZeroLengthFiles)
            {
                continue;
            }

            candidates.Add(new Candidate(item, metadata));
        }

        return candidates;
    }

    private async Task<IReadOnlyList<Candidate>> SampleCandidatesAsync(
        IReadOnlyList<IGrouping<long, Candidate>> buckets,
        DuplicateAnalysisOptions options,
        List<DuplicateSkippedCandidate> skipped,
        IProgress<DuplicateAnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        var result = new List<Candidate>();
        var allCandidates = buckets.Sum(bucket => bucket.Count());
        var examined = 0L;
        var bytesRead = 0L;

        foreach (var bucket in buckets)
        {
            var groups = new Dictionary<SampleKey, List<Candidate>>();
            foreach (var candidate in bucket)
            {
                cancellationToken.ThrowIfCancellationRequested();
                examined++;

                if (!await IsUnchangedAsync(candidate, skipped, cancellationToken)
                        .ConfigureAwait(false))
                {
                    continue;
                }

                var sample = await TryReadSampleAsync(
                        candidate,
                        options,
                        skipped,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (sample is null)
                {
                    continue;
                }

                bytesRead += sample.BytesRead;
                if (ShouldReportProgress(examined, allCandidates))
                {
                    progress?.Report(new DuplicateAnalysisProgress(
                        DuplicateAnalysisStage.SamplingCandidates,
                        candidate.Item.Path,
                        examined,
                        allCandidates,
                        bytesRead,
                        GroupsFound: 0));
                }

                if (!groups.TryGetValue(sample.Key, out var sampleGroup))
                {
                    sampleGroup = [];
                    groups.Add(sample.Key, sampleGroup);
                }

                sampleGroup.Add(candidate);
            }

            result.AddRange(groups.Values.Where(group => group.Count > 1).SelectMany(group => group));
        }

        return result;
    }

    private async Task<IReadOnlyList<HashedCandidate>> HashCandidatesAsync(
        IReadOnlyList<Candidate> candidates,
        DuplicateAnalysisOptions options,
        List<DuplicateSkippedCandidate> skipped,
        IProgress<DuplicateAnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        var hashed = new List<HashedCandidate>();
        var examined = 0L;
        var bytesRead = 0L;

        foreach (var candidate in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            examined++;

            if (!await IsUnchangedAsync(candidate, skipped, cancellationToken)
                    .ConfigureAwait(false))
            {
                continue;
            }

            var hash = await TryHashAsync(candidate, options, skipped, cancellationToken)
                .ConfigureAwait(false);
            if (hash is null)
            {
                continue;
            }

            bytesRead += candidate.Metadata.LogicalLengthBytes;
            if (ShouldReportProgress(examined, candidates.Count))
            {
                progress?.Report(new DuplicateAnalysisProgress(
                    DuplicateAnalysisStage.HashingCandidates,
                    candidate.Item.Path,
                    examined,
                    candidates.Count,
                    bytesRead,
                    GroupsFound: 0));
            }

            hashed.Add(new HashedCandidate(candidate, hash));
        }

        return hashed
            .GroupBy(candidate => candidate.Hash, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group)
            .ToArray();
    }

    private async Task<IReadOnlyList<DuplicateGroup>> ConfirmGroupsAsync(
        IReadOnlyList<HashedCandidate> hashedCandidates,
        DuplicateAnalysisOptions options,
        List<DuplicateSkippedCandidate> skipped,
        IProgress<DuplicateAnalysisProgress>? progress,
        CancellationToken cancellationToken)
    {
        var groups = new List<DuplicateGroup>();
        var examined = 0L;
        var hashGroups = hashedCandidates
            .GroupBy(candidate => candidate.Hash, StringComparer.Ordinal)
            .ToArray();

        foreach (var hashGroup in hashGroups)
        {
            var clusters = new List<List<Candidate>>();
            foreach (var hashed in hashGroup)
            {
                cancellationToken.ThrowIfCancellationRequested();
                examined++;

                var candidate = hashed.Candidate;
                if (!await IsUnchangedAsync(candidate, skipped, cancellationToken)
                        .ConfigureAwait(false))
                {
                    continue;
                }

                var matchingCluster = await FindMatchingClusterAsync(
                        clusters,
                        candidate,
                        options,
                        skipped,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (matchingCluster is null)
                {
                    clusters.Add([candidate]);
                }
                else
                {
                    matchingCluster.Add(candidate);
                }

                if (ShouldReportProgress(examined, hashedCandidates.Count))
                {
                    progress?.Report(new DuplicateAnalysisProgress(
                        DuplicateAnalysisStage.ConfirmingEquality,
                        candidate.Item.Path,
                        examined,
                        hashedCandidates.Count,
                        BytesRead: 0,
                        groups.Count));
                }
            }

            foreach (var cluster in clusters.Where(cluster => cluster.Count > 1))
            {
                groups.Add(CreateGroup(cluster));
            }
        }

        return groups
            .OrderByDescending(group => group.ReclaimableSizeBytes)
            .ThenBy(group => group.Entries[0].Item.Path, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<List<Candidate>?> FindMatchingClusterAsync(
        List<List<Candidate>> clusters,
        Candidate candidate,
        DuplicateAnalysisOptions options,
        List<DuplicateSkippedCandidate> skipped,
        CancellationToken cancellationToken)
    {
        foreach (var cluster in clusters)
        {
            if (await ContentsEqualAsync(
                    cluster[0],
                    candidate,
                    options,
                    skipped,
                    cancellationToken).ConfigureAwait(false))
            {
                return cluster;
            }
        }

        return null;
    }

    private DuplicateGroup CreateGroup(IReadOnlyList<Candidate> candidates)
    {
        var ordered = candidates
            .OrderBy(candidate => PathDepth(candidate.Item.Path))
            .ThenBy(candidate => candidate.Item.Path, StringComparer.Ordinal)
            .ToArray();
        var retainedIdentity = ordered[0].Metadata.Identity;
        var entries = new List<DuplicateGroupEntry>(ordered.Length)
        {
            new(
                ordered[0].Item,
                ordered[0].Metadata.LogicalLengthBytes,
                DuplicateGroupEntryKind.RetainedCopy,
                retainedIdentity)
        };

        for (var index = 1; index < ordered.Length; index++)
        {
            var candidate = ordered[index];
            var isLinkedToRetained = retainedIdentity is not null
                && candidate.Metadata.Identity == retainedIdentity;
            entries.Add(new DuplicateGroupEntry(
                candidate.Item,
                candidate.Metadata.LogicalLengthBytes,
                isLinkedToRetained
                    ? DuplicateGroupEntryKind.LinkedPath
                    : DuplicateGroupEntryKind.ReclaimableCopy,
                candidate.Metadata.Identity));
        }

        return new DuplicateGroup(ordered[0].Metadata.LogicalLengthBytes, entries);
    }

    private async Task<DuplicateCandidateMetadata?> TryReadMetadataAsync(
        DiskItem item,
        List<DuplicateSkippedCandidate> skipped,
        CancellationToken cancellationToken)
    {
        try
        {
            return await metadataReader.ReadAsync(item.Path, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            skipped.Add(Skipped(item, SkipReason(exception), Message(exception)));
            return null;
        }
    }

    private async Task<bool> IsUnchangedAsync(
        Candidate candidate,
        List<DuplicateSkippedCandidate> skipped,
        CancellationToken cancellationToken)
    {
        var current = await TryReadMetadataAsync(candidate.Item, skipped, cancellationToken)
            .ConfigureAwait(false);
        if (current is null)
        {
            return false;
        }

        if (current.ContentAvailability == DuplicateContentAvailability.NotLocal)
        {
            skipped.Add(Skipped(
                candidate.Item,
                DuplicateSkipReason.ContentsNotLocal,
                "The file contents are not local."));
            return false;
        }

        if (current.LogicalLengthBytes != candidate.Metadata.LogicalLengthBytes
            || (candidate.Metadata.Identity is not null
                && current.Identity is not null
                && current.Identity != candidate.Metadata.Identity))
        {
            skipped.Add(Skipped(
                candidate.Item,
                DuplicateSkipReason.Changed,
                "The file changed during duplicate analysis."));
            return false;
        }

        return true;
    }

    private async Task<SampleResult?> TryReadSampleAsync(
        Candidate candidate,
        DuplicateAnalysisOptions options,
        List<DuplicateSkippedCandidate> skipped,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await contentReader
                .OpenReadAsync(candidate.Item.Path, cancellationToken)
                .ConfigureAwait(false);
            if (!stream.CanSeek)
            {
                skipped.Add(Skipped(
                    candidate.Item,
                    DuplicateSkipReason.ReadFailed,
                    "The file cannot be sampled because it is not seekable."));
                return null;
            }

            var length = candidate.Metadata.LogicalLengthBytes;
            var sampleSize = Math.Min(options.SampleSizeBytes, checked((int)Math.Min(length, int.MaxValue)));
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = ArrayPool<byte>.Shared.Rent(options.BufferSizeBytes);
            try
            {
                var prefixBytes = await HashRangeAsync(
                        stream,
                        0,
                        sampleSize,
                        buffer,
                        options.BufferSizeBytes,
                        hash,
                        cancellationToken)
                    .ConfigureAwait(false);
                var suffixStart = Math.Max(0, length - sampleSize);
                var suffixBytes = await HashRangeAsync(
                        stream,
                        suffixStart,
                        sampleSize,
                        buffer,
                        options.BufferSizeBytes,
                        hash,
                        cancellationToken)
                    .ConfigureAwait(false);

                return new SampleResult(
                    new SampleKey(Convert.ToHexString(hash.GetHashAndReset())),
                    prefixBytes + suffixBytes);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            skipped.Add(Skipped(candidate.Item, SkipReason(exception), Message(exception)));
            return null;
        }
    }

    private async Task<string?> TryHashAsync(
        Candidate candidate,
        DuplicateAnalysisOptions options,
        List<DuplicateSkippedCandidate> skipped,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = await contentReader
                .OpenReadAsync(candidate.Item.Path, cancellationToken)
                .ConfigureAwait(false);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = ArrayPool<byte>.Shared.Rent(options.BufferSizeBytes);
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var read = await stream
                        .ReadAsync(buffer.AsMemory(0, options.BufferSizeBytes), cancellationToken)
                        .ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }

                    hash.AppendData(buffer.AsSpan(0, read));
                }

                return Convert.ToHexString(hash.GetHashAndReset());
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            skipped.Add(Skipped(candidate.Item, SkipReason(exception), Message(exception)));
            return null;
        }
    }

    private async Task<bool> ContentsEqualAsync(
        Candidate left,
        Candidate right,
        DuplicateAnalysisOptions options,
        List<DuplicateSkippedCandidate> skipped,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var leftStream = await contentReader
                .OpenReadAsync(left.Item.Path, cancellationToken)
                .ConfigureAwait(false);
            await using var rightStream = await contentReader
                .OpenReadAsync(right.Item.Path, cancellationToken)
                .ConfigureAwait(false);
            var leftBuffer = ArrayPool<byte>.Shared.Rent(options.BufferSizeBytes);
            var rightBuffer = ArrayPool<byte>.Shared.Rent(options.BufferSizeBytes);
            try
            {
                while (true)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var leftRead = await leftStream
                        .ReadAsync(leftBuffer.AsMemory(0, options.BufferSizeBytes), cancellationToken)
                        .ConfigureAwait(false);
                    var rightRead = await rightStream
                        .ReadAsync(rightBuffer.AsMemory(0, options.BufferSizeBytes), cancellationToken)
                        .ConfigureAwait(false);

                    if (leftRead != rightRead)
                    {
                        return false;
                    }

                    if (leftRead == 0)
                    {
                        return true;
                    }

                    if (!leftBuffer.AsSpan(0, leftRead).SequenceEqual(rightBuffer.AsSpan(0, rightRead)))
                    {
                        return false;
                    }
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(leftBuffer);
                ArrayPool<byte>.Shared.Return(rightBuffer);
            }
        }
        catch (Exception exception) when (IsRecoverable(exception))
        {
            skipped.Add(Skipped(right.Item, SkipReason(exception), Message(exception)));
            return false;
        }
    }

    private static async Task<long> HashRangeAsync(
        Stream stream,
        long offset,
        int count,
        byte[] buffer,
        int bufferSize,
        IncrementalHash hash,
        CancellationToken cancellationToken)
    {
        stream.Seek(offset, SeekOrigin.Begin);
        var total = 0;
        while (total < count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await stream
                .ReadAsync(buffer.AsMemory(0, Math.Min(bufferSize, count - total)), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            hash.AppendData(buffer.AsSpan(0, read));
            total += read;
        }

        return total;
    }

    private static void ValidateOptions(DuplicateAnalysisOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.SampleSizeBytes, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.BufferSizeBytes, 0);
    }

    private static bool ShouldReportProgress(long examined, long total = 0) =>
        examined == 1
        || examined % ProgressReportEveryCandidates == 0
        || (total > 0 && examined == total);

    private static DuplicateSkippedCandidate Skipped(
        DiskItem item,
        DuplicateSkipReason reason,
        string message) =>
        new(item, reason, message);

    private static DuplicateSkipReason SkipReason(Exception exception) =>
        exception is FileNotFoundException or DirectoryNotFoundException
            ? DuplicateSkipReason.Missing
            : DuplicateSkipReason.ReadFailed;

    private static string Message(Exception exception) =>
        exception is FileNotFoundException or DirectoryNotFoundException
            ? "The file no longer exists."
            : "The file could not be read.";

    private static bool IsRecoverable(Exception exception) =>
        exception is IOException or UnauthorizedAccessException
            or FileNotFoundException or DirectoryNotFoundException;

    private static int PathDepth(string path) =>
        path.Count(character => character == Path.DirectorySeparatorChar);

    private sealed record Candidate(DiskItem Item, DuplicateCandidateMetadata Metadata);

    private sealed record SampleKey(string Hash);

    private sealed record SampleResult(SampleKey Key, long BytesRead);

    private sealed record HashedCandidate(Candidate Candidate, string Hash);
}
