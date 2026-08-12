using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core.Insights;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;
using NSubstitute;

namespace MacStorageAtlas.App.Tests;

public class MainWindowViewModelDuplicateAnalysisTests
{
    [Test]
    public void StartDuplicateAnalysisCommandIsDisabledBeforeCompletedScan()
    {
        var viewModel = CreateViewModel(CreateTree(), DuplicateAnalyzerWithFiles());

        Assert.That(viewModel.StartDuplicateAnalysisCommand.CanExecute(null), Is.False);
    }

    [Test]
    public async Task StartDuplicateAnalysisCommandFindsExactDuplicates()
    {
        var root = CreateTree();
        var analyzer = DuplicateAnalyzerWithFiles(
            ("/scan/a.bin", [1, 2, 3, 4]),
            ("/scan/b.bin", [1, 2, 3, 4]),
            ("/scan/c.bin", [9, 8, 7]));
        var viewModel = CreateViewModel(root, analyzer);
        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        await viewModel.StartDuplicateAnalysisCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.DuplicateGroups, Has.Count.EqualTo(1));
            Assert.That(viewModel.HasDuplicateGroups, Is.True);
            Assert.That(viewModel.DuplicateGroupCount, Is.EqualTo(1));
            Assert.That(viewModel.FormattedDuplicateReclaimableSize, Is.EqualTo("4 B"));
            Assert.That(
                viewModel.DuplicateAnalysisStatusMessage,
                Does.Contain("Found 1 exact duplicate group"));
            Assert.That(viewModel.IsAnalyzingDuplicates, Is.False);
        });
    }

    [Test]
    public async Task StartDuplicateAnalysisCommandReportsNoDuplicates()
    {
        var root = CreateTree();
        var analyzer = DuplicateAnalyzerWithFiles(
            ("/scan/a.bin", [1, 2, 3, 4]),
            ("/scan/b.bin", [1, 2, 3, 9]),
            ("/scan/c.bin", [9, 8, 7]));
        var viewModel = CreateViewModel(root, analyzer);
        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        await viewModel.StartDuplicateAnalysisCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.DuplicateGroups, Is.Empty);
            Assert.That(viewModel.DuplicateAnalysisStatusMessage, Is.EqualTo("No exact duplicates found."));
        });
    }

    [Test]
    public async Task StartDuplicateAnalysisCommandReportsSkippedFiles()
    {
        var root = CreateTree();
        var analyzer = DuplicateAnalyzerWithFiles(
            new Dictionary<string, DuplicateCandidateMetadata>
            {
                ["/scan/a.bin"] = new(4, DuplicateContentAvailability.NotLocal),
                ["/scan/b.bin"] = new(4, DuplicateContentAvailability.Local),
                ["/scan/c.bin"] = new(3, DuplicateContentAvailability.Local)
            },
            ("/scan/b.bin", [1, 2, 3, 4]),
            ("/scan/c.bin", [9, 8, 7]));
        var viewModel = CreateViewModel(root, analyzer);
        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        await viewModel.StartDuplicateAnalysisCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.DuplicateSkippedCandidateCount, Is.EqualTo(1));
            Assert.That(
                viewModel.DuplicateSkippedCandidates.Single().Reason,
                Is.EqualTo(DuplicateSkipReason.ContentsNotLocal));
            Assert.That(viewModel.DuplicateAnalysisStatusMessage, Does.Contain("Skipped 1 files"));
        });
    }

    [Test]
    public async Task CancelDuplicateAnalysisCommandCancelsRunningAnalysis()
    {
        var root = CreateTree();
        var content = new BlockingContentReader(
            ("/scan/a.bin", [1, 2, 3, 4]),
            ("/scan/b.bin", [1, 2, 3, 4]));
        var analyzer = new DuplicateAnalyzer(
            MetadataReader.WithLengths(
                ("/scan/a.bin", 4),
                ("/scan/b.bin", 4),
                ("/scan/c.bin", 3)),
            content);
        var viewModel = CreateViewModel(root, analyzer);
        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        var analysisTask = viewModel.StartDuplicateAnalysisCommand.ExecuteAsync(null);
        await content.OpenStarted.Task;
        viewModel.CancelDuplicateAnalysisCommand.Execute(null);
        await analysisTask;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsAnalyzingDuplicates, Is.False);
            Assert.That(viewModel.DuplicateAnalysisStatusMessage, Is.EqualTo("Duplicate analysis cancelled."));
            Assert.That(viewModel.DuplicateGroups, Is.Empty);
        });
    }

    [Test]
    public async Task DuplicateAnalysisCompletionDoesNotPopulateCleanupBasket()
    {
        var root = CreateTree();
        var analyzer = DuplicateAnalyzerWithFiles(
            ("/scan/a.bin", [1, 2, 3, 4]),
            ("/scan/b.bin", [1, 2, 3, 4]),
            ("/scan/c.bin", [9, 8, 7]));
        var viewModel = CreateViewModel(root, analyzer);
        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        await viewModel.StartDuplicateAnalysisCommand.ExecuteAsync(null);

        Assert.That(viewModel.CleanupBasketItems, Is.Empty);
    }

    [Test]
    public async Task DuplicateResultsClearWhenScanResultIsReplaced()
    {
        var root = CreateTree();
        var scanner = new QueueScanner(root, CreateTree("/other"));
        var viewModel = CreateViewModel(
            scanner,
            DuplicateAnalyzerWithFiles(
                ("/scan/a.bin", [1, 2, 3, 4]),
                ("/scan/b.bin", [1, 2, 3, 4]),
                ("/scan/c.bin", [9, 8, 7])));
        await viewModel.ScanFolderCommand.ExecuteAsync(null);
        await viewModel.StartDuplicateAnalysisCommand.ExecuteAsync(null);

        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.DuplicateGroups, Is.Empty);
            Assert.That(viewModel.DuplicateAnalysisStatusMessage, Is.Null);
        });
    }

    [Test]
    public async Task SelectedDuplicateEntryFeedsSelectedItemCommands()
    {
        var root = CreateTree();
        var analyzer = DuplicateAnalyzerWithFiles(
            ("/scan/a.bin", [1, 2, 3, 4]),
            ("/scan/b.bin", [1, 2, 3, 4]),
            ("/scan/c.bin", [9, 8, 7]));
        var viewModel = CreateViewModel(root, analyzer);
        await viewModel.ScanFolderCommand.ExecuteAsync(null);
        await viewModel.StartDuplicateAnalysisCommand.ExecuteAsync(null);

        viewModel.SelectedDuplicateEntry = viewModel.DuplicateGroups[0].Entries[1];
        viewModel.AddSelectedItemToCleanupBasketCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedItem?.Path, Is.EqualTo("/scan/b.bin"));
            Assert.That(viewModel.AddSelectedItemToCleanupBasketCommand.CanExecute(null), Is.True);
            Assert.That(viewModel.CleanupBasketItems.Single().Snapshot.Path, Is.EqualTo("/scan/b.bin"));
        });
    }

    private static MainWindowViewModel CreateViewModel(
        DiskItem root,
        DuplicateAnalyzer analyzer) =>
        CreateViewModel(new QueueScanner(root), analyzer);

    private static MainWindowViewModel CreateViewModel(
        IDiskScanner scanner,
        DuplicateAnalyzer analyzer)
    {
        var folderPicker = Substitute.For<IFolderPickerService>();
        var viewModel = new MainWindowViewModel(
            folderPicker,
            scanner,
            new ImmediateDispatcher(),
            duplicateAnalyzer: analyzer)
        {
            SelectedFolderPath = "/scan"
        };

        return viewModel;
    }

    private static DuplicateAnalyzer DuplicateAnalyzerWithFiles(
        params (string Path, byte[] Content)[] files) =>
        DuplicateAnalyzerWithFiles(
            files.ToDictionary(
                file => file.Path,
                file => new DuplicateCandidateMetadata(
                    file.Content.Length,
                    DuplicateContentAvailability.Local),
                StringComparer.Ordinal),
            files);

    private static DuplicateAnalyzer DuplicateAnalyzerWithFiles(
        IReadOnlyDictionary<string, DuplicateCandidateMetadata> metadata,
        params (string Path, byte[] Content)[] files) =>
        new(new MetadataReader(metadata), new MemoryContentReader(files));

    private static DiskItem CreateTree(string rootPath = "/scan")
    {
        var root = new DiskItem("scan", rootPath, isDirectory: true);
        root.AddChild(new DiskItem("a.bin", $"{rootPath}/a.bin", isDirectory: false)
        {
            SizeBytes = 4096
        });
        root.AddChild(new DiskItem("b.bin", $"{rootPath}/b.bin", isDirectory: false)
        {
            SizeBytes = 4096
        });
        root.AddChild(new DiskItem("c.bin", $"{rootPath}/c.bin", isDirectory: false)
        {
            SizeBytes = 4096
        });
        return root;
    }

    private sealed class QueueScanner(params DiskItem[] roots) : IDiskScanner
    {
        private readonly Queue<DiskItem> _roots = new(roots);

        public async IAsyncEnumerable<ScanProgress> ScanAsync(
            string rootPath,
            ScanOptions? options = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation]
            CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            var root = _roots.Count == 1 ? _roots.Peek() : _roots.Dequeue();
            yield return new ScanProgress(
                root.Path,
                FilesScanned: 3,
                DirectoriesScanned: 1,
                BytesScanned: root.SizeBytes,
                root,
                Errors: [],
                IsCompleted: true,
                options?.MeasurementMode ?? StorageMeasurementMode.Logical);
        }
    }

    private sealed class MetadataReader(
        IReadOnlyDictionary<string, DuplicateCandidateMetadata> metadata)
        : IDuplicateCandidateMetadataReader
    {
        public static MetadataReader WithLengths(params (string Path, long Length)[] values) =>
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

    private class MemoryContentReader : IDuplicateContentReader
    {
        private readonly IReadOnlyDictionary<string, byte[]> _files;

        public MemoryContentReader(params (string Path, byte[] Content)[] files)
        {
            _files = files.ToDictionary(
                file => file.Path,
                file => file.Content,
                StringComparer.Ordinal);
        }

        public virtual ValueTask<Stream> OpenReadAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<Stream>(new MemoryStream(_files[path]));
        }
    }

    private sealed class BlockingContentReader : MemoryContentReader
    {
        private readonly TaskCompletionSource _openStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public BlockingContentReader(params (string Path, byte[] Content)[] files)
            : base(files)
        {
        }

        public TaskCompletionSource OpenStarted => _openStarted;

        public override async ValueTask<Stream> OpenReadAsync(
            string path,
            CancellationToken cancellationToken = default)
        {
            _openStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
    }
}
