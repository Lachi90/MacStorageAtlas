using System.Runtime.CompilerServices;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core.Cleanup;
using MacStorageAtlas.Core.Export;
using MacStorageAtlas.Core.History;
using MacStorageAtlas.Core.Insights;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Platform;
using MacStorageAtlas.Core.Scanning;
using NSubstitute;

namespace MacStorageAtlas.App.Tests;

public class MainWindowViewModelInsightScopeTests
{
    private const string RootPath = "/scan";
    private const string DocumentsPath = "/scan/Documents";
    private const string OldProjectsPath = "/scan/Documents/Old Projects";
    private const string DownloadsPath = "/scan/Downloads";
    private const string NestedPath = "/scan/Downloads/Nested";
    private const string EmptyPath = "/scan/Empty";

    private static readonly DateTimeOffset Now = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            $"MacStorageAtlasInsightScope-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public async Task ACompletedScanStartsWithTheWholeResultInScope()
    {
        var context = await ScannedAsync();

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.InsightScope, Is.SameAs(Root(context)));
            Assert.That(context.ViewModel.IsInsightScopeNarrowed, Is.False);
            Assert.That(context.ViewModel.InsightScopeCaption, Is.EqualTo("Showing the whole scan"));
            Assert.That(context.ViewModel.ResetInsightScopeCommand.CanExecute(null), Is.False);
            Assert.That(
                LargeFileNames(context),
                Is.EqualTo(new[] { "movie.mov", "legacy.zip", "setup.dmg", "report.pdf", "top.txt" }));
        });
    }

    [Test]
    public async Task SelectingAFolderInTheTreeScopesBothTabsToItsDescendants()
    {
        var context = await ScannedAsync();

        SelectTreeNode(context, DownloadsPath);
        await context.ViewModel.InsightPreparation;

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.InsightScope?.Path, Is.EqualTo(DownloadsPath));
            Assert.That(LargeFileNames(context), Is.EqualTo(new[] { "movie.mov", "setup.dmg" }));
            Assert.That(
                context.ViewModel.FileTypeSummaries.Select(summary => summary.Extension),
                Is.EqualTo(new[] { ".mov", ".dmg" }));
            Assert.That(context.ViewModel.InsightScopeCaption, Is.EqualTo("Showing files in “Downloads”"));
            Assert.That(context.ViewModel.InsightScopePath, Is.EqualTo(DownloadsPath));
            Assert.That(context.ViewModel.IsInsightScopeNarrowed, Is.True);
        });
    }

    [Test]
    public async Task SelectingAFolderInTheTreemapScopesBothTabs()
    {
        var context = await ScannedAsync();

        SelectTreemapItem(context, DocumentsPath);
        await context.ViewModel.InsightPreparation;

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.InsightScope?.Path, Is.EqualTo(DocumentsPath));
            Assert.That(LargeFileNames(context), Is.EqualTo(new[] { "legacy.zip", "report.pdf" }));
            Assert.That(
                context.ViewModel.FileTypeSummaries.Sum(summary => summary.FileCount),
                Is.EqualTo(2));
        });
    }

    [Test]
    public async Task SelectingANestedFileScopesTheTabsToItsContainingFolder()
    {
        var context = await ScannedAsync();

        SelectTreeNode(context, NestedPath + "/setup.dmg");
        await context.ViewModel.InsightPreparation;

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.InsightScope?.Path, Is.EqualTo(NestedPath));
            Assert.That(LargeFileNames(context), Is.EqualTo(new[] { "setup.dmg" }));
        });
    }

    [Test]
    public async Task SelectingAFileInTheScanRootScopesTheTabsToTheWholeScan()
    {
        var context = await ScannedAsync();
        SelectTreemapItem(context, DownloadsPath);
        await context.ViewModel.InsightPreparation;

        SelectTreemapItem(context, RootPath + "/top.txt");
        await context.ViewModel.InsightPreparation;

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.InsightScope, Is.SameAs(Root(context)));
            Assert.That(context.ViewModel.LargeFiles, Has.Count.EqualTo(5));
        });
    }

    [Test]
    public async Task SelectingALargestFilesEntryKeepsTheScopeAndTheList()
    {
        var context = await ScannedAsync();
        SelectTreeNode(context, DownloadsPath);
        await context.ViewModel.InsightPreparation;
        var listed = context.ViewModel.LargeFiles;

        context.ViewModel.SelectedLargeFile = listed[1];
        await context.ViewModel.InsightPreparation;

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.SelectedTreeItem, Is.Null);
            Assert.That(context.ViewModel.InsightScope?.Path, Is.EqualTo(DownloadsPath));
            Assert.That(context.ViewModel.LargeFiles, Is.SameAs(listed));
            Assert.That(context.ViewModel.SelectedLargeFile, Is.SameAs(listed[1]));
        });
    }

    [Test]
    public async Task SelectingADuplicateOutsideTheScopeKeepsTheScope()
    {
        var context = await ScannedAsync();
        SelectTreeNode(context, DownloadsPath);
        await context.ViewModel.InsightPreparation;
        var outside = Find(context, DocumentsPath + "/report.pdf");

        context.ViewModel.SelectedDuplicateEntry = new DuplicateGroupEntry(
            outside,
            outside.SizeBytes,
            DuplicateGroupEntryKind.ReclaimableCopy);
        await context.ViewModel.InsightPreparation;

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.SelectedTreeItem, Is.Null);
            Assert.That(context.ViewModel.InsightScope?.Path, Is.EqualTo(DownloadsPath));
            Assert.That(LargeFileNames(context), Is.EqualTo(new[] { "movie.mov", "setup.dmg" }));
        });
    }

    [Test]
    public async Task AFolderWithoutFilesYieldsEmptyTabsThatSaySo()
    {
        var context = await ScannedAsync();

        SelectTreeNode(context, EmptyPath);
        await context.ViewModel.InsightPreparation;

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.LargeFiles, Is.Empty);
            Assert.That(context.ViewModel.FileTypeSummaries, Is.Empty);
            Assert.That(context.ViewModel.ShowLargeFilesEmptyMessage, Is.True);
            Assert.That(context.ViewModel.ShowFileTypesEmptyMessage, Is.True);
            Assert.That(context.ViewModel.InsightScopeEmptyMessage, Is.EqualTo("No files in “Empty”."));
        });
    }

    [Test]
    public async Task AFilterStillAppliesInsideTheScope()
    {
        var context = await ScannedAsync();
        SelectTreeNode(context, DocumentsPath);
        await context.ViewModel.InsightPreparation;

        context.ViewModel.Filter.ExtensionsText = ".zip";
        await context.ViewModel.TreePreparation;
        await context.ViewModel.InsightPreparation;

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.InsightScope?.Path, Is.EqualTo(DocumentsPath));
            Assert.That(LargeFileNames(context), Is.EqualTo(new[] { "legacy.zip" }));
            Assert.That(
                context.ViewModel.FileTypeSummaries.Select(summary => summary.Extension),
                Is.EqualTo(new[] { ".zip" }));
            Assert.That(
                context.ViewModel.LargeFiles,
                Has.All.Matches<DiskItem>(context.ViewModel.IsMatched));
        });
    }

    [Test]
    public async Task ChangingTheScopeWhileFilteredDescribesMatchesInTheNewScope()
    {
        var context = await ScannedAsync();
        context.ViewModel.Filter.ExtensionsText = ".zip, .dmg";
        await context.ViewModel.TreePreparation;

        SelectTreemapItem(context, DownloadsPath);
        await context.ViewModel.InsightPreparation;

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.InsightScope?.Path, Is.EqualTo(DownloadsPath));
            Assert.That(LargeFileNames(context), Is.EqualTo(new[] { "setup.dmg" }));
        });
    }

    [Test]
    public async Task AFilterThatHidesTheScopeFolderKeepsTheScopeAndYieldsEmptyTabs()
    {
        var context = await ScannedAsync();
        SelectTreeNode(context, DownloadsPath);
        await context.ViewModel.InsightPreparation;

        context.ViewModel.Filter.ExtensionsText = ".pdf";
        await context.ViewModel.TreePreparation;
        await context.ViewModel.InsightPreparation;

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.SelectedTreeItem, Is.Null);
            Assert.That(context.ViewModel.InsightScope?.Path, Is.EqualTo(DownloadsPath));
            Assert.That(context.ViewModel.LargeFiles, Is.Empty);
            Assert.That(context.ViewModel.FileTypeSummaries, Is.Empty);
            Assert.That(
                context.ViewModel.InsightScopeEmptyMessage,
                Is.EqualTo("No matching files in “Downloads”."));
            Assert.That(context.ViewModel.ResetInsightScopeCommand.CanExecute(null), Is.True);
        });
    }

    [Test]
    public async Task RapidScopeChangesShowOnlyTheLatestScope()
    {
        var context = await ScannedAsync();

        SelectTreeNode(context, DownloadsPath);
        var superseded = context.ViewModel.InsightPreparation;
        SelectTreeNode(context, DocumentsPath);
        SelectTreemapItem(context, EmptyPath);
        SelectTreeNode(context, OldProjectsPath);
        await superseded;
        await context.ViewModel.InsightPreparation;

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.InsightScope?.Path, Is.EqualTo(OldProjectsPath));
            Assert.That(LargeFileNames(context), Is.EqualTo(new[] { "legacy.zip" }));
        });
    }

    [Test]
    public async Task ChangingTheScopeReadsNoScannedPathAndKeepsTheMeasurementLabels()
    {
        var context = await ScannedAsync();
        var measurementLabel = context.ViewModel.MeasurementBasisLabel;
        var measurementMode = context.ViewModel.ResultMeasurementMode;

        SelectTreeNode(context, DownloadsPath);
        await context.ViewModel.InsightPreparation;
        SelectTreemapItem(context, NestedPath);
        await context.ViewModel.InsightPreparation;
        context.ViewModel.ResetInsightScopeCommand.Execute(null);
        await context.ViewModel.InsightPreparation;

        Assert.Multiple(() =>
        {
            Assert.That(context.Scanner.FullScanCount, Is.EqualTo(1));
            Assert.That(context.Scanner.SubtreeScanCount, Is.Zero);
            Assert.That(context.MetadataReader.ReadCount, Is.Zero);
            Assert.That(context.ViewModel.MeasurementBasisLabel, Is.EqualTo(measurementLabel));
            Assert.That(context.ViewModel.ResultMeasurementMode, Is.EqualTo(measurementMode));
            Assert.That(context.Reveal.ReceivedCalls(), Is.Empty);
            Assert.That(context.Trash.ReceivedCalls(), Is.Empty);
        });
    }

    [Test]
    public async Task ResettingTheScopeReturnsToTheWholeScanWithoutChangingTheSelection()
    {
        var context = await ScannedAsync();
        SelectTreeNode(context, DownloadsPath);
        await context.ViewModel.InsightPreparation;
        var selected = context.ViewModel.SelectedTreeItem;
        var treemap = context.ViewModel.TreemapRectangles;
        var availableWhileNarrowed = context.ViewModel.ResetInsightScopeCommand.CanExecute(null);

        context.ViewModel.ResetInsightScopeCommand.Execute(null);
        await context.ViewModel.InsightPreparation;

        Assert.Multiple(() =>
        {
            Assert.That(availableWhileNarrowed, Is.True);
            Assert.That(context.ViewModel.InsightScope, Is.SameAs(Root(context)));
            Assert.That(context.ViewModel.LargeFiles, Has.Count.EqualTo(5));
            Assert.That(context.ViewModel.SelectedTreeItem, Is.SameAs(selected));
            Assert.That(context.ViewModel.TreemapRectangles, Is.SameAs(treemap));
            Assert.That(context.ViewModel.ResetInsightScopeCommand.CanExecute(null), Is.False);
            Assert.That(context.ViewModel.InsightScopeCaption, Is.EqualTo("Showing the whole scan"));
        });
    }

    [Test]
    public async Task TheResetCommandIsUnavailableWithoutAResult()
    {
        var context = Create();

        var beforeScan = context.ViewModel.ResetInsightScopeCommand.CanExecute(null);
        await context.ViewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(beforeScan, Is.False);
            Assert.That(context.ViewModel.HasInsightScope, Is.True);
            Assert.That(context.ViewModel.ResetInsightScopeCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public async Task FilterMatchTotalsIgnoreANarrowedScope()
    {
        var context = await ScannedAsync();
        SelectTreeNode(context, DownloadsPath);
        await context.ViewModel.InsightPreparation;

        context.Filter().ExtensionsText = ".zip, .mov";
        await context.ViewModel.TreePreparation;
        await context.ViewModel.InsightPreparation;

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.InsightScope?.Path, Is.EqualTo(DownloadsPath));
            Assert.That(LargeFileNames(context), Is.EqualTo(new[] { "movie.mov" }));
            Assert.That(context.Filter().MatchCount, Is.EqualTo(2));
            Assert.That(context.Filter().MatchedBytes, Is.EqualTo(1600));
        });
    }

    [Test]
    public async Task ExportIgnoresANarrowedScope()
    {
        var destination = Path.Combine(_directory, "result.csv");
        var context = await ScannedAsync(savePath: destination);
        SelectTreeNode(context, DownloadsPath);
        await context.ViewModel.InsightPreparation;

        await context.ViewModel.ExportCsvCommand.ExecuteAsync(null);

        Assert.That(File.ReadAllLines(destination), Has.Length.EqualTo(CountItems(Root(context)) + 1));
    }

    [Test]
    public async Task ScanHistoryCaptureIgnoresANarrowedScope()
    {
        var context = await ScannedAsync(historyEnabled: true);
        context.Scanner.SubtreeReplacement = () => Folder(
            NestedPath,
            FileItem(NestedPath + "/setup.dmg", 500),
            FileItem(NestedPath + "/added.pkg", 50));
        SelectTreeNode(context, NestedPath);
        await context.ViewModel.InsightPreparation;

        await context.ViewModel.RescanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.InsightScope?.Path, Is.EqualTo(NestedPath));
            Assert.That(context.History.RowCounts, Is.Not.Empty);
            Assert.That(context.History.RowCounts[^1], Is.EqualTo(CountItems(Root(context))));
        });
    }

    [Test]
    public async Task TrashingTheScopeFolderMovesTheScopeToItsParent()
    {
        var context = await ScannedAsync();
        SelectTreeNode(context, OldProjectsPath);
        await context.ViewModel.InsightPreparation;

        await context.ViewModel.MoveToTrashCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.InsightScope?.Path, Is.EqualTo(DocumentsPath));
            Assert.That(context.ViewModel.InsightScope, Is.SameAs(Find(context, DocumentsPath)));
            Assert.That(LargeFileNames(context), Is.EqualTo(new[] { "report.pdf" }));
        });
    }

    [Test]
    public async Task TrashingAnAncestorOfTheScopeMovesTheScopeToTheNearestRemainingFolder()
    {
        var context = await ScannedAsync(confirmCleanup: true);
        SelectTreeNode(context, DownloadsPath);
        context.ViewModel.AddSelectedItemToCleanupBasketCommand.Execute(null);
        SelectTreeNode(context, NestedPath);
        await context.ViewModel.InsightPreparation;

        await context.ViewModel.MoveCleanupBasketToTrashCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.CleanupBasketSucceededCount, Is.EqualTo(1));
            Assert.That(context.ViewModel.InsightScope, Is.SameAs(Root(context)));
            Assert.That(
                LargeFileNames(context),
                Is.EqualTo(new[] { "legacy.zip", "report.pdf", "top.txt" }));
        });
    }

    [Test]
    public async Task TrashingAnUnrelatedItemKeepsTheScope()
    {
        var context = await ScannedAsync();
        SelectTreeNode(context, DownloadsPath);
        await context.ViewModel.InsightPreparation;
        var scope = context.ViewModel.InsightScope;
        context.ViewModel.SelectedDuplicateEntry = new DuplicateGroupEntry(
            Find(context, RootPath + "/top.txt"),
            100,
            DuplicateGroupEntryKind.ReclaimableCopy);

        await context.ViewModel.MoveToTrashCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.InsightScope, Is.SameAs(scope));
            Assert.That(LargeFileNames(context), Is.EqualTo(new[] { "movie.mov", "setup.dmg" }));
        });
    }

    [Test]
    public async Task ARescannedSubtreeKeepsTheScopeByPath()
    {
        var context = await ScannedAsync();
        context.Scanner.SubtreeReplacement = () => Folder(
            DownloadsPath,
            FileItem(DownloadsPath + "/fresh.iso", 2000),
            Folder(NestedPath, FileItem(NestedPath + "/setup.dmg", 500)));
        SelectTreeNode(context, DownloadsPath);
        await context.ViewModel.InsightPreparation;
        var stale = context.ViewModel.InsightScope;

        await context.ViewModel.RescanFolderCommand.ExecuteAsync(null);
        await context.ViewModel.InsightPreparation;

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.InsightScope, Is.Not.SameAs(stale));
            Assert.That(context.ViewModel.InsightScope, Is.SameAs(Find(context, DownloadsPath)));
            Assert.That(LargeFileNames(context), Is.EqualTo(new[] { "fresh.iso", "setup.dmg" }));
        });
    }

    [Test]
    public async Task ARescannedSubtreeWithoutTheScopeFolderMovesTheScopeToItsParent()
    {
        var context = await ScannedAsync();
        context.Scanner.SubtreeReplacement = () => null;
        SelectTreeNode(context, OldProjectsPath);
        await context.ViewModel.InsightPreparation;

        await context.ViewModel.RescanFolderCommand.ExecuteAsync(null);
        await context.ViewModel.InsightPreparation;

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.InsightScope, Is.SameAs(Find(context, DocumentsPath)));
            Assert.That(LargeFileNames(context), Is.EqualTo(new[] { "report.pdf" }));
        });
    }

    [Test]
    public async Task ANewScanResetsTheScopeToItsRoot()
    {
        var context = await ScannedAsync();
        SelectTreeNode(context, DownloadsPath);
        await context.ViewModel.InsightPreparation;

        await context.ViewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.InsightScope, Is.SameAs(Root(context)));
            Assert.That(context.ViewModel.IsInsightScopeNarrowed, Is.False);
            Assert.That(context.ViewModel.LargeFiles, Has.Count.EqualTo(5));
        });
    }

    [Test]
    public async Task AFolderRescanWaitsForAPendingInsightPreparation()
    {
        var context = await ScannedAsync();
        context.Scanner.SubtreeReplacement = () => Folder(
            NestedPath,
            FileItem(NestedPath + "/setup.dmg", 500));
        SelectTreeNode(context, NestedPath);
        var pending = context.ViewModel.InsightPreparation;

        await context.ViewModel.RescanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(pending.IsCompleted, Is.True);
            Assert.That(context.Scanner.SubtreeScanCount, Is.EqualTo(1));
            Assert.That(context.ViewModel.InsightScope, Is.SameAs(Find(context, NestedPath)));
        });
    }

    private async Task<ScopeContext> ScannedAsync(
        string? savePath = null,
        bool historyEnabled = false,
        bool confirmCleanup = false)
    {
        var context = Create(savePath, historyEnabled, confirmCleanup);
        await context.ViewModel.ScanFolderCommand.ExecuteAsync(null);
        return context;
    }

    private static ScopeContext Create(
        string? savePath = null,
        bool historyEnabled = false,
        bool confirmCleanup = false)
    {
        var scanner = new ScopeScanner(CreateTree);
        var reveal = Substitute.For<IFileRevealService>();
        var trash = Substitute.For<ITrashService>();
        var confirmation = Substitute.For<ITrashConfirmationService>();
        confirmation.ConfirmMoveToTrashAsync(Arg.Any<DiskItem>()).Returns(true);
        var review = Substitute.For<ICleanupBasketReviewService>();
        review.ConfirmCleanupAsync(Arg.Any<CleanupBasketReview>()).Returns(confirmCleanup);
        var savePicker = Substitute.For<ISaveFilePickerService>();
        savePicker
            .SelectSaveFileAsync(Arg.Any<ScanExportFormat>(), Arg.Any<string>())
            .Returns(savePath);
        var metadataReader = new SnapshotReader(scanner);
        var history = new RecordingHistoryStore();
        var viewModel = new MainWindowViewModel(
            new NullFolderPickerService(),
            scanner,
            new ImmediateDispatcher(),
            reveal,
            trash,
            confirmation,
            quickLookService: Substitute.For<IQuickLookService>(),
            saveFilePickerService: savePicker,
            cleanupBasketReviewService: review,
            cleanupFileSystemMetadataReader: metadataReader,
            scanHistoryStore: history,
            searchDebounceInterval: TimeSpan.Zero,
            referenceTimeProvider: () => Now)
        {
            SelectedFolderPath = RootPath,
            MeasurementMode = StorageMeasurementMode.Logical,
            IncludeHiddenFiles = false,
            FollowSymbolicLinks = false,
            ScanHistoryEnabled = historyEnabled
        };

        return new ScopeContext(viewModel, scanner, reveal, trash, metadataReader, history);
    }

    private static DiskItem CreateTree()
    {
        var root = new DiskItem("scan", RootPath, isDirectory: true);
        var documents = Folder(
            DocumentsPath,
            Folder(OldProjectsPath, FileItem(OldProjectsPath + "/legacy.zip", 700)),
            FileItem(DocumentsPath + "/report.pdf", 300));
        var downloads = Folder(
            DownloadsPath,
            FileItem(DownloadsPath + "/movie.mov", 900),
            Folder(NestedPath, FileItem(NestedPath + "/setup.dmg", 500)));
        var empty = Folder(EmptyPath);
        empty.SizeBytes = 1;
        root.AddChild(documents);
        root.AddChild(downloads);
        root.AddChild(empty);
        root.AddChild(FileItem(RootPath + "/top.txt", 100));
        root.SizeBytes = root.Children.Sum(child => child.SizeBytes);
        return root;
    }

    private static DiskItem Folder(string path, params DiskItem[] children)
    {
        var folder = new DiskItem(Path.GetFileName(path), path, isDirectory: true);
        foreach (var child in children)
        {
            folder.AddChild(child);
        }

        folder.SizeBytes = children.Sum(child => child.SizeBytes);
        return folder;
    }

    private static DiskItem FileItem(string path, long size) =>
        new(Path.GetFileName(path), path, isDirectory: false) { SizeBytes = size };

    private static int CountItems(DiskItem item) =>
        1 + item.Children.Sum(CountItems);

    private static DiskItem Root(ScopeContext context) =>
        context.ViewModel.TreeItems.Single().Item;

    private static DiskItem Find(ScopeContext context, string path) =>
        ScanResultMerger.FindByPath(Root(context), path)
        ?? throw new InvalidOperationException($"{path} is not in the result.");

    private static IEnumerable<string> LargeFileNames(ScopeContext context) =>
        context.ViewModel.LargeFiles.Select(file => file.Name);

    private static void SelectTreeNode(ScopeContext context, string path)
    {
        var node = context.ViewModel.TreeItems.Single();
        while (!string.Equals(node.Item.Path, path, StringComparison.Ordinal))
        {
            node = node.Children.Single(child =>
                string.Equals(child.Item.Path, path, StringComparison.Ordinal)
                || path.StartsWith(child.Item.Path + "/", StringComparison.Ordinal));
        }

        context.ViewModel.SelectedTreeItem = node;
    }

    private static void SelectTreemapItem(ScopeContext context, string path)
    {
        var item = Find(context, path);
        var parent = Find(context, Path.GetDirectoryName(path)!);
        if (!context.ViewModel.TreemapRectangles.Any(rectangle => ReferenceEquals(rectangle.Item.Item, item)))
        {
            SelectTreeNode(context, parent.Path);
        }

        context.ViewModel.SelectedTreemapRectangle = context.ViewModel.TreemapRectangles
            .Single(rectangle => ReferenceEquals(rectangle.Item.Item, item));
    }

    private sealed record ScopeContext(
        MainWindowViewModel ViewModel,
        ScopeScanner Scanner,
        IFileRevealService Reveal,
        ITrashService Trash,
        SnapshotReader MetadataReader,
        RecordingHistoryStore History)
    {
        public ResultFilterViewModel Filter() => ViewModel.Filter;
    }

    private sealed class ScopeScanner(Func<DiskItem> createRoot) : IDiskScanner
    {
        public DiskItem? LastRoot { get; private set; }

        public int FullScanCount { get; private set; }

        public int SubtreeScanCount { get; private set; }

        public Func<DiskItem?>? SubtreeReplacement { get; set; }

        public async IAsyncEnumerable<ScanProgress> ScanAsync(
            string rootPath,
            ScanOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            FullScanCount++;
            await Task.Yield();
            var root = createRoot();
            LastRoot = root;
            yield return new ScanProgress(
                rootPath,
                FilesScanned: 5,
                DirectoriesScanned: 6,
                BytesScanned: root.SizeBytes,
                root,
                Errors: [],
                IsCompleted: true,
                options?.MeasurementMode ?? StorageMeasurementMode.Logical);
        }

        public async IAsyncEnumerable<ScanProgress> ScanSubtreeAsync(
            string scanRootPath,
            string subtreePath,
            ScanOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            SubtreeScanCount++;
            await Task.Yield();
            var replacement = SubtreeReplacement?.Invoke();
            var placeholder = new DiskItem(Path.GetFileName(subtreePath), subtreePath, isDirectory: true);
            yield return new ScanProgress(
                subtreePath,
                FilesScanned: 1,
                DirectoriesScanned: 1,
                BytesScanned: replacement?.SizeBytes ?? 0,
                replacement ?? placeholder,
                Errors: [],
                IsCompleted: true,
                options?.MeasurementMode ?? StorageMeasurementMode.Logical)
            {
                IsRootPresent = replacement is not null
            };
        }
    }

    private sealed class SnapshotReader(ScopeScanner scanner) : ICleanupFileSystemMetadataReader
    {
        public int ReadCount { get; private set; }

        public bool TryReadSnapshot(string path, out CleanupFileSystemSnapshot snapshot)
        {
            ReadCount++;
            if (scanner.LastRoot is { } root && ScanResultMerger.FindByPath(root, path) is { } item)
            {
                snapshot = new CleanupFileSystemSnapshot(
                    item.Path,
                    item.IsDirectory,
                    item.SizeBytes,
                    item.MeasuredSizeBytes);
                return true;
            }

            snapshot = null!;
            return false;
        }
    }

    private sealed class RecordingHistoryStore : IScanHistoryStore
    {
        public string Location => "/tmp/history";

        public List<int> RowCounts { get; } = [];

        public Task<ScanHistoryCaptureResult> CaptureAsync(
            ScanSnapshotRequest request,
            ScanHistoryLimits limits,
            CancellationToken cancellationToken = default)
        {
            RowCounts.Add(request.Rows.Count());
            return Task.FromResult(ScanHistoryCaptureResult.Captured(
                new ScanSnapshotDescriptor(request.Metadata, 1024),
                []));
        }

        public Task<IReadOnlyList<ScanHistoryEntry>> ListAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ScanHistoryEntry>>([]);

        public Task<long> GetTotalSizeBytesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0L);

        public Task<ScanSnapshotReadResult<ScanSnapshotDocument>> ReadAsync(
            string snapshotId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                ScanSnapshotReadResult<ScanSnapshotDocument>.Unreadable("none"));

        public Task<bool> DeleteAsync(
            string snapshotId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task ClearAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<ScanSnapshotDescriptor>> ApplyLimitsAsync(
            ScanHistoryLimits limits,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ScanSnapshotDescriptor>>([]);
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
