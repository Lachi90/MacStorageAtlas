using MacStorageAtlas.Core.Insights;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Tests.Insights;

public class DuplicateAnalyzerTests
{
    [Test]
    public async Task AnalyzeAsyncDoesNotOpenUniqueLengthFiles()
    {
        var root = Directory("root", "/scan");
        root.AddChild(File("a.bin", "/scan/a.bin", sizeBytes: 4096));
        root.AddChild(File("b.bin", "/scan/b.bin", sizeBytes: 4096));
        var metadata = MetadataReader.WithLengths(
            ("/scan/a.bin", 10),
            ("/scan/b.bin", 11));
        var content = new ThrowingContentReader();
        var analyzer = new DuplicateAnalyzer(metadata, content);

        var result = await analyzer.AnalyzeAsync(root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Groups, Is.Empty);
            Assert.That(content.OpenCount, Is.Zero);
        });
    }

    [Test]
    public async Task AnalyzeAsyncExcludesZeroLengthFilesByDefaultWithoutOpeningContent()
    {
        var root = Directory("root", "/scan");
        root.AddChild(File("a.bin", "/scan/a.bin", sizeBytes: 0));
        root.AddChild(File("b.bin", "/scan/b.bin", sizeBytes: 0));
        var metadata = MetadataReader.WithLengths(
            ("/scan/a.bin", 0),
            ("/scan/b.bin", 0));
        var content = new ThrowingContentReader();
        var analyzer = new DuplicateAnalyzer(metadata, content);

        var result = await analyzer.AnalyzeAsync(root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Groups, Is.Empty);
            Assert.That(content.OpenCount, Is.Zero);
        });
    }

    [Test]
    public async Task AnalyzeAsyncUsesCurrentLogicalLengthForAllocatedScanResult()
    {
        var root = Directory("root", "/scan");
        root.AddChild(File("a.bin", "/scan/a.bin", sizeBytes: 4096));
        root.AddChild(File("b.bin", "/scan/b.bin", sizeBytes: 8192));
        var metadata = MetadataReader.WithLengths(
            ("/scan/a.bin", 3),
            ("/scan/b.bin", 3));
        var content = ContentReader.WithFiles(
            ("/scan/a.bin", [1, 2, 3]),
            ("/scan/b.bin", [1, 2, 3]));
        var analyzer = new DuplicateAnalyzer(metadata, content);

        var result = await analyzer.AnalyzeAsync(root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Groups, Has.Count.EqualTo(1));
            Assert.That(result.Groups[0].LogicalSizeBytes, Is.EqualTo(3));
            Assert.That(result.Groups[0].ReclaimableSizeBytes, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task AnalyzeAsyncReportsEqualFilesTogether()
    {
        var root = RootWithFiles("a.bin", "b.bin");
        var metadata = MetadataReader.WithLengths(
            ("/scan/a.bin", 4),
            ("/scan/b.bin", 4));
        var content = ContentReader.WithFiles(
            ("/scan/a.bin", [1, 2, 3, 4]),
            ("/scan/b.bin", [1, 2, 3, 4]));
        var analyzer = new DuplicateAnalyzer(metadata, content);

        var result = await analyzer.AnalyzeAsync(root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Groups, Has.Count.EqualTo(1));
            Assert.That(
                result.Groups[0].Entries.Select(entry => entry.Item.Path),
                Is.EqualTo(new[] { "/scan/a.bin", "/scan/b.bin" }));
            Assert.That(result.Summary.ReclaimableCopyCount, Is.EqualTo(1));
            Assert.That(result.Summary.ReclaimableSizeBytes, Is.EqualTo(4));
        });
    }

    [TestCase(new byte[] { 9, 2, 3, 4 })]
    [TestCase(new byte[] { 1, 2, 3, 9 })]
    [TestCase(new byte[] { 1, 2, 9, 4 })]
    public async Task AnalyzeAsyncDoesNotReportSameSizeDifferentContent(byte[] otherContent)
    {
        var root = RootWithFiles("a.bin", "b.bin");
        var metadata = MetadataReader.WithLengths(
            ("/scan/a.bin", 4),
            ("/scan/b.bin", 4));
        var content = ContentReader.WithFiles(
            ("/scan/a.bin", [1, 2, 3, 4]),
            ("/scan/b.bin", otherContent));
        var analyzer = new DuplicateAnalyzer(metadata, content);
        var options = DuplicateAnalysisOptions.Default with { SampleSizeBytes = 1 };

        var result = await analyzer.AnalyzeAsync(root, options);

        Assert.That(result.Groups, Is.Empty);
    }

    [Test]
    public async Task AnalyzeAsyncRepresentsHardlinksAsLinkedPaths()
    {
        var root = RootWithFiles("a.bin", "b.bin", "c.bin");
        var identity = new FileIdentity(1, 2);
        var metadata = new MetadataReader(new Dictionary<string, DuplicateCandidateMetadata>
        {
            ["/scan/a.bin"] = new(4, DuplicateContentAvailability.Local, identity, linkCount: 2),
            ["/scan/b.bin"] = new(4, DuplicateContentAvailability.Local, identity, linkCount: 2),
            ["/scan/c.bin"] = new(4, DuplicateContentAvailability.Local, new FileIdentity(1, 3))
        });
        var content = ContentReader.WithFiles(
            ("/scan/a.bin", [1, 2, 3, 4]),
            ("/scan/b.bin", [1, 2, 3, 4]),
            ("/scan/c.bin", [1, 2, 3, 4]));
        var analyzer = new DuplicateAnalyzer(metadata, content);

        var result = await analyzer.AnalyzeAsync(root);

        var group = result.Groups.Single();
        Assert.Multiple(() =>
        {
            Assert.That(
                group.Entries.Select(entry => entry.Kind),
                Is.EqualTo(new[]
                {
                    DuplicateGroupEntryKind.RetainedCopy,
                    DuplicateGroupEntryKind.LinkedPath,
                    DuplicateGroupEntryKind.ReclaimableCopy
                }));
            Assert.That(group.ReclaimableCopyCount, Is.EqualTo(1));
            Assert.That(group.ReclaimableSizeBytes, Is.EqualTo(4));
        });
    }

    [Test]
    public async Task AnalyzeAsyncSkipsNotLocalCandidateWithoutOpeningContent()
    {
        var root = RootWithFiles("a.bin", "b.bin");
        var metadata = new MetadataReader(new Dictionary<string, DuplicateCandidateMetadata>
        {
            ["/scan/a.bin"] = new(4, DuplicateContentAvailability.NotLocal),
            ["/scan/b.bin"] = new(4, DuplicateContentAvailability.Local)
        });
        var content = new ThrowingContentReader();
        var analyzer = new DuplicateAnalyzer(metadata, content);

        var result = await analyzer.AnalyzeAsync(root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Groups, Is.Empty);
            Assert.That(result.SkippedCandidates.Single().Reason, Is.EqualTo(DuplicateSkipReason.ContentsNotLocal));
            Assert.That(content.OpenCount, Is.Zero);
        });
    }

    [Test]
    public async Task AnalyzeAsyncSkipsChangedCandidate()
    {
        var root = RootWithFiles("a.bin", "b.bin");
        var metadata = new SequencedMetadataReader(new Dictionary<string, Queue<DuplicateCandidateMetadata>>
        {
            ["/scan/a.bin"] = new Queue<DuplicateCandidateMetadata>([
                new(4, DuplicateContentAvailability.Local),
                new(5, DuplicateContentAvailability.Local)
            ]),
            ["/scan/b.bin"] = new Queue<DuplicateCandidateMetadata>([
                new(4, DuplicateContentAvailability.Local),
                new(4, DuplicateContentAvailability.Local),
                new(4, DuplicateContentAvailability.Local),
                new(4, DuplicateContentAvailability.Local)
            ])
        });
        var content = ContentReader.WithFiles(
            ("/scan/a.bin", [1, 2, 3, 4]),
            ("/scan/b.bin", [1, 2, 3, 4]));
        var analyzer = new DuplicateAnalyzer(metadata, content);

        var result = await analyzer.AnalyzeAsync(root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Groups, Is.Empty);
            Assert.That(result.SkippedCandidates.Any(candidate => candidate.Reason == DuplicateSkipReason.Changed), Is.True);
        });
    }

    [Test]
    public async Task AnalyzeAsyncSkipsReplacedIdentityCandidate()
    {
        var root = RootWithFiles("a.bin", "b.bin");
        var metadata = new SequencedMetadataReader(new Dictionary<string, Queue<DuplicateCandidateMetadata>>
        {
            ["/scan/a.bin"] = new Queue<DuplicateCandidateMetadata>([
                new(4, DuplicateContentAvailability.Local, new FileIdentity(1, 1)),
                new(4, DuplicateContentAvailability.Local, new FileIdentity(1, 9))
            ]),
            ["/scan/b.bin"] = new Queue<DuplicateCandidateMetadata>([
                new(4, DuplicateContentAvailability.Local, new FileIdentity(1, 2)),
                new(4, DuplicateContentAvailability.Local, new FileIdentity(1, 2)),
                new(4, DuplicateContentAvailability.Local, new FileIdentity(1, 2)),
                new(4, DuplicateContentAvailability.Local, new FileIdentity(1, 2))
            ])
        });
        var content = ContentReader.WithFiles(
            ("/scan/a.bin", [1, 2, 3, 4]),
            ("/scan/b.bin", [1, 2, 3, 4]));
        var analyzer = new DuplicateAnalyzer(metadata, content);

        var result = await analyzer.AnalyzeAsync(root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Groups, Is.Empty);
            Assert.That(result.SkippedCandidates.Any(candidate => candidate.Reason == DuplicateSkipReason.Changed), Is.True);
        });
    }

    [Test]
    public async Task AnalyzeAsyncContinuesAfterReadFailure()
    {
        var root = RootWithFiles("a.bin", "b.bin", "c.bin");
        var metadata = MetadataReader.WithLengths(
            ("/scan/a.bin", 4),
            ("/scan/b.bin", 4),
            ("/scan/c.bin", 4));
        var content = ContentReader.WithFiles(
            ("/scan/a.bin", [1, 2, 3, 4]),
            ("/scan/c.bin", [1, 2, 3, 4]));
        content.ThrowOnOpen("/scan/b.bin");
        var analyzer = new DuplicateAnalyzer(metadata, content);

        var result = await analyzer.AnalyzeAsync(root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Groups, Has.Count.EqualTo(1));
            Assert.That(result.SkippedCandidates.Any(candidate => candidate.Item.Path == "/scan/b.bin"), Is.True);
        });
    }

    [Test]
    public void AnalyzeAsyncHonorsCancellationDuringMetadataReads()
    {
        var root = RootWithFiles("a.bin", "b.bin");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var analyzer = new DuplicateAnalyzer(
            MetadataReader.WithLengths(("/scan/a.bin", 4), ("/scan/b.bin", 4)),
            ContentReader.WithFiles());

        Assert.That(
            async () => await analyzer.AnalyzeAsync(
                root,
                cancellationToken: cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void AnalyzeAsyncHonorsCancellationDuringContentReads()
    {
        var root = RootWithFiles("a.bin", "b.bin");
        using var cancellation = new CancellationTokenSource();
        var metadata = MetadataReader.WithLengths(
            ("/scan/a.bin", 4),
            ("/scan/b.bin", 4));
        var content = ContentReader.WithFiles(
            ("/scan/a.bin", [1, 2, 3, 4]),
            ("/scan/b.bin", [1, 2, 3, 4]));
        content.CancelOnOpen(cancellation);
        var analyzer = new DuplicateAnalyzer(metadata, content);

        Assert.That(
            async () => await analyzer.AnalyzeAsync(
                root,
                cancellationToken: cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public async Task AnalyzeAsyncUsesBoundedStreamingReads()
    {
        var first = Enumerable.Range(0, 500).Select(value => (byte)(value % 251)).ToArray();
        var second = first.ToArray();
        var root = RootWithFiles("a.bin", "b.bin");
        var metadata = MetadataReader.WithLengths(
            ("/scan/a.bin", first.Length),
            ("/scan/b.bin", second.Length));
        var content = ContentReader.WithFiles(
            ("/scan/a.bin", first),
            ("/scan/b.bin", second));
        var analyzer = new DuplicateAnalyzer(metadata, content);
        var options = DuplicateAnalysisOptions.Default with
        {
            SampleSizeBytes = 8,
            BufferSizeBytes = 32
        };

        var result = await analyzer.AnalyzeAsync(root, options);

        Assert.Multiple(() =>
        {
            Assert.That(result.Groups, Has.Count.EqualTo(1));
            Assert.That(content.MaxRequestedReadLength, Is.LessThanOrEqualTo(32));
        });
    }

    [Test]
    public async Task AnalyzeAsyncDoesNotReportProgressForEveryCandidate()
    {
        const int fileCount = 300;
        var root = Directory("root", "/scan");
        var metadata = new Dictionary<string, DuplicateCandidateMetadata>(StringComparer.Ordinal);
        for (var index = 0; index < fileCount; index++)
        {
            var path = $"/scan/{index}.bin";
            root.AddChild(File($"{index}.bin", path, sizeBytes: 4096));
            metadata[path] = new DuplicateCandidateMetadata(
                index + 1,
                DuplicateContentAvailability.Local);
        }

        var reports = new List<DuplicateAnalysisProgress>();
        var analyzer = new DuplicateAnalyzer(
            new MetadataReader(metadata),
            new ThrowingContentReader());

        await analyzer.AnalyzeAsync(root, progress: new RecordingProgress(reports));

        var collectingReports = reports
            .Where(report => report.Stage == DuplicateAnalysisStage.CollectingCandidates)
            .Select(report => report.CandidatesExamined)
            .ToArray();
        Assert.That(collectingReports, Is.EqualTo(new long[] { 0, 1, 128, 256 }));
    }

    private static DiskItem RootWithFiles(params string[] names)
    {
        var root = Directory("root", "/scan");
        foreach (var name in names)
        {
            root.AddChild(File(name, $"/scan/{name}", sizeBytes: 4096));
        }

        return root;
    }

    private static DiskItem Directory(string name, string path) =>
        new(name, path, isDirectory: true);

    private static DiskItem File(string name, string path, long sizeBytes) =>
        new(name, path, isDirectory: false) { SizeBytes = sizeBytes };

    private sealed class MetadataReader(
        IReadOnlyDictionary<string, DuplicateCandidateMetadata> metadata)
        : IDuplicateCandidateMetadataReader
    {
        public static MetadataReader WithLengths(
            params (string Path, long Length)[] values) =>
            new(values.ToDictionary(
                value => value.Path,
                value => new DuplicateCandidateMetadata(
                    value.Length,
                    DuplicateContentAvailability.Local),
                StringComparer.Ordinal));

        public ValueTask<DuplicateCandidateMetadata> ReadAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(metadata[path]);
        }
    }

    private sealed class SequencedMetadataReader(
        IReadOnlyDictionary<string, Queue<DuplicateCandidateMetadata>> metadata)
        : IDuplicateCandidateMetadataReader
    {
        public ValueTask<DuplicateCandidateMetadata> ReadAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var queue = metadata[path];
            return ValueTask.FromResult(queue.Count == 1 ? queue.Peek() : queue.Dequeue());
        }
    }

    private sealed class ContentReader : IDuplicateContentReader
    {
        private readonly Dictionary<string, byte[]> _content;
        private readonly HashSet<string> _throwOnOpen = [];
        private CancellationTokenSource? _cancelOnOpen;

        private ContentReader(Dictionary<string, byte[]> content)
        {
            _content = content;
        }

        public int OpenCount { get; private set; }

        public int MaxRequestedReadLength { get; private set; }

        public static ContentReader WithFiles(params (string Path, byte[] Content)[] files) =>
            new(files.ToDictionary(
                file => file.Path,
                file => file.Content,
                StringComparer.Ordinal));

        public void ThrowOnOpen(string path) => _throwOnOpen.Add(path);

        public void CancelOnOpen(CancellationTokenSource cancellation) =>
            _cancelOnOpen = cancellation;

        public ValueTask<Stream> OpenReadAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCount++;
            if (_throwOnOpen.Contains(path))
            {
                throw new IOException("Read failed.");
            }

            _cancelOnOpen?.Cancel();

            return ValueTask.FromResult<Stream>(
                new TrackingStream(_content[path], requested =>
                    MaxRequestedReadLength = Math.Max(MaxRequestedReadLength, requested)));
        }
    }

    private sealed class ThrowingContentReader : IDuplicateContentReader
    {
        public int OpenCount { get; private set; }

        public ValueTask<Stream> OpenReadAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            OpenCount++;
            throw new InvalidOperationException("Content should not be opened.");
        }
    }

    private sealed class RecordingProgress(List<DuplicateAnalysisProgress> reports)
        : IProgress<DuplicateAnalysisProgress>
    {
        public void Report(DuplicateAnalysisProgress value) => reports.Add(value);
    }

    private sealed class TrackingStream(byte[] buffer, Action<int> recordRead) : MemoryStream(buffer)
    {
        public override ValueTask<int> ReadAsync(
            Memory<byte> destination,
            CancellationToken cancellationToken = default)
        {
            recordRead(destination.Length);
            return base.ReadAsync(destination, cancellationToken);
        }
    }
}
