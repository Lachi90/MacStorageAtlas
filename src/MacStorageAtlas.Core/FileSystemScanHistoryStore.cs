namespace MacStorageAtlas.Core;

public sealed class FileSystemScanHistoryStore : IScanHistoryStore
{
    private const string SnapshotExtension = ".msascan.gz";
    private const string PendingExtension = ".pending";
    private const string SpotlightExclusionMarker = ".metadata_never_index";

    private const UnixFileMode SnapshotDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private const UnixFileMode SnapshotFileMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private readonly string _rootDirectory;

    public FileSystemScanHistoryStore(string rootDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        _rootDirectory = rootDirectory;
        SweepPendingFiles();
    }

    public string Location => _rootDirectory;

    public Task<IReadOnlyList<ScanHistoryEntry>> ListAsync(
        CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<ScanHistoryEntry>>(
            () => ListEntries(cancellationToken),
            cancellationToken);

    public Task<long> GetTotalSizeBytesAsync(
        CancellationToken cancellationToken = default) =>
        Task.Run(
            () => EnumerateSnapshotFiles().Sum(file => SafeLength(file)),
            cancellationToken);

    public async Task<ScanHistoryCaptureResult> CaptureAsync(
        ScanSnapshotRequest request,
        ScanHistoryLimits limits,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(limits);

        if (!ScanSnapshotIdentity.IsValid(request.Metadata.SnapshotId))
        {
            return ScanHistoryCaptureResult.Failed(
                $"'{request.Metadata.SnapshotId}' is not a usable snapshot identity.");
        }

        var pendingPath = Path.Combine(
            _rootDirectory,
            request.Metadata.SnapshotId + SnapshotExtension + PendingExtension);

        try
        {
            EnsureStore();
            SweepPendingFiles();

            long pendingSize;

            var pending = new FileStream(
                pendingPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                useAsync: true);

            await using (pending.ConfigureAwait(false))
            {
                await ScanSnapshotJsonWriter
                    .WriteAsync(request, pending, cancellationToken)
                    .ConfigureAwait(false);

                pendingSize = pending.Length;
            }

            cancellationToken.ThrowIfCancellationRequested();

            var existing = ListEntries(cancellationToken)
                .Where(entry => entry.IsReadable)
                .Select(entry => entry.Descriptor!)
                .ToArray();

            var decision = ScanHistoryRetentionPolicy.DecideForCapture(
                existing,
                request.Metadata.RootPath,
                pendingSize,
                limits);

            if (!decision.IsAccepted)
            {
                Delete(pendingPath);
                return ScanHistoryCaptureResult.Refused(decision.RefusalMessage!);
            }

            foreach (var prunedSnapshot in decision.SnapshotsToPrune)
            {
                Delete(SnapshotPath(prunedSnapshot.SnapshotId));
            }

            var publishedPath = SnapshotPath(request.Metadata.SnapshotId);
            File.Move(pendingPath, publishedPath, overwrite: true);
            ApplyFileMode(publishedPath);

            return ScanHistoryCaptureResult.Captured(
                new ScanSnapshotDescriptor(request.Metadata, pendingSize),
                decision.SnapshotsToPrune);
        }
        catch (OperationCanceledException)
        {
            Delete(pendingPath);
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            Delete(pendingPath);
            return ScanHistoryCaptureResult.Failed(exception.Message);
        }
    }

    public Task<ScanSnapshotReadResult<ScanSnapshotDocument>> ReadAsync(
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotId);

        return Task.Run(
            () =>
            {
                var path = SnapshotPath(snapshotId);

                if (!File.Exists(path))
                {
                    return ScanSnapshotReadResult<ScanSnapshotDocument>.Unreadable(
                        $"No snapshot named '{snapshotId}' is stored.");
                }

                try
                {
                    using var stream = File.OpenRead(path);
                    return ScanSnapshotJsonReader.Read(stream);
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                    return ScanSnapshotReadResult<ScanSnapshotDocument>.Unreadable(
                        exception.Message);
                }
            },
            cancellationToken);
    }

    public Task<bool> DeleteAsync(
        string snapshotId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotId);

        return Task.Run(() => Delete(SnapshotPath(snapshotId)), cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default) =>
        Task.Run(
            () =>
            {
                foreach (var file in EnumerateSnapshotFiles())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Delete(file);
                }

                SweepPendingFiles();
            },
            cancellationToken);

    public Task<IReadOnlyList<ScanSnapshotDescriptor>> ApplyLimitsAsync(
        ScanHistoryLimits limits,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(limits);

        return Task.Run<IReadOnlyList<ScanSnapshotDescriptor>>(
            () =>
            {
                var existing = ListEntries(cancellationToken)
                    .Where(entry => entry.IsReadable)
                    .Select(entry => entry.Descriptor!)
                    .ToArray();

                var pruned = ScanHistoryRetentionPolicy.DecideForLimitChange(
                    existing,
                    limits);

                foreach (var snapshot in pruned)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    Delete(SnapshotPath(snapshot.SnapshotId));
                }

                return pruned;
            },
            cancellationToken);
    }

    private List<ScanHistoryEntry> ListEntries(CancellationToken cancellationToken)
    {
        var entries = new List<ScanHistoryEntry>();

        foreach (var file in EnumerateSnapshotFiles())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var snapshotId = SnapshotIdFrom(file);
            var length = SafeLength(file);

            try
            {
                using var stream = File.OpenRead(file);
                var result = ScanSnapshotJsonReader.ReadDescriptor(stream, length);

                entries.Add(result.IsOk
                    ? ScanHistoryEntry.Readable(snapshotId, result.Payload!)
                    : ScanHistoryEntry.Unreadable(
                        snapshotId,
                        length,
                        result.Message ?? "The snapshot could not be read."));
            }
            catch (Exception exception) when (
                exception is FileNotFoundException or DirectoryNotFoundException)
            {
                continue;
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException)
            {
                entries.Add(ScanHistoryEntry.Unreadable(
                    snapshotId,
                    length,
                    exception.Message));
            }
        }

        return entries;
    }

    private IEnumerable<string> EnumerateSnapshotFiles()
    {
        if (!Directory.Exists(_rootDirectory))
        {
            return [];
        }

        try
        {
            return Directory
                .EnumerateFiles(_rootDirectory, "*" + SnapshotExtension)
                .Where(file => !file.EndsWith(PendingExtension, StringComparison.Ordinal))
                .OrderBy(file => file, StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private void SweepPendingFiles()
    {
        if (!Directory.Exists(_rootDirectory))
        {
            return;
        }

        try
        {
            foreach (var pending in Directory.EnumerateFiles(
                         _rootDirectory,
                         "*" + PendingExtension))
            {
                Delete(pending);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private void EnsureStore()
    {
        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(_rootDirectory);
        }
        else
        {
            Directory.CreateDirectory(_rootDirectory, SnapshotDirectoryMode);
        }

        var marker = Path.Combine(_rootDirectory, SpotlightExclusionMarker);

        if (!File.Exists(marker))
        {
            File.WriteAllBytes(marker, []);
            ApplyFileMode(marker);
        }
    }

    private static void ApplyFileMode(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            File.SetUnixFileMode(path, SnapshotFileMode);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private string SnapshotPath(string snapshotId) =>
        Path.Combine(_rootDirectory, snapshotId + SnapshotExtension);

    private static string SnapshotIdFrom(string path)
    {
        var name = Path.GetFileName(path);
        return name[..^SnapshotExtension.Length];
    }

    private static long SafeLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static bool Delete(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
