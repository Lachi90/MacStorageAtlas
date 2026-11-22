using System.Runtime.CompilerServices;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core.Cleanup;
using MacStorageAtlas.Core.Insights;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Platform;
using MacStorageAtlas.Core.Scanning;
using NSubstitute;

namespace MacStorageAtlas.App.Tests;

public class MainWindowViewModelItemContextMenuTests
{
    private const string RootPath = "/scan/root";
    private const string FilePath = "/scan/root/movie.mov";
    private const string FolderPath = "/scan/root/Projects";

    public enum SelectionSource
    {
        Tree,
        Treemap,
        LargeFile,
        DuplicateEntry
    }

    [TestCase(SelectionSource.Tree)]
    [TestCase(SelectionSource.Treemap)]
    [TestCase(SelectionSource.LargeFile)]
    [TestCase(SelectionSource.DuplicateEntry)]
    public async Task CopySelectedItemPathCopiesThePathOfTheItemSelectedThroughEachSource(
        SelectionSource source)
    {
        var context = await ScannedContextAsync();

        Select(context, source, FilePath);
        await context.ViewModel.CopySelectedItemPathCommand.ExecuteAsync(null);

        await context.Clipboard.Received(1).SetTextAsync(FilePath);
    }

    [Test]
    public async Task CopySelectedItemPathCopiesADirectoryPathSelectedInTheTreemap()
    {
        var context = await ScannedContextAsync();

        Select(context, SelectionSource.Treemap, FolderPath);
        await context.ViewModel.CopySelectedItemPathCommand.ExecuteAsync(null);

        await context.Clipboard.Received(1).SetTextAsync(FolderPath);
    }

    [Test]
    public async Task CopySelectedItemPathIsDisabledWithoutASelectedItem()
    {
        var context = await ScannedContextAsync();

        Assert.That(context.ViewModel.CopySelectedItemPathCommand.CanExecute(null), Is.False);

        await context.ViewModel.CopySelectedItemPathCommand.ExecuteAsync(null);

        await context.Clipboard.DidNotReceive().SetTextAsync(Arg.Any<string>());
    }

    [Test]
    public async Task CopySelectedItemPathBecomesEnabledWhenAnItemIsSelected()
    {
        var context = await ScannedContextAsync();
        var changes = 0;
        context.ViewModel.CopySelectedItemPathCommand.CanExecuteChanged += (_, _) => changes++;

        Select(context, SelectionSource.LargeFile, FilePath);

        Assert.Multiple(() =>
        {
            Assert.That(changes, Is.GreaterThan(0));
            Assert.That(context.ViewModel.CopySelectedItemPathCommand.CanExecute(null), Is.True);
        });
    }

