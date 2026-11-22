using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Input;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.App.Views;
using MacStorageAtlas.Core.Cleanup;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Platform;
using MacStorageAtlas.Core.Scanning;
using NSubstitute;

namespace MacStorageAtlas.App.Tests;

public class MainWindowMenuCommandTests
{
    private const string RootPath = "/scan/root";
    private const string OtherRootPath = "/scan/other";
    private const string FilePath = "/scan/root/movie.mov";

    [Test]
    public void ScanCommandLabelReadsScanBeforeAResultIsDisplayed()
    {
        var viewModel = CreateContext().ViewModel;
        viewModel.SelectedFolderPath = RootPath;

        Assert.That(viewModel.ScanCommandLabel, Is.EqualTo("Scan"));
    }

    [Test]
    public async Task ScanCommandLabelReadsRescanOnceTheSelectedFolderHasAResult()
    {
        var context = CreateContext();
        var changes = TrackChanges(context.ViewModel, nameof(MainWindowViewModel.ScanCommandLabel));

        await ScanAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.ScanCommandLabel, Is.EqualTo("Rescan"));
            Assert.That(changes(), Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task ScanCommandLabelReadsScanAfterAnotherFolderIsSelected()
    {
        var context = CreateContext();
        await ScanAsync(context);
        var changes = TrackChanges(context.ViewModel, nameof(MainWindowViewModel.ScanCommandLabel));

        context.ViewModel.SelectedFolderPath = OtherRootPath;

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.ScanCommandLabel, Is.EqualTo("Scan"));
            Assert.That(changes(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ScanCommandLabelIgnoresATrailingSeparatorOnTheSelectedFolder()
    {
        var context = CreateContext();
        await ScanAsync(context);

        context.ViewModel.SelectedFolderPath = RootPath + "/";

        Assert.That(context.ViewModel.ScanCommandLabel, Is.EqualTo("Rescan"));
    }

    [Test]
    public async Task ScanIsDisabledAndStopEnabledWhileAScanRuns()
    {
        var context = CreateContext();
        context.Scanner.BlockNextScan = true;
        context.ViewModel.SelectedFolderPath = RootPath;

        var scan = context.ViewModel.ScanFolderCommand.ExecuteAsync(null);
        await context.Scanner.BlockedScanStarted.Task;

        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(context.ViewModel.RescanCommand.CanExecute(null), Is.False);
                Assert.That(context.ViewModel.StopScanCommand.CanExecute(null), Is.True);
            });
        }
        finally
        {
            context.ViewModel.StopScanCommand.Execute(null);
            await scan;
        }

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.RescanCommand.CanExecute(null), Is.True);
            Assert.That(context.ViewModel.StopScanCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public void ExportCommandsAreUnavailableWithoutAResult()
    {
        var viewModel = CreateContext().ViewModel;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ExportCsvCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.ExportJsonCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public async Task CleanupBasketCommandLabelNamesTheActionForTheSelectedItem()
    {
        var context = await ScannedContextAsync();
        SelectFile(context);
        var outside = context.ViewModel.CleanupBasketCommandLabel;
        var changes = TrackChanges(
            context.ViewModel,
            nameof(MainWindowViewModel.CleanupBasketCommandLabel));

        context.ViewModel.AddSelectedItemToCleanupBasketCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(outside, Is.EqualTo("Add to Cleanup Basket"));
            Assert.That(
                context.ViewModel.CleanupBasketCommandLabel,
                Is.EqualTo("Remove from Cleanup Basket"));
            Assert.That(changes(), Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task CleanupBasketCommandLabelFollowsTheSelection()
    {
        var context = await ScannedContextAsync();
        SelectFile(context);
        context.ViewModel.AddSelectedItemToCleanupBasketCommand.Execute(null);
        var changes = TrackChanges(
            context.ViewModel,
            nameof(MainWindowViewModel.CleanupBasketCommandLabel));

        context.ViewModel.SelectedLargeFile = null;

        Assert.Multiple(() =>
        {
            Assert.That(
                context.ViewModel.CleanupBasketCommandLabel,
                Is.EqualTo("Add to Cleanup Basket"));
            Assert.That(changes(), Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task ToggleAddsAnItemOutsideTheBasketAndRemovesItAgain()
    {
        var context = await ScannedContextAsync();
        SelectFile(context);
        var toggle = context.ViewModel.ToggleSelectedItemInCleanupBasketCommand;

        toggle.Execute(null);
        var addedPaths = context.ViewModel.CleanupBasketItems.Select(item => item.Item.Path).ToArray();
        toggle.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(addedPaths, Is.EqualTo(new[] { FilePath }));
            Assert.That(context.ViewModel.CleanupBasketItems, Is.Empty);
            Assert.That(
                context.ViewModel.CleanupBasketStatusMessage,
                Is.EqualTo("Removed from the cleanup basket."));
        });
    }

    [Test]
    public async Task ToggleIsEnabledExactlyWhenTheMatchingBasketCommandIs()
    {
        var context = await ScannedContextAsync();
        var viewModel = context.ViewModel;
        var withoutSelection = viewModel.ToggleSelectedItemInCleanupBasketCommand.CanExecute(null);
        var changes = 0;
        viewModel.ToggleSelectedItemInCleanupBasketCommand.CanExecuteChanged += (_, _) => changes++;

        SelectFile(context);
        var outside = (
            Toggle: viewModel.ToggleSelectedItemInCleanupBasketCommand.CanExecute(null),
            Add: viewModel.AddSelectedItemToCleanupBasketCommand.CanExecute(null));
        viewModel.AddSelectedItemToCleanupBasketCommand.Execute(null);
        var inside = (
            Toggle: viewModel.ToggleSelectedItemInCleanupBasketCommand.CanExecute(null),
            Remove: viewModel.RemoveSelectedItemFromCleanupBasketCommand.CanExecute(null));

        Assert.Multiple(() =>
        {
            Assert.That(withoutSelection, Is.False);
            Assert.That(changes, Is.GreaterThan(0));
            Assert.That(outside.Toggle, Is.EqualTo(outside.Add).And.True);
            Assert.That(inside.Toggle, Is.EqualTo(inside.Remove).And.True);
        });
    }

    [Test]
    public void ToggleIsDisabledBeforeAScanResultExists()
    {
        var viewModel = CreateContext().ViewModel;
        viewModel.SelectedLargeFile = new DiskItem("movie.mov", FilePath, isDirectory: false);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ToggleSelectedItemInCleanupBasketCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.AddSelectedItemToCleanupBasketCommand.CanExecute(null), Is.False);
        });
    }

    [TestCase(StorageMeasurementMode.Logical)]
    [TestCase(StorageMeasurementMode.Allocated)]
    [TestCase(StorageMeasurementMode.SharedAwareAllocated)]
    public void SelectMeasurementModeChoosesTheModeForTheNextScan(StorageMeasurementMode mode)
    {
        var viewModel = CreateContext().ViewModel;

        viewModel.SelectMeasurementModeCommand.Execute(mode);

        Assert.That(viewModel.MeasurementMode, Is.EqualTo(mode));
    }

    [Test]
    public void ScanOptionToggleCommandsFlipTheirOption()
    {
        var viewModel = CreateContext().ViewModel;
        var before = (
            viewModel.ExpandApplicationBundles,
            viewModel.IncludeHiddenFiles,
            viewModel.FollowSymbolicLinks);

        viewModel.ToggleExpandApplicationBundlesCommand.Execute(null);
        viewModel.ToggleIncludeHiddenFilesCommand.Execute(null);
        viewModel.ToggleFollowSymbolicLinksCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ExpandApplicationBundles, Is.EqualTo(!before.ExpandApplicationBundles));
            Assert.That(viewModel.IncludeHiddenFiles, Is.EqualTo(!before.IncludeHiddenFiles));
            Assert.That(viewModel.FollowSymbolicLinks, Is.EqualTo(!before.FollowSymbolicLinks));
        });
    }

