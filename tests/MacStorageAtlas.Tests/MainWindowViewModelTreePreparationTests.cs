using System.Diagnostics;
using System.Runtime.CompilerServices;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.Tests;

public class MainWindowViewModelTreePreparationTests
{
    [Test]
    public async Task EmptySearchTextDisplaysTheCompleteScanResult()
    {
        var viewModel = await CreateScannedViewModelAsync();

        viewModel.SearchText = "report";
        await viewModel.TreePreparation;
        viewModel.SearchText = string.Empty;
        await viewModel.TreePreparation;

        Assert.That(
            viewModel.TreeItems.Single().Children.Select(node => node.Name),
            Is.EqualTo(["Documents", "Photos"]));
    }

    [Test]
    public async Task SearchTextDisplaysMatchesAndTheirAncestors()
    {
        var viewModel = await CreateScannedViewModelAsync();

        viewModel.SearchText = "report";
        await viewModel.TreePreparation;

        var rootNode = viewModel.TreeItems.Single();
        Assert.Multiple(() =>
        {
            Assert.That(
                rootNode.Children.Select(node => node.Name),
                Is.EqualTo(["Documents"]));
            Assert.That(
                rootNode.Children.Single().Children.Select(node => node.Name),
                Is.EqualTo(["report.pdf"]));
        });
    }

    [Test]
    public async Task SearchTextMatchingIgnoresLetterCase()
    {
        var viewModel = await CreateScannedViewModelAsync();

        viewModel.SearchText = "REPORT";
        await viewModel.TreePreparation;

        Assert.That(
            viewModel.TreeItems.Single().Children.Single().Children.Select(node => node.Name),
            Is.EqualTo(["report.pdf"]));
    }

    [Test]
    public async Task SearchTextMatchingNothingDisplaysAnEmptyTree()
    {
        var viewModel = await CreateScannedViewModelAsync();

        viewModel.SearchText = "no-such-item";
        await viewModel.TreePreparation;

        Assert.That(viewModel.TreeItems, Is.Empty);
    }

    [Test]
    public async Task ClearingSearchTextRestoresTheResultWithoutRescanning()
    {
        var scanner = new CountingDiskScanner(CreateTree());
        var viewModel = await CreateScannedViewModelAsync(scanner);
        var scanCountAfterInitialScan = scanner.ScanCount;

        viewModel.SearchText = "report";
        await viewModel.TreePreparation;
        viewModel.SearchText = string.Empty;
        await viewModel.TreePreparation;

        Assert.Multiple(() =>
        {
            Assert.That(scanner.ScanCount, Is.EqualTo(scanCountAfterInitialScan));
            Assert.That(
                viewModel.TreeItems.Single().Children.Select(node => node.Name),
                Is.EqualTo(["Documents", "Photos"]));
        });
    }

    [Test]
    public async Task RapidSearchChangesDisplayTheMostRecentSearchText()
    {
        var viewModel = await CreateScannedViewModelAsync();

        viewModel.SearchText = "report";
        viewModel.SearchText = "holiday";
        viewModel.SearchText = "notes";
        await viewModel.TreePreparation;

        Assert.That(
            viewModel.TreeItems.Single().Children.Single().Children.Select(node => node.Name),
            Is.EqualTo(["notes.txt"]));
    }

    [Test]
    public async Task ASupersededPreparationDoesNotUpdateTheDisplayedTree()
    {
        var viewModel = await CreateScannedViewModelAsync();

        viewModel.SearchText = "report";
        var superseded = viewModel.TreePreparation;
        viewModel.SearchText = "holiday";
        await superseded;
        await viewModel.TreePreparation;

        Assert.That(
            viewModel.TreeItems.Single().Children.Select(node => node.Name),
            Is.EqualTo(["Photos"]));
    }

