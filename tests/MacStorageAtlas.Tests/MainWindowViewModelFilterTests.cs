using System.Runtime.CompilerServices;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.Tests;

public class MainWindowViewModelFilterTests
{
    [Test]
    public async Task AnInactiveFilterShowsTheCompleteResult()
    {
        var viewModel = await CreateScannedViewModelAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsFilterActive, Is.False);
            Assert.That(viewModel.TreeSizeColumnHeader, Is.EqualTo("Size"));
            Assert.That(viewModel.LargeFiles, Has.Count.EqualTo(4));
            Assert.That(viewModel.TreeItems.Single().Children, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task AnActiveFilterNarrowsTheTreeToAncestorsOfMatches()
    {
        var viewModel = await CreateScannedViewModelAsync();

        viewModel.Filter.ExtensionsText = ".mov";
        await viewModel.TreePreparation;

        var rootNode = viewModel.TreeItems.Single();
        Assert.Multiple(() =>
        {
            Assert.That(rootNode.Children.Select(node => node.Name), Is.EqualTo(["Media"]));
            Assert.That(
                rootNode.Children.Single().Children.Select(node => node.Name),
                Is.EqualTo(["big.mov"]));
        });
    }

    [Test]
    public async Task AnActiveFilterNarrowsTheLargestFilesAndFileTypes()
    {
        var viewModel = await CreateScannedViewModelAsync();

        viewModel.Filter.ExtensionsText = ".mov";
        await viewModel.TreePreparation;

        Assert.Multiple(() =>
        {
            Assert.That(
                viewModel.LargeFiles.Select(file => file.Name),
                Is.EqualTo(["big.mov"]));
            Assert.That(
                viewModel.FileTypeSummaries.Select(summary => summary.Extension),
                Is.EqualTo([".mov"]));
        });
    }

    [Test]
    public async Task DirectoryRowsShowMatchedSubtotalsWhileFilteringAndFullSizeOtherwise()
    {
        var viewModel = await CreateScannedViewModelAsync();
        var unfilteredRoot = viewModel.TreeItems.Single();

        viewModel.Filter.ExtensionsText = ".mov";
        await viewModel.TreePreparation;
        var filteredRoot = viewModel.TreeItems.Single();

        Assert.Multiple(() =>
        {
            Assert.That(unfilteredRoot.HasMatchedSize, Is.False);
            Assert.That(unfilteredRoot.DisplaySize, Is.EqualTo(unfilteredRoot.FormattedSize));
            Assert.That(filteredRoot.HasMatchedSize, Is.True);
            Assert.That(filteredRoot.MatchedSizeBytes, Is.EqualTo(8192));
            Assert.That(
                filteredRoot.DisplaySize,
                Is.EqualTo(FileSizeFormatter.Format(8192)));
            Assert.That(filteredRoot.Item.SizeBytes, Is.EqualTo(11776));
        });
    }

    [Test]
    public async Task TheColumnHeaderDistinguishesMatchedSize()
    {
        var viewModel = await CreateScannedViewModelAsync();

        viewModel.Filter.ExtensionsText = ".mov";
        await viewModel.TreePreparation;
        var filteredHeader = viewModel.TreeSizeColumnHeader;

        viewModel.Filter.ClearFilterCommand.Execute(null);
        await viewModel.TreePreparation;

        Assert.Multiple(() =>
        {
            Assert.That(filteredHeader, Is.EqualTo("Matched size"));
            Assert.That(viewModel.TreeSizeColumnHeader, Is.EqualTo("Size"));
        });
    }

    [Test]
    public async Task TheTreemapKeepsItsFullLayoutWhileFiltering()
    {
        var viewModel = await CreateScannedViewModelAsync();
        var unfilteredRectangles = viewModel.TreemapRectangles;

        viewModel.Filter.ExtensionsText = ".mov";
        await viewModel.TreePreparation;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.TreemapRectangles, Is.SameAs(unfilteredRectangles));
            Assert.That(viewModel.TreemapRectangles, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task TheTreemapHighlightsOnlyVisibleItemsWhileFiltering()
    {
        var viewModel = await CreateScannedViewModelAsync();

        viewModel.Filter.ExtensionsText = ".mov";
        await viewModel.TreePreparation;

        Assert.That(
            viewModel.HighlightedTreemapItems.Select(item => item.Name),
            Is.EqualTo(["Media"]));
    }

    [Test]
    public async Task ClearingTheFilterRemovesTreemapHighlighting()
    {
        var viewModel = await CreateScannedViewModelAsync();
        viewModel.Filter.ExtensionsText = ".mov";
        await viewModel.TreePreparation;

        viewModel.Filter.ClearFilterCommand.Execute(null);
        await viewModel.TreePreparation;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HighlightedTreemapItems, Is.Empty);
            Assert.That(viewModel.IsFilterActive, Is.False);
        });
    }

    [Test]
    public async Task AnInvalidFilterIsReportedRatherThanShownAsZeroMatches()
    {
        var viewModel = await CreateScannedViewModelAsync();

        viewModel.Filter.MinimumSizeBytes = 4096;
        viewModel.Filter.MaximumSizeBytes = 1024;
        await viewModel.TreePreparation;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Filter.IsFilterValid, Is.False);
            Assert.That(viewModel.Filter.HasValidationError, Is.True);
            Assert.That(
                viewModel.Filter.ValidationMessage,
                Does.Contain("Minimum size is larger than maximum size"));
            Assert.That(viewModel.Filter.HasMatchSummary, Is.False);
        });
    }

    [Test]
    public async Task AValidFilterWithNoMatchesShowsAnEmptyState()
    {
        var viewModel = await CreateScannedViewModelAsync();

        viewModel.Filter.ExtensionsText = ".nothing";
        await viewModel.TreePreparation;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Filter.IsFilterValid, Is.True);
            Assert.That(viewModel.Filter.MatchCount, Is.Zero);
            Assert.That(viewModel.Filter.HasNoMatches, Is.True);
            Assert.That(viewModel.TreeItems, Is.Empty);
        });
    }

    [Test]
    public async Task MatchTotalsAreReported()
    {
        var viewModel = await CreateScannedViewModelAsync();

        viewModel.Filter.MinimumSizeBytes = 1024;
        await viewModel.TreePreparation;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Filter.MatchCount, Is.EqualTo(3));
            Assert.That(viewModel.Filter.MatchedBytes, Is.EqualTo(11264));
            Assert.That(
                viewModel.Filter.FormattedMatchedBytes,
                Is.EqualTo(FileSizeFormatter.Format(11264)));
        });
    }

    [Test]
    public async Task UnknownDateExclusionsAreReported()
    {
        var viewModel = await CreateScannedViewModelAsync();

        viewModel.Filter.ModifiedBefore.Instant = new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.Zero);
        await viewModel.TreePreparation;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Filter.UnknownDateExclusionCount, Is.EqualTo(4));
            Assert.That(viewModel.Filter.HasUnknownDateExclusions, Is.True);
            Assert.That(
                viewModel.Filter.UnknownDateExclusionMessage,
                Does.Contain("required date unknown"));
        });
    }

    [Test]
    public async Task RapidCriteriaChangesDisplayTheMostRecentCriteria()
    {
        var viewModel = await CreateScannedViewModelAsync();

        viewModel.Filter.ExtensionsText = ".pdf";
        viewModel.Filter.ExtensionsText = ".txt";
        viewModel.Filter.ExtensionsText = ".mov";
        await viewModel.TreePreparation;

        Assert.That(
            viewModel.LargeFiles.Select(file => file.Name),
            Is.EqualTo(["big.mov"]));
    }

    [Test]
    public async Task ASupersededEvaluationDoesNotUpdateTheDisplayedResult()
    {
        var viewModel = await CreateScannedViewModelAsync();

        viewModel.Filter.ExtensionsText = ".pdf";
        var superseded = viewModel.TreePreparation;
        viewModel.Filter.ExtensionsText = ".mov";
        await superseded;
        await viewModel.TreePreparation;

        Assert.That(
            viewModel.LargeFiles.Select(file => file.Name),
            Is.EqualTo(["big.mov"]));
    }

    [Test]
    public async Task ABurstOfCriteriaChangesIsDebouncedIntoOneEvaluation()
    {
        var viewModel = await CreateScannedViewModelAsync(
            debounceInterval: TimeSpan.FromMilliseconds(80));
        var dispatcher = (RecordingUiDispatcher)viewModel.Dispatcher;
        var baseline = dispatcher.InvocationCount;

        viewModel.Filter.ExtensionsText = ".p";
        viewModel.Filter.ExtensionsText = ".pd";
        viewModel.Filter.ExtensionsText = ".mov";
        await viewModel.TreePreparation;

        Assert.That(dispatcher.InvocationCount - baseline, Is.EqualTo(1));
    }

    [Test]
    public async Task SearchTextAndTheFilterTextTermStayInSync()
    {
        var viewModel = await CreateScannedViewModelAsync();

        viewModel.SearchText = "big";
        await viewModel.TreePreparation;
        var termFromSearch = viewModel.Filter.TextTerm;

        viewModel.Filter.TextTerm = "report";
        await viewModel.TreePreparation;

        Assert.Multiple(() =>
        {
            Assert.That(termFromSearch, Is.EqualTo("big"));
            Assert.That(viewModel.SearchText, Is.EqualTo("report"));
            Assert.That(
                viewModel.LargeFiles.Select(file => file.Name),
                Is.EqualTo(["report.pdf"]));
        });
    }

    [Test]
    public async Task ALargeFileSelectionIsClearedWhenItStopsMatching()
    {
        var viewModel = await CreateScannedViewModelAsync();
        viewModel.SelectedLargeFile = viewModel.LargeFiles.Single(
            file => file.Name == "report.pdf");

        viewModel.Filter.ExtensionsText = ".mov";
        await viewModel.TreePreparation;

        Assert.That(viewModel.SelectedLargeFile, Is.Null);
    }

    [Test]
    public async Task ALargeFileSelectionIsKeptWhenItStillMatches()
    {
        var viewModel = await CreateScannedViewModelAsync();
        var selected = viewModel.LargeFiles.Single(file => file.Name == "big.mov");
        viewModel.SelectedLargeFile = selected;

        viewModel.Filter.ExtensionsText = ".mov";
        await viewModel.TreePreparation;

        Assert.That(viewModel.SelectedLargeFile, Is.SameAs(selected));
    }

    [Test]
    public async Task ATreemapSelectionSurvivesFilteringBecauseTheTreemapKeepsEveryArea()
    {
        var viewModel = await CreateScannedViewModelAsync();
        var selected = viewModel.TreemapRectangles.First(
            rectangle => rectangle.Item.Item.Name == "Documents");
        viewModel.SelectedTreemapRectangle = selected;

        viewModel.Filter.ExtensionsText = ".mov";
        await viewModel.TreePreparation;

        Assert.That(viewModel.SelectedTreemapRectangle, Is.EqualTo(selected));
    }

    [Test]
    public async Task FilteringDoesNotStartAScanOrModifyMatchedFiles()
    {
        var scanner = new CountingDiskScanner(CreateTree());
        var viewModel = await CreateScannedViewModelAsync(scanner);
        var scanCount = scanner.ScanCount;

        viewModel.Filter.ExtensionsText = ".mov";
        await viewModel.TreePreparation;
        viewModel.Filter.ClearFilterCommand.Execute(null);
        await viewModel.TreePreparation;

        Assert.Multiple(() =>
        {
            Assert.That(scanner.ScanCount, Is.EqualTo(scanCount));
            Assert.That(scanner.Root.Children, Has.Count.EqualTo(2));
            Assert.That(
                scanner.Root.Children.Single(child => child.Name == "Media").Children,
                Has.Count.EqualTo(2));
            Assert.That(scanner.Root.SizeBytes, Is.EqualTo(11776));
        });
    }

    [Test]
    public async Task FilteringPreservesTheScanMeasurementBasis()
    {
        var viewModel = await CreateScannedViewModelAsync();
        var basis = viewModel.MeasurementBasisLabel;

        viewModel.Filter.ExtensionsText = ".mov";
        await viewModel.TreePreparation;

        Assert.That(viewModel.MeasurementBasisLabel, Is.EqualTo(basis));
    }

    [Test]
    public async Task BuiltInPresetsArePresentAndFactuallyNamed()
    {
        var viewModel = await CreateScannedViewModelAsync();

        var names = viewModel.Filter.Presets.Select(preset => preset.Name).ToArray();

        Assert.That(names, Is.EqualTo([
            BuiltInFilterPresets.LargerThanOneGigabyteName,
            BuiltInFilterPresets.NotModifiedForOneYearName,
            BuiltInFilterPresets.LargeArchivesName,
            BuiltInFilterPresets.LargeDiskImagesAndInstallersName
        ]));
    }

    [Test]
    public async Task ApplyingABuiltInPresetPopulatesTheCriteria()
    {
        var viewModel = await CreateScannedViewModelAsync();
        var preset = viewModel.Filter.Presets.Single(
            candidate => candidate.Name == BuiltInFilterPresets.LargerThanOneGigabyteName);

        viewModel.Filter.ApplyPresetCommand.Execute(preset);
        await viewModel.TreePreparation;

        Assert.Multiple(() =>
        {
            Assert.That(
                viewModel.Filter.MinimumSizeBytes,
                Is.EqualTo(BuiltInFilterPresets.OneGigabyte));
            Assert.That(viewModel.Filter.IsFilterActive, Is.True);
            Assert.That(viewModel.Filter.MatchCount, Is.Zero);
        });
    }

    [Test]
    public async Task ApplyingAPresetDoesNotChangeTheScanRoot()
    {
        var viewModel = await CreateScannedViewModelAsync();
        var root = viewModel.CurrentPath;
        var preset = viewModel.Filter.Presets.Single(
            candidate => candidate.Name == BuiltInFilterPresets.LargeArchivesName);

        viewModel.Filter.ApplyPresetCommand.Execute(preset);
        await viewModel.TreePreparation;

        Assert.That(viewModel.CurrentPath, Is.EqualTo(root));
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

        var media = new DiskItem("Media", "/Users/test/Media", isDirectory: true);
        media.AddChild(new DiskItem("big.mov", "/Users/test/Media/big.mov", isDirectory: false)
        {
            SizeBytes = 8192
        });
        media.AddChild(new DiskItem("tiny.log", "/Users/test/Media/tiny.log", isDirectory: false)
        {
            SizeBytes = 16
        });
        media.SizeBytes = 8208;

        var documents = new DiskItem("Documents", "/Users/test/Documents", isDirectory: true);
        documents.AddChild(new DiskItem(
            "report.pdf",
            "/Users/test/Documents/report.pdf",
            isDirectory: false)
        {
            SizeBytes = 2048
        });
        documents.AddChild(new DiskItem(
            "notes.txt",
            "/Users/test/Documents/notes.txt",
            isDirectory: false)
        {
            SizeBytes = 1024
        });
        documents.SizeBytes = 3072;

        root.AddChild(media);
        root.AddChild(documents);
        root.SizeBytes = 11776;
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
                FilesScanned: 4,
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