    [Test]
    public async Task CopySelectedItemPathIsDisabledWhileAnUpdateRestartIsPending()
    {
        var context = await ScannedContextAsync();
        Select(context, SelectionSource.Tree, FilePath);
        var changes = 0;
        context.ViewModel.CopySelectedItemPathCommand.CanExecuteChanged += (_, _) => changes++;

        var prepared = await context.ViewModel.PrepareForUpdateRestartAsync();
        await context.ViewModel.CopySelectedItemPathCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(prepared, Is.True);
            Assert.That(changes, Is.GreaterThan(0));
            Assert.That(context.ViewModel.CopySelectedItemPathCommand.CanExecute(null), Is.False);
        });
        await context.Clipboard.DidNotReceive().SetTextAsync(Arg.Any<string>());
    }

    [TestCase(SelectionSource.Tree)]
    [TestCase(SelectionSource.Treemap)]
    [TestCase(SelectionSource.LargeFile)]
    [TestCase(SelectionSource.DuplicateEntry)]
    public async Task SelectingAnItemOutsideTheBasketEnablesTheToolbarCommandsForIt(
        SelectionSource source)
    {
        var context = await ScannedContextAsync();

        Select(context, source, FilePath);

        var viewModel = context.ViewModel;
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedItem?.Path, Is.EqualTo(FilePath));
            Assert.That(viewModel.QuickLookCommand.CanExecute(null), Is.True);
            Assert.That(viewModel.ShowSelectedItemDetailsCommand.CanExecute(null), Is.True);
            Assert.That(viewModel.RevealInFinderCommand.CanExecute(null), Is.True);
            Assert.That(viewModel.CopySelectedItemPathCommand.CanExecute(null), Is.True);
            Assert.That(viewModel.AddSelectedItemToCleanupBasketCommand.CanExecute(null), Is.True);
            Assert.That(viewModel.RemoveSelectedItemFromCleanupBasketCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.MoveToTrashCommand.CanExecute(null), Is.True);
        });
    }

    [TestCase(SelectionSource.Tree)]
    [TestCase(SelectionSource.Treemap)]
    [TestCase(SelectionSource.LargeFile)]
    [TestCase(SelectionSource.DuplicateEntry)]
    public async Task SelectingAnItemInTheBasketEnablesRemoveFromBasket(SelectionSource source)
    {
        var context = await ScannedContextAsync();
        Select(context, SelectionSource.LargeFile, FilePath);
        context.ViewModel.AddSelectedItemToCleanupBasketCommand.Execute(null);
        context.ViewModel.SelectedLargeFile = null;

        Select(context, source, FilePath);

        var viewModel = context.ViewModel;
        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CleanupBasketItems.Single().Item.Path, Is.EqualTo(FilePath));
            Assert.That(viewModel.RemoveSelectedItemFromCleanupBasketCommand.CanExecute(null), Is.True);
            Assert.That(viewModel.AddSelectedItemToCleanupBasketCommand.CanExecute(null), Is.True);
        });
    }

    [Test]
    public async Task AddingAnItemAlreadyInTheBasketReportsItWithoutAddingItAgain()
    {
        var context = await ScannedContextAsync();
        Select(context, SelectionSource.Tree, FilePath);
        context.ViewModel.AddSelectedItemToCleanupBasketCommand.Execute(null);

        context.ViewModel.AddSelectedItemToCleanupBasketCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.CleanupBasketItems, Has.Count.EqualTo(1));
            Assert.That(
                context.ViewModel.CleanupBasketStatusMessage,
                Is.EqualTo("The item is already in the cleanup basket."));
        });
    }

    [Test]
    public async Task MoveToTrashForTheScanRootReportsProtectionWithoutConfirmationOrTrash()
    {
        var context = await ScannedContextAsync();
        context.ViewModel.SelectedTreeItem = context.ViewModel.TreeItems.Single();

        Assert.That(context.ViewModel.MoveToTrashCommand.CanExecute(null), Is.True);

        await context.ViewModel.MoveToTrashCommand.ExecuteAsync(null);

        Assert.That(
            context.ViewModel.TrashStatusMessage,
            Is.EqualTo("The scan root is protected from cleanup."));
        await context.Confirmation.DidNotReceive().ConfirmMoveToTrashAsync(Arg.Any<DiskItem>());
        await context.Trash.DidNotReceive().MoveToTrashAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task MoveToTrashForAnEligibleLargeFileAsksForConfirmationBeforeTrashing()
    {
        var context = await ScannedContextAsync();
        context.Confirmation.ConfirmMoveToTrashAsync(Arg.Any<DiskItem>()).Returns(true);
        Select(context, SelectionSource.LargeFile, FilePath);

        await context.ViewModel.MoveToTrashCommand.ExecuteAsync(null);

        Received.InOrder(() =>
        {
            context.Confirmation.ConfirmMoveToTrashAsync(
                Arg.Is<DiskItem>(item => item.Path == FilePath));
            context.Trash.MoveToTrashAsync(FilePath, Arg.Any<CancellationToken>());
        });
    }

    [Test]
    public async Task DecliningTrashConfirmationLeavesTheItemAndResultInPlace()
    {
        var context = await ScannedContextAsync();
        context.Confirmation.ConfirmMoveToTrashAsync(Arg.Any<DiskItem>()).Returns(false);
        Select(context, SelectionSource.LargeFile, FilePath);
        var largeFiles = context.ViewModel.LargeFiles;
        var treeItems = context.ViewModel.TreeItems;

        await context.ViewModel.MoveToTrashCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.LargeFiles, Is.SameAs(largeFiles));
            Assert.That(context.ViewModel.TreeItems, Is.SameAs(treeItems));
            Assert.That(context.ViewModel.SelectedItem?.Path, Is.EqualTo(FilePath));
            Assert.That(context.Root.Children.Select(child => child.Path), Does.Contain(FilePath));
        });
        await context.Trash.DidNotReceive().MoveToTrashAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [TestCase(SelectionSource.Tree)]
    [TestCase(SelectionSource.Treemap)]
    [TestCase(SelectionSource.LargeFile)]
    [TestCase(SelectionSource.DuplicateEntry)]
    public async Task StartingAScanClearsTheSelectionAndDisablesEverySelectedItemCommand(
        SelectionSource source)
    {
        var context = await ScannedContextAsync();
        Select(context, source, FilePath);
        var viewModel = context.ViewModel;

        context.Scanner.BlockNextScan = true;
        var scan = viewModel.ScanFolderCommand.ExecuteAsync(null);
        await context.Scanner.BlockedScanStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(viewModel.SelectedItem, Is.Null);
                Assert.That(viewModel.SelectedTreeItem, Is.Null);
                Assert.That(viewModel.SelectedTreemapRectangle, Is.Null);
                Assert.That(viewModel.SelectedLargeFile, Is.Null);
                Assert.That(viewModel.SelectedDuplicateEntry, Is.Null);
                Assert.That(viewModel.QuickLookCommand.CanExecute(null), Is.False);
                Assert.That(viewModel.ShowSelectedItemDetailsCommand.CanExecute(null), Is.False);
                Assert.That(viewModel.RevealInFinderCommand.CanExecute(null), Is.False);
                Assert.That(viewModel.CopySelectedItemPathCommand.CanExecute(null), Is.False);
                Assert.That(viewModel.AddSelectedItemToCleanupBasketCommand.CanExecute(null), Is.False);
                Assert.That(viewModel.RemoveSelectedItemFromCleanupBasketCommand.CanExecute(null), Is.False);
                Assert.That(viewModel.MoveToTrashCommand.CanExecute(null), Is.False);
            });
        }
        finally
        {
            viewModel.StopScanCommand.Execute(null);
            await scan;
        }

        await context.Clipboard.DidNotReceive().SetTextAsync(Arg.Any<string>());
    }

    private static void Select(ScannedContext context, SelectionSource source, string path)
    {
        var viewModel = context.ViewModel;
        var item = Find(context.Root, path);

        switch (source)
        {
            case SelectionSource.Tree:
                viewModel.SelectedTreeItem = viewModel.TreeItems
                    .Single()
                    .Children
                    .Single(node => ReferenceEquals(node.Item, item));
                break;
            case SelectionSource.Treemap:
                viewModel.SelectedTreemapRectangle = viewModel.TreemapRectangles
                    .Single(rectangle => ReferenceEquals(rectangle.Item.Item, item));
                break;
            case SelectionSource.LargeFile:
                viewModel.SelectedLargeFile = viewModel.LargeFiles
                    .Single(file => ReferenceEquals(file, item));
                break;
            case SelectionSource.DuplicateEntry:
                viewModel.SelectedDuplicateEntry = new DuplicateGroupEntry(
                    item,
                    item.SizeBytes,
                    DuplicateGroupEntryKind.ReclaimableCopy);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(source), source, null);
        }
    }

    private static DiskItem Find(DiskItem root, string path) =>
        root.Path == path
            ? root
            : root.Children.Single(child => child.Path == path);

    private static async Task<ScannedContext> ScannedContextAsync()
    {
        var root = CreateTree();
        var scanner = new FirstScanCompletesScanner(root);
        var clipboard = Substitute.For<IClipboardService>();
        var trash = Substitute.For<ITrashService>();
        var confirmation = Substitute.For<ITrashConfirmationService>();
        var viewModel = new MainWindowViewModel(
            Substitute.For<IFolderPickerService>(),
            scanner,
            new ImmediateDispatcher(),
            Substitute.For<IFileRevealService>(),
            trash,
            confirmation,
            clipboardService: clipboard,
            quickLookService: Substitute.For<IQuickLookService>())
        {
            SelectedFolderPath = RootPath
        };

        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        return new ScannedContext(viewModel, root, scanner, clipboard, trash, confirmation);
    }

    private static DiskItem CreateTree()
    {
        var root = new DiskItem("root", RootPath, isDirectory: true)
        {
            SizeBytes = 6144,
            MeasuredSizeBytes = 6144
        };
        root.AddChild(new DiskItem("movie.mov", FilePath, isDirectory: false)
        {
            SizeBytes = 4096,
            MeasuredSizeBytes = 4096
        });
        root.AddChild(new DiskItem("Projects", FolderPath, isDirectory: true)
        {
            SizeBytes = 2048,
            MeasuredSizeBytes = 2048
        });
        return root;
    }

    private sealed record ScannedContext(
        MainWindowViewModel ViewModel,
        DiskItem Root,
        FirstScanCompletesScanner Scanner,
        IClipboardService Clipboard,
        ITrashService Trash,
        ITrashConfirmationService Confirmation);

    private sealed class FirstScanCompletesScanner(DiskItem root) : IDiskScanner
    {
        public bool BlockNextScan { get; set; }

        public TaskCompletionSource BlockedScanStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async IAsyncEnumerable<ScanProgress> ScanAsync(
            string rootPath,
            ScanOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            if (BlockNextScan)
            {
                BlockNextScan = false;
                BlockedScanStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }

            yield return new ScanProgress(
                root.Path,
                FilesScanned: 1,
                DirectoriesScanned: 2,
                BytesScanned: root.SizeBytes,
                root,
                Errors: [],
                IsCompleted: true,
                options?.MeasurementMode ?? StorageMeasurementMode.Logical);
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