    [Test]
    public async Task PreparationDoesNotBlockTheCallingThread()
    {
        var viewModel = await CreateScannedViewModelAsync(
            debounceInterval: TimeSpan.FromMilliseconds(200));

        var stopwatch = Stopwatch.StartNew();
        viewModel.SearchText = "report";
        stopwatch.Stop();

        await viewModel.TreePreparation;

        Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(150)));
    }

    [Test]
    public async Task DebouncingCollapsesABurstOfChangesIntoOnePreparation()
    {
        var scanner = new CountingDiskScanner(CreateTree());
        var viewModel = await CreateScannedViewModelAsync(
            scanner,
            debounceInterval: TimeSpan.FromMilliseconds(80));
        var dispatcher = (RecordingUiDispatcher)viewModel.Dispatcher;
        var invocationsAfterScan = dispatcher.InvocationCount;

        viewModel.SearchText = "r";
        viewModel.SearchText = "re";
        viewModel.SearchText = "rep";
        viewModel.SearchText = "report";
        await viewModel.TreePreparation;

        Assert.That(
            dispatcher.InvocationCount - invocationsAfterScan,
            Is.EqualTo(1));
    }

    [Test]
    public async Task PreparationDoesNotStartAScanOrChangeTheScanResult()
    {
        var scanner = new CountingDiskScanner(CreateTree());
        var viewModel = await CreateScannedViewModelAsync(scanner);
        var scanCountAfterInitialScan = scanner.ScanCount;

        viewModel.SearchText = "report";
        await viewModel.TreePreparation;

        Assert.Multiple(() =>
        {
            Assert.That(scanner.ScanCount, Is.EqualTo(scanCountAfterInitialScan));
            Assert.That(scanner.Root.Children, Has.Count.EqualTo(2));
            Assert.That(
                scanner.Root.Children.Single(child => child.Name == "Documents").Children,
                Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task PreparedNodesKeepTheScanMeasurementBasis()
    {
        var viewModel = await CreateScannedViewModelAsync();

        viewModel.SearchText = "report";
        await viewModel.TreePreparation;

        var reportNode = viewModel.TreeItems
            .Single()
            .Children
            .Single()
            .Children
            .Single();

        Assert.Multiple(() =>
        {
            Assert.That(reportNode.SizeBytes, Is.EqualTo(2048));
            Assert.That(reportNode.FormattedSize, Is.EqualTo(FileSizeFormatter.Format(2048)));
        });
    }

    [Test]
    public async Task SearchChangeClearsASelectionThatIsNoLongerDisplayed()
    {
        var viewModel = await CreateScannedViewModelAsync();
        viewModel.SelectedTreeItem = viewModel.TreeItems
            .Single()
            .Children
            .Single(node => node.Name == "Photos");

        viewModel.SearchText = "report";
        await viewModel.TreePreparation;

        Assert.That(viewModel.SelectedTreeItem, Is.Null);
    }

    [Test]
    public async Task SearchChangeKeepsASelectionThatIsStillDisplayed()
    {
        var viewModel = await CreateScannedViewModelAsync();
        var rootNode = viewModel.TreeItems.Single();
        viewModel.SelectedTreeItem = rootNode;

        viewModel.SearchText = "report";
        await viewModel.TreePreparation;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedTreeItem, Is.Not.Null);
            Assert.That(viewModel.SelectedTreeItem!.Item, Is.SameAs(rootNode.Item));
            Assert.That(
                viewModel.SelectedTreeItem,
                Is.SameAs(viewModel.TreeItems.Single()));
        });
    }

    private static async Task<TestableMainWindowViewModel> CreateScannedViewModelAsync(
        CountingDiskScanner? scanner = null,
        TimeSpan? debounceInterval = null)
    {
        var viewModel = new TestableMainWindowViewModel(
            scanner ?? new CountingDiskScanner(CreateTree()),
            new RecordingUiDispatcher(),
            debounceInterval ?? TimeSpan.Zero)
        {
            SelectedFolderPath = "/Users/test"
        };

        await viewModel.ScanFolderCommand.ExecuteAsync(null);
        return viewModel;
    }

    private static DiskItem CreateTree()
    {
        var root = new DiskItem("root", "/Users/test", isDirectory: true);
        var documents = new DiskItem("Documents", "/Users/test/Documents", isDirectory: true);
        var report = new DiskItem(
            "report.pdf",
            "/Users/test/Documents/report.pdf",
            isDirectory: false)
        {
            SizeBytes = 2048
        };
        var notes = new DiskItem(
            "notes.txt",
            "/Users/test/Documents/notes.txt",
            isDirectory: false)
        {
            SizeBytes = 512
        };
        documents.AddChild(report);
        documents.AddChild(notes);
        documents.SizeBytes = 2560;

        var photos = new DiskItem("Photos", "/Users/test/Photos", isDirectory: true);
        var holiday = new DiskItem(
            "holiday.jpg",
            "/Users/test/Photos/holiday.jpg",
            isDirectory: false)
        {
            SizeBytes = 4096
        };
        photos.AddChild(holiday);
        photos.SizeBytes = 4096;

        root.AddChild(documents);
        root.AddChild(photos);
        root.SizeBytes = 6656;
        return root;
    }

    private sealed class TestableMainWindowViewModel : MainWindowViewModel
    {
        public TestableMainWindowViewModel(
            IDiskScanner scanner,
            IUiDispatcher dispatcher,
            TimeSpan debounceInterval)
            : base(
                new NullFolderPickerService(),
                scanner,
                dispatcher,
                searchDebounceInterval: debounceInterval)
        {
            Dispatcher = dispatcher;
        }

        public IUiDispatcher Dispatcher { get; }
    }

    private sealed class CountingDiskScanner : IDiskScanner
    {
        public CountingDiskScanner(DiskItem root)
        {
            Root = root;
        }

        public DiskItem Root { get; }

        public int ScanCount { get; private set; }

        public async IAsyncEnumerable<ScanProgress> ScanAsync(
            string rootPath,
            ScanOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ScanCount++;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            yield return new ScanProgress(
                rootPath,
                FilesScanned: 3,
                DirectoriesScanned: 3,
                BytesScanned: Root.SizeBytes,
                Root: Root,
                Errors: [],
                IsCompleted: true,
                MeasurementMode: (options ?? ScanOptions.Default).MeasurementMode);
        }
    }

    private sealed class RecordingUiDispatcher : IUiDispatcher
    {
        public int InvocationCount { get; private set; }

        public Task InvokeAsync(Action action)
        {
            InvocationCount++;
            action();
            return Task.CompletedTask;
        }
    }
}
