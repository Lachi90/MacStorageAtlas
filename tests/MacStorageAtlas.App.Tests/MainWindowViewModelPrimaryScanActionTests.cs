using System.Runtime.CompilerServices;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core.Cleanup;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Platform;
using MacStorageAtlas.Core.Scanning;
using NSubstitute;

namespace MacStorageAtlas.App.Tests;

public class MainWindowViewModelPrimaryScanActionTests
{
    private const string RootPath = "/scan/root";
    private const string ProjectPath = "/scan/root/project";
    private const string MoviePath = "/scan/root/movie.mov";
    private const string FirstProjectFilePath = "/scan/root/project/a.bin";
    private const string SecondProjectFilePath = "/scan/root/project/b.bin";

    [Test]
    public void ThePrimaryScanActionReadsScanBeforeAResultIsDisplayed()
    {
        var context = CreateContext();

        context.ViewModel.SelectedFolderPath = RootPath;

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.PrimaryScanActionLabel, Is.EqualTo("Scan"));
            Assert.That(context.ViewModel.PrimaryScanActionToolTip, Is.EqualTo(MainWindowViewModel.ScanToolTip));
            Assert.That(context.ViewModel.IsPrimaryScanActionScan, Is.True);
            Assert.That(context.ViewModel.IsPrimaryScanActionRescan, Is.False);
            Assert.That(context.ViewModel.IsPrimaryScanActionStop, Is.False);
        });
    }

    [Test]
    public async Task ThePrimaryScanActionReadsRescanOnceTheSelectedFolderHasAResult()
    {
        var context = CreateContext();
        var changes = TrackChanges(context.ViewModel, nameof(MainWindowViewModel.PrimaryScanActionLabel));

        await CompleteScanAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.PrimaryScanActionLabel, Is.EqualTo("Rescan"));
            Assert.That(context.ViewModel.PrimaryScanActionToolTip, Is.EqualTo(MainWindowViewModel.RescanToolTip));
            Assert.That(context.ViewModel.IsPrimaryScanActionRescan, Is.True);
            Assert.That(context.ViewModel.IsPrimaryScanActionScan, Is.False);
            Assert.That(changes(), Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task ThePrimaryScanActionReadsStopWhileAScanRuns()
    {
        var context = CreateContext();
        context.ViewModel.SelectedFolderPath = RootPath;
        context.Scanner.BlockScans = true;
        var changes = TrackChanges(context.ViewModel, nameof(MainWindowViewModel.PrimaryScanActionLabel));

        context.ViewModel.PrimaryScanActionCommand.Execute(null);
        await context.Scanner.ScanStarted.Task;

        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(context.ViewModel.PrimaryScanActionLabel, Is.EqualTo("Stop"));
                Assert.That(context.ViewModel.PrimaryScanActionToolTip, Is.EqualTo(MainWindowViewModel.StopToolTip));
                Assert.That(context.ViewModel.IsPrimaryScanActionStop, Is.True);
                Assert.That(context.ViewModel.IsPrimaryScanActionScan, Is.False);
                Assert.That(context.ViewModel.IsPrimaryScanActionRescan, Is.False);
                Assert.That(changes(), Is.GreaterThan(0));
            });
        }
        finally
        {
            await StopAsync(context);
        }
    }

    [Test]
    public async Task ActivatingTheScanControlStartsAScanOfTheSelectedFolder()
    {
        var context = CreateContext();
        context.ViewModel.SelectedFolderPath = RootPath;

        context.ViewModel.PrimaryScanActionCommand.Execute(null);
        await context.ViewModel.ScanFolderCommand.ExecutionTask!;

        Assert.Multiple(() =>
        {
            Assert.That(context.Scanner.ScannedRoots, Is.EqualTo(new[] { RootPath }));
            Assert.That(context.ViewModel.ScanCompletedAt, Is.Not.Null);
        });
    }

    [Test]
    public async Task ActivatingTheStopControlStopsTheRunningScan()
    {
        var context = CreateContext();
        context.ViewModel.SelectedFolderPath = RootPath;
        context.Scanner.BlockScans = true;
        context.ViewModel.PrimaryScanActionCommand.Execute(null);
        await context.Scanner.ScanStarted.Task;
        context.Time.Advance(MainWindowViewModel.PrimaryScanActionTransitionInterval);

        context.ViewModel.PrimaryScanActionCommand.Execute(null);
        await context.ViewModel.ScanFolderCommand.ExecutionTask!;

        Assert.Multiple(() =>
        {
            Assert.That(context.Scanner.CancelledScans, Is.EqualTo(1));
            Assert.That(context.ViewModel.IsScanning, Is.False);
            Assert.That(context.ViewModel.ScanCompletedAt, Is.Null);
            Assert.That(context.ViewModel.PrimaryScanActionLabel, Is.EqualTo("Scan"));
        });
    }

    [Test]
    public async Task ADoubleActivationStartsExactlyOneScanAndDoesNotStopIt()
    {
        var context = CreateContext();
        context.ViewModel.SelectedFolderPath = RootPath;
        context.Scanner.BlockScans = true;

        context.ViewModel.PrimaryScanActionCommand.Execute(null);
        context.ViewModel.PrimaryScanActionCommand.Execute(null);
        await context.Scanner.ScanStarted.Task;
        context.Time.Advance(TimeSpan.FromMilliseconds(100));
        context.ViewModel.PrimaryScanActionCommand.Execute(null);

        try
        {
            Assert.Multiple(() =>
            {
                Assert.That(context.Scanner.ScannedRoots, Is.EqualTo(new[] { RootPath }));
                Assert.That(context.Scanner.CancelledScans, Is.Zero);
                Assert.That(context.ViewModel.IsScanning, Is.True);
            });
        }
        finally
        {
            await StopAsync(context);
        }
    }

    [Test]
    public async Task ThePrimaryScanActionStopsAFolderRescan()
    {
        var context = CreateContext();
        await CompleteScanAsync(context);
        context.ViewModel.SelectedTreeItem = context.ViewModel.TreeItems
            .Single()
            .Children
            .Single(node => node.Item.Path == ProjectPath);
        context.Scanner.BlockScans = true;
        var rescan = context.ViewModel.RescanFolderCommand.ExecuteAsync(null);
        await context.Scanner.ScanStarted.Task;
        var label = context.ViewModel.PrimaryScanActionLabel;
        var enabled = context.ViewModel.PrimaryScanActionCommand.CanExecute(null);
        context.Time.Advance(MainWindowViewModel.PrimaryScanActionTransitionInterval);

        context.ViewModel.PrimaryScanActionCommand.Execute(null);
        await rescan;

        Assert.Multiple(() =>
        {
            Assert.That(label, Is.EqualTo("Stop"));
            Assert.That(enabled, Is.True);
            Assert.That(context.Scanner.SubtreeRequests, Is.EqualTo(new[] { ProjectPath }));
            Assert.That(context.Scanner.CancelledScans, Is.EqualTo(1));
            Assert.That(context.ViewModel.IsRescanningFolder, Is.False);
            Assert.That(context.ViewModel.FolderRescanStatusMessage, Does.Contain("was stopped"));
            Assert.That(context.ViewModel.PrimaryScanActionLabel, Is.EqualTo("Rescan"));
        });
    }

    [Test]
    public async Task ThePrimaryScanActionIsEnabledExactlyWhenTheCommandItShowsIs()
    {
        var context = CreateContext();
        var viewModel = context.ViewModel;
        var changes = 0;
        viewModel.PrimaryScanActionCommand.CanExecuteChanged += (_, _) => changes++;
        var withoutFolder = (
            Primary: viewModel.PrimaryScanActionCommand.CanExecute(null),
            Scan: viewModel.ScanFolderCommand.CanExecute(null));

        viewModel.SelectedFolderPath = RootPath;
        var withFolder = (
            Primary: viewModel.PrimaryScanActionCommand.CanExecute(null),
            Scan: viewModel.ScanFolderCommand.CanExecute(null));
        context.Scanner.BlockScans = true;
        viewModel.PrimaryScanActionCommand.Execute(null);
        await context.Scanner.ScanStarted.Task;
        var whileScanning = (
            Primary: viewModel.PrimaryScanActionCommand.CanExecute(null),
            Stop: viewModel.StopScanCommand.CanExecute(null));
        await StopAsync(context);

        Assert.Multiple(() =>
        {
            Assert.That(withoutFolder.Primary, Is.EqualTo(withoutFolder.Scan).And.False);
            Assert.That(withFolder.Primary, Is.EqualTo(withFolder.Scan).And.True);
            Assert.That(whileScanning.Primary, Is.EqualTo(whileScanning.Stop).And.True);
            Assert.That(viewModel.PrimaryScanActionCommand.CanExecute(null), Is.True);
            Assert.That(changes, Is.GreaterThan(0));
        });
    }

    [Test]
    public void ThePrimaryScanActionDoesNothingWithoutASelectedFolder()
    {
        var context = CreateContext();

        context.ViewModel.PrimaryScanActionCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(context.Scanner.ScannedRoots, Is.Empty);
            Assert.That(context.ViewModel.IsScanning, Is.False);
        });
    }

    [Test]
    public async Task TheBasketToggleStatesTheActionItWillPerform()
    {
        var context = CreateContext();
        await CompleteScanAsync(context);
        SelectLargeFile(context, MoviePath);
        var changes = TrackChanges(
            context.ViewModel,
            nameof(MainWindowViewModel.CleanupBasketToggleRemovesItem));
        var before = (
            Removes: context.ViewModel.CleanupBasketToggleRemovesItem,
            ToolTip: context.ViewModel.CleanupBasketToggleToolTip);

        context.ViewModel.ToggleSelectedItemInCleanupBasketCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(before.Removes, Is.False);
            Assert.That(before.ToolTip, Is.EqualTo("Add to Cleanup Basket (Command-B)"));
            Assert.That(context.ViewModel.CleanupBasketToggleRemovesItem, Is.True);
            Assert.That(
                context.ViewModel.CleanupBasketToggleToolTip,
                Is.EqualTo("Remove from Cleanup Basket (Command-B)"));
            Assert.That(
                context.ViewModel.CleanupBasketCommandLabel,
                Is.EqualTo(MainWindowViewModel.RemoveFromCleanupBasketLabel));
            Assert.That(changes(), Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task TheBasketToggleShowsTheBasketItemCount()
    {
        var context = CreateContext();
        await CompleteScanAsync(context);
        var countBefore = context.ViewModel.CleanupBasketItemCount;
        var badgeBefore = context.ViewModel.HasCleanupBasketItems;

        foreach (var path in new[] { MoviePath, FirstProjectFilePath, SecondProjectFilePath })
        {
            SelectLargeFile(context, path);
            context.ViewModel.ToggleSelectedItemInCleanupBasketCommand.Execute(null);
        }

        Assert.Multiple(() =>
        {
            Assert.That(countBefore, Is.Zero);
            Assert.That(badgeBefore, Is.False);
            Assert.That(context.ViewModel.CleanupBasketItemCount, Is.EqualTo(3));
            Assert.That(context.ViewModel.HasCleanupBasketItems, Is.True);
        });
    }

    [Test]
    public async Task TheExportControlIsAvailableExactlyWhenExportIs()
    {
        var context = CreateContext();
        var viewModel = context.ViewModel;
        var changes = TrackChanges(viewModel, nameof(MainWindowViewModel.IsExportAvailable));
        var beforeScan = (Control: viewModel.IsExportAvailable, Csv: viewModel.ExportCsvCommand.CanExecute(null));
        viewModel.SelectedFolderPath = RootPath;
        context.Scanner.BlockScans = true;
        viewModel.PrimaryScanActionCommand.Execute(null);
        await context.Scanner.ScanStarted.Task;
        var whileScanning = (Control: viewModel.IsExportAvailable, Json: viewModel.ExportJsonCommand.CanExecute(null));
        await StopAsync(context);
        context.Scanner.BlockScans = false;

        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(beforeScan.Control, Is.EqualTo(beforeScan.Csv).And.False);
            Assert.That(whileScanning.Control, Is.EqualTo(whileScanning.Json).And.False);
            Assert.That(viewModel.IsExportAvailable, Is.EqualTo(viewModel.ExportCsvCommand.CanExecute(null)).And.True);
            Assert.That(viewModel.IsExportAvailable, Is.EqualTo(viewModel.ExportJsonCommand.CanExecute(null)));
            Assert.That(changes(), Is.GreaterThan(0));
        });
    }

    private static async Task CompleteScanAsync(PrimaryScanContext context)
    {
        context.ViewModel.MeasurementMode = StorageMeasurementMode.Logical;
        context.ViewModel.SelectedFolderPath = RootPath;
        await context.ViewModel.ScanFolderCommand.ExecuteAsync(null);
        context.Time.Advance(MainWindowViewModel.PrimaryScanActionTransitionInterval);
    }

    private static async Task StopAsync(PrimaryScanContext context)
    {
        context.ViewModel.StopScanCommand.Execute(null);
        if (context.ViewModel.ScanFolderCommand.ExecutionTask is { } scan)
        {
            await scan;
        }
    }

    private static void SelectLargeFile(PrimaryScanContext context, string path) =>
        context.ViewModel.SelectedLargeFile = context.ViewModel.LargeFiles
            .Single(file => file.Path == path);

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

    private static PrimaryScanContext CreateContext()
    {
        var scanner = new BlockingScanner(CreateTree());
        var time = new ManualTimeProvider();
        var viewModel = new MainWindowViewModel(
            Substitute.For<IFolderPickerService>(),
            scanner,
            new ImmediateDispatcher(),
            Substitute.For<IFileRevealService>(),
            Substitute.For<ITrashService>(),
            Substitute.For<ITrashConfirmationService>(),
            quickLookService: Substitute.For<IQuickLookService>(),
            timeProvider: time);

        return new PrimaryScanContext(viewModel, scanner, time);
    }

    private static DiskItem CreateTree()
    {
        var root = new DiskItem("root", RootPath, isDirectory: true);
        var project = new DiskItem("project", ProjectPath, isDirectory: true);
        project.AddChild(File(FirstProjectFilePath, 2048));
        project.AddChild(File(SecondProjectFilePath, 1024));
        project.SizeBytes = 3072;
        project.MeasuredSizeBytes = 3072;
        root.AddChild(project);
        root.AddChild(File(MoviePath, 4096));
        root.SizeBytes = 7168;
        root.MeasuredSizeBytes = 7168;
        return root;
    }

    private static DiskItem File(string path, long size) =>
        new(Path.GetFileName(path), path, isDirectory: false)
        {
            SizeBytes = size,
            MeasuredSizeBytes = size
        };

    private sealed record PrimaryScanContext(
        MainWindowViewModel ViewModel,
        BlockingScanner Scanner,
        ManualTimeProvider Time);

    private sealed class BlockingScanner(DiskItem root) : IDiskScanner
    {
        public bool BlockScans { get; set; }

        public List<string> ScannedRoots { get; } = [];

        public List<string> SubtreeRequests { get; } = [];

        public int CancelledScans { get; private set; }

        public TaskCompletionSource ScanStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async IAsyncEnumerable<ScanProgress> ScanAsync(
            string rootPath,
            ScanOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            ScannedRoots.Add(rootPath);
            await BlockIfRequestedAsync(cancellationToken);

            yield return new ScanProgress(
                root.Path,
                FilesScanned: 3,
                DirectoriesScanned: 2,
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
            SubtreeRequests.Add(subtreePath);
            await BlockIfRequestedAsync(cancellationToken);

            yield break;
        }

        private async Task BlockIfRequestedAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();
            if (!BlockScans)
            {
                return;
            }

            ScanStarted.TrySetResult();
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                CancelledScans++;
                throw;
            }
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private long _timestamp;

        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        public override long GetTimestamp() => _timestamp;

        public void Advance(TimeSpan interval) => _timestamp += interval.Ticks;
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