    [Test]
    public async Task CommandBTogglesTheSelectedItemInTheBasket()
    {
        var context = await ScannedContextAsync();
        SelectFile(context);
        var keyEvent = KeyDown(Key.B, KeyModifiers.Meta, new Border());

        var handled = MainWindow.TryHandleSelectedItemShortcut(context.ViewModel, keyEvent);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(keyEvent.Handled, Is.True);
            Assert.That(
                context.ViewModel.CleanupBasketItems.Select(item => item.Item.Path),
                Is.EqualTo(new[] { FilePath }));
        });
    }

    [Test]
    public async Task CommandBIsIgnoredWhileATextFieldHasFocus()
    {
        var context = await ScannedContextAsync();
        SelectFile(context);
        var keyEvent = KeyDown(Key.B, KeyModifiers.Meta, new TextBox());

        var handled = MainWindow.TryHandleSelectedItemShortcut(context.ViewModel, keyEvent);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.False);
            Assert.That(keyEvent.Handled, Is.False);
            Assert.That(context.ViewModel.CleanupBasketItems, Is.Empty);
        });
    }

    [Test]
    public async Task CommandBIsIgnoredWithoutASelectedItem()
    {
        var context = await ScannedContextAsync();
        var keyEvent = KeyDown(Key.B, KeyModifiers.Meta, new Border());

        var handled = MainWindow.TryHandleSelectedItemShortcut(context.ViewModel, keyEvent);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.False);
            Assert.That(keyEvent.Handled, Is.False);
        });
    }

    [TestCase(Key.B, KeyModifiers.Meta | KeyModifiers.Shift)]
    [TestCase(Key.B, KeyModifiers.None)]
    [TestCase(Key.Back, KeyModifiers.None)]
    [TestCase(Key.Back, KeyModifiers.Meta | KeyModifiers.Alt)]
    public async Task OtherModifierCombinationsLeaveTheItemAlone(Key key, KeyModifiers modifiers)
    {
        var context = await ScannedContextAsync();
        SelectFile(context);
        var keyEvent = KeyDown(key, modifiers, new Border());

        var handled = MainWindow.TryHandleSelectedItemShortcut(context.ViewModel, keyEvent);

        Assert.That(handled, Is.False);
        Assert.That(context.ViewModel.CleanupBasketItems, Is.Empty);
        await context.Confirmation.DidNotReceive().ConfirmMoveToTrashAsync(Arg.Any<DiskItem>());
    }

    [Test]
    public async Task CommandDeleteAsksBeforeMovingTheSelectedItemToTrash()
    {
        var context = await ScannedContextAsync();
        SelectFile(context);
        var keyEvent = KeyDown(Key.Back, KeyModifiers.Meta, new Border());

        var handled = MainWindow.TryHandleSelectedItemShortcut(context.ViewModel, keyEvent);

        Assert.That(handled, Is.True);
        await context.Confirmation.Received(1).ConfirmMoveToTrashAsync(
            Arg.Is<DiskItem>(item => item.Path == FilePath));
        await context.Trash.DidNotReceive().MoveToTrashAsync(Arg.Any<string>());
    }

    [Test]
    public async Task CommandDeleteInATextFieldLeavesTheSelectedItemAlone()
    {
        var context = await ScannedContextAsync();
        SelectFile(context);
        var keyEvent = KeyDown(Key.Back, KeyModifiers.Meta, new TextBox { Name = "SearchBox" });

        var handled = MainWindow.TryHandleSelectedItemShortcut(context.ViewModel, keyEvent);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.False);
            Assert.That(keyEvent.Handled, Is.False);
        });
        await context.Confirmation.DidNotReceive().ConfirmMoveToTrashAsync(Arg.Any<DiskItem>());
    }

    [Test]
    public async Task CommandDeleteRefusesAProtectedItem()
    {
        var context = await ScannedContextAsync();
        context.ViewModel.SelectedTreeItem = context.ViewModel.TreeItems.Single();
        var keyEvent = KeyDown(Key.Back, KeyModifiers.Meta, new Border());

        MainWindow.TryHandleSelectedItemShortcut(context.ViewModel, keyEvent);

        Assert.That(context.ViewModel.TrashStatusMessage, Is.Not.Null.And.Not.Empty);
        await context.Confirmation.DidNotReceive().ConfirmMoveToTrashAsync(Arg.Any<DiskItem>());
        await context.Trash.DidNotReceive().MoveToTrashAsync(Arg.Any<string>());
    }

    [Test]
    public void CommandDeleteIsIgnoredWithoutASelectedItem()
    {
        var context = CreateContext();
        var keyEvent = KeyDown(Key.Back, KeyModifiers.Meta, new Border());

        var handled = MainWindow.TryHandleSelectedItemShortcut(context.ViewModel, keyEvent);

        Assert.That(handled, Is.False);
    }

    private static Func<int> TrackChanges(MainWindowViewModel viewModel, string propertyName)
    {
        var changes = 0;
        viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == propertyName)
            {
                changes++;
            }
        };
        return () => changes;
    }

    private static void SelectFile(MenuContext context) =>
        context.ViewModel.SelectedLargeFile = context.ViewModel.LargeFiles
            .Single(file => file.Path == FilePath);

    private static KeyEventArgs KeyDown(Key key, KeyModifiers modifiers, Control source) =>
        new()
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = modifiers,
            Source = source
        };

    private static async Task<MenuContext> ScannedContextAsync()
    {
        var context = CreateContext();
        await ScanAsync(context);
        return context;
    }

    private static async Task ScanAsync(MenuContext context)
    {
        context.ViewModel.SelectedFolderPath = RootPath;
        await context.ViewModel.ScanFolderCommand.ExecuteAsync(null);
    }

    private static MenuContext CreateContext()
    {
        var scanner = new ControllableScanner(CreateTree());
        var trash = Substitute.For<ITrashService>();
        var confirmation = Substitute.For<ITrashConfirmationService>();
        var viewModel = new MainWindowViewModel(
            Substitute.For<IFolderPickerService>(),
            scanner,
            new ImmediateDispatcher(),
            Substitute.For<IFileRevealService>(),
            trash,
            confirmation,
            clipboardService: Substitute.For<IClipboardService>(),
            quickLookService: Substitute.For<IQuickLookService>());

        return new MenuContext(viewModel, scanner, trash, confirmation);
    }

    private static DiskItem CreateTree()
    {
        var root = new DiskItem("root", RootPath, isDirectory: true)
        {
            SizeBytes = 4096,
            MeasuredSizeBytes = 4096
        };
        root.AddChild(new DiskItem("movie.mov", FilePath, isDirectory: false)
        {
            SizeBytes = 4096,
            MeasuredSizeBytes = 4096
        });
        return root;
    }

    private sealed record MenuContext(
        MainWindowViewModel ViewModel,
        ControllableScanner Scanner,
        ITrashService Trash,
        ITrashConfirmationService Confirmation);

    private sealed class ControllableScanner(DiskItem root) : IDiskScanner
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
                DirectoriesScanned: 1,
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
