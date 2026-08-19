using MacStorageAtlas.Core.Insights;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Tests.Insights;

public class DuplicateAnalysisModelTests
{
    [Test]
    public void CandidateMetadataRejectsNegativeLogicalLength()
    {
        Assert.That(
            () => new DuplicateCandidateMetadata(
                logicalLengthBytes: -1,
                DuplicateContentAvailability.Local),
            Throws.InstanceOf<ArgumentOutOfRangeException>());
    }

    [Test]
    public void GroupRequiresAtLeastTwoEntries()
    {
        var retained = Entry(
            "a.bin",
            "/scan/a.bin",
            DuplicateGroupEntryKind.RetainedCopy);

        Assert.That(
            () => new DuplicateGroup(10, [retained]),
            Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void GroupRequiresRetainedCopy()
    {
        var first = Entry(
            "a.bin",
            "/scan/a.bin",
            DuplicateGroupEntryKind.ReclaimableCopy);
        var second = Entry(
            "b.bin",
            "/scan/b.bin",
            DuplicateGroupEntryKind.ReclaimableCopy);

        Assert.That(
            () => new DuplicateGroup(10, [first, second]),
            Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void GroupRequiresMatchingLogicalSizes()
    {
        var retained = Entry(
            "a.bin",
            "/scan/a.bin",
            DuplicateGroupEntryKind.RetainedCopy,
            logicalSizeBytes: 10);
        var reclaimable = Entry(
            "b.bin",
            "/scan/b.bin",
            DuplicateGroupEntryKind.ReclaimableCopy,
            logicalSizeBytes: 11);

        Assert.That(
            () => new DuplicateGroup(10, [retained, reclaimable]),
            Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void GroupTotalsCountOnlyReclaimableCopies()
    {
        var retained = Entry(
            "a.bin",
            "/scan/a.bin",
            DuplicateGroupEntryKind.RetainedCopy);
        var reclaimable = Entry(
            "b.bin",
            "/scan/b.bin",
            DuplicateGroupEntryKind.ReclaimableCopy);
        var linked = Entry(
            "c.bin",
            "/scan/c.bin",
            DuplicateGroupEntryKind.LinkedPath);

        var group = new DuplicateGroup(10, [retained, reclaimable, linked]);

        Assert.Multiple(() =>
        {
            Assert.That(group.ReclaimableCopyCount, Is.EqualTo(1));
            Assert.That(group.ReclaimableSizeBytes, Is.EqualTo(10));
            Assert.That(linked.IsLinkedPath, Is.True);
            Assert.That(linked.ReclaimableSizeBytes, Is.Zero);
        });
    }

    [Test]
    public void ResultSummarizesGroupsAndSkippedCandidates()
    {
        var group = new DuplicateGroup(
            10,
            [
                Entry("a.bin", "/scan/a.bin", DuplicateGroupEntryKind.RetainedCopy),
                Entry("b.bin", "/scan/b.bin", DuplicateGroupEntryKind.ReclaimableCopy),
                Entry("c.bin", "/scan/c.bin", DuplicateGroupEntryKind.ReclaimableCopy)
            ]);
        var skipped = new DuplicateSkippedCandidate(
            File("cloud.bin", "/scan/cloud.bin"),
            DuplicateSkipReason.ContentsNotLocal,
            "The file contents are not local.");

        var result = new DuplicateAnalysisResult([group], [skipped]);

        Assert.Multiple(() =>
        {
            Assert.That(result.Summary.GroupCount, Is.EqualTo(1));
            Assert.That(result.Summary.ReclaimableCopyCount, Is.EqualTo(2));
            Assert.That(result.Summary.ReclaimableSizeBytes, Is.EqualTo(20));
            Assert.That(result.Summary.SkippedCandidateCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void EmptyResultUsesEmptySummary()
    {
        Assert.That(
            DuplicateAnalysisResult.Empty.Summary,
            Is.SameAs(DuplicateAnalysisSummary.Empty));
    }

    [Test]
    public void SkippedCandidateRequiresMessage()
    {
        Assert.That(
            () => new DuplicateSkippedCandidate(
                File("a.bin", "/scan/a.bin"),
                DuplicateSkipReason.ReadFailed,
                ""),
            Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void ProgressStartBeginsAtCandidateCollection()
    {
        var progress = DuplicateAnalysisProgress.Start;

        Assert.Multiple(() =>
        {
            Assert.That(progress.Stage, Is.EqualTo(DuplicateAnalysisStage.CollectingCandidates));
            Assert.That(progress.CandidatesExamined, Is.Zero);
            Assert.That(progress.CandidateCount, Is.Zero);
            Assert.That(progress.BytesRead, Is.Zero);
            Assert.That(progress.GroupsFound, Is.Zero);
        });
    }

    [Test]
    public async Task MetadataReaderInterfacePropagatesCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        var reader = new RecordingMetadataReader();

        await reader.ReadAsync("/scan/a.bin", cancellation.Token);

        Assert.That(reader.CancellationToken, Is.EqualTo(cancellation.Token));
    }

    [Test]
    public async Task ContentReaderInterfacePropagatesCancellationToken()
    {
        using var cancellation = new CancellationTokenSource();
        await using var stream = new MemoryStream();
        var reader = new RecordingContentReader(stream);

        var opened = await reader.OpenReadAsync("/scan/a.bin", cancellation.Token);

        Assert.Multiple(() =>
        {
            Assert.That(opened, Is.SameAs(stream));
            Assert.That(reader.CancellationToken, Is.EqualTo(cancellation.Token));
        });
    }

    private static DuplicateGroupEntry Entry(
        string name,
        string path,
        DuplicateGroupEntryKind kind,
        long logicalSizeBytes = 10,
        FileIdentity? identity = null) =>
        new(File(name, path), logicalSizeBytes, kind, identity);

    private static DiskItem File(string name, string path) =>
        new(name, path, isDirectory: false) { SizeBytes = 10 };

    private sealed class RecordingMetadataReader : IDuplicateCandidateMetadataReader
    {
        public CancellationToken CancellationToken { get; private set; }

        public ValueTask<DuplicateCandidateMetadata> ReadAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(
                new DuplicateCandidateMetadata(
                    logicalLengthBytes: 10,
                    DuplicateContentAvailability.Local));
        }
    }

    private sealed class RecordingContentReader(Stream stream) : IDuplicateContentReader
    {
        public CancellationToken CancellationToken { get; private set; }

        public ValueTask<Stream> OpenReadAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            CancellationToken = cancellationToken;
            return ValueTask.FromResult(stream);
        }
    }
}
