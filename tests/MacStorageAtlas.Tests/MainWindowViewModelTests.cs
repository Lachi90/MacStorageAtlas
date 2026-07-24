using System.IO;
using System.Runtime.CompilerServices;
using MacStorageAtlas.App.Models;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core;
using MacStorageAtlas.Rendering;
using NSubstitute;

namespace MacStorageAtlas.Tests;

public class MainWindowViewModelTests
{
    [Test]
    public void ApplicationNameIdentifiesTheApplication()
    {
        var viewModel = new MainWindowViewModel();

        var applicationName = viewModel.ApplicationName;

        Assert.That(applicationName, Is.EqualTo("MacStorageAtlas"));
    }

    [Test]
    public void ApplicationDefaultsToHardlinkAwareAllocatedMeasurement()
    {
        var viewModel = new MainWindowViewModel();

        Assert.Multiple(() =>
        {
            Assert.That(
                viewModel.MeasurementMode,
                Is.EqualTo(StorageMeasurementMode.HardlinkAwareAllocated));
            Assert.That(
                viewModel.ResultMeasurementMode,
                Is.EqualTo(StorageMeasurementMode.HardlinkAwareAllocated));
            Assert.That(
                viewModel.MeasurementBasisLabel,
                Is.EqualTo("Allocated size, hardlinks counted once"));
        });
    }

    [Test]
    public async Task SelectFolderCommandStoresSelectedPath()
    {
        var folderPicker = Substitute.For<IFolderPickerService>();
        folderPicker.SelectFolderAsync().Returns("/Users/test/Documents");
        var viewModel = new MainWindowViewModel(folderPicker);

        await viewModel.SelectFolderCommand.ExecuteAsync(null);

        Assert.That(viewModel.SelectedFolderPath, Is.EqualTo("/Users/test/Documents"));
    }

    [Test]
    public async Task SelectFolderCommandLeavesExistingPathWhenSelectionIsCancelled()
    {
        var folderPicker = Substitute.For<IFolderPickerService>();
        folderPicker.SelectFolderAsync().Returns(
            "/Users/test/Documents",
            (string?)null);
        var viewModel = new MainWindowViewModel(folderPicker);

        await viewModel.SelectFolderCommand.ExecuteAsync(null);
        await viewModel.SelectFolderCommand.ExecuteAsync(null);

        Assert.That(viewModel.SelectedFolderPath, Is.EqualTo("/Users/test/Documents"));
    }

    [Test]
    public async Task ScanFolderCommandReportsProgressWhileScanIsRunning()
    {
        var continueScan = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var progressApplied = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var scanner = new StubDiskScanner(
            cancellationToken => ProgressUntilReleasedAsync(
                continueScan.Task,
                progressApplied,
                cancellationToken));
        var dispatcher = new RecordingUiDispatcher();
        var folderPicker = Substitute.For<IFolderPickerService>();
        folderPicker.SelectFolderAsync().Returns("/scan/root");
        var viewModel = new MainWindowViewModel(folderPicker, scanner, dispatcher);
        await viewModel.SelectFolderCommand.ExecuteAsync(null);

        var scanTask = viewModel.ScanFolderCommand.ExecuteAsync(null);
        await progressApplied.Task;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsScanning, Is.True);
            Assert.That(viewModel.CurrentPath, Is.EqualTo("/scan/root/file.dat"));
            Assert.That(viewModel.FilesScanned, Is.EqualTo(1));
            Assert.That(viewModel.DirectoriesScanned, Is.EqualTo(2));
            Assert.That(viewModel.BytesScanned, Is.EqualTo(4_096));
            Assert.That(
                viewModel.ResultMeasurementMode,
                Is.EqualTo(StorageMeasurementMode.HardlinkAwareAllocated));
            Assert.That(
                viewModel.MeasurementBasisLabel,
                Is.EqualTo("Allocated size, hardlinks counted once"));
            Assert.That(viewModel.ScanErrors, Has.Count.EqualTo(1));
            Assert.That(viewModel.ScanErrors[0].Path, Is.EqualTo("/scan/root/restricted"));
            Assert.That(viewModel.ScanErrors[0].ExceptionType, Is.EqualTo(nameof(UnauthorizedAccessException)));
            Assert.That(dispatcher.InvocationCount, Is.GreaterThanOrEqualTo(2));
        });

        continueScan.SetResult();
        await scanTask;
        Assert.That(viewModel.IsScanning, Is.False);
    }

    [Test]
    public void StopScanCommandIsDisabledWhenNotScanning()
    {
        var viewModel = new MainWindowViewModel(
            Substitute.For<IFolderPickerService>(),
            Substitute.For<IDiskScanner>(),
            new RecordingUiDispatcher());

        Assert.That(viewModel.StopScanCommand.CanExecute(null), Is.False);
    }

    [Test]
    public async Task StopScanCommandCancelsActiveScanAndKeepsPartialResults()
    {
        var progressApplied = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var scanner = new StubDiskScanner(
            cancellationToken => ProgressThenAwaitCancellationAsync(
                progressApplied,
                cancellationToken));
        var folderPicker = Substitute.For<IFolderPickerService>();
        folderPicker.SelectFolderAsync().Returns("/scan/root");
        var viewModel = new MainWindowViewModel(
            folderPicker,
            scanner,
            new RecordingUiDispatcher());
        await viewModel.SelectFolderCommand.ExecuteAsync(null);

        var scanTask = viewModel.ScanFolderCommand.ExecuteAsync(null);
        await progressApplied.Task;
        Assert.That(viewModel.StopScanCommand.CanExecute(null), Is.True);
        viewModel.StopScanCommand.Execute(null);
        await scanTask;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsScanning, Is.False);
            Assert.That(viewModel.StopScanCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.FilesScanned, Is.EqualTo(3));
            Assert.That(viewModel.BytesScanned, Is.EqualTo(2_048));
            Assert.That(viewModel.CurrentPath, Is.EqualTo("/scan/root/partial.dat"));
            Assert.That(
                viewModel.MeasurementBasisLabel,
                Is.EqualTo("Allocated size, hardlinks counted once"));
        });
    }

    [Test]
    public async Task CompletedScanMapsTreeAndStoresSelection()
    {
        var root = new DiskItem("root", "/scan/root", isDirectory: true);
        var folder = new DiskItem("folder", "/scan/root/folder", isDirectory: true);
        var file = new DiskItem("file.dat", "/scan/root/folder/file.dat", isDirectory: false)
        {
            SizeBytes = 1_536
        };
        folder.AddChild(file);
        folder.SizeBytes = file.SizeBytes;
        root.AddChild(folder);
        root.SizeBytes = folder.SizeBytes;

        var scanner = new StubDiskScanner(
            _ => CompletedScanAsync(root, StorageMeasurementMode.Allocated));
        var folderPicker = Substitute.For<IFolderPickerService>();
        folderPicker.SelectFolderAsync().Returns("/scan/root");
        var viewModel = new MainWindowViewModel(
            folderPicker,
            scanner,
            new RecordingUiDispatcher());
        await viewModel.SelectFolderCommand.ExecuteAsync(null);

        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        var rootNode = viewModel.TreeItems.Single();
        var folderNode = rootNode.Children.Single();
        var fileNode = folderNode.Children.Single();
        viewModel.SelectedTreeItem = fileNode;

        Assert.Multiple(() =>
        {
            Assert.That(rootNode.Name, Is.EqualTo("root"));
            Assert.That(folderNode.Name, Is.EqualTo("folder"));
            Assert.That(fileNode.Name, Is.EqualTo("file.dat"));
            Assert.That(fileNode.FormattedSize, Is.EqualTo("1.5 KB"));
            Assert.That(fileNode.Item, Is.SameAs(file));
            Assert.That(viewModel.SelectedTreeItem, Is.SameAs(fileNode));
            Assert.That(viewModel.FileTypeSummaries, Has.Count.EqualTo(1));
            Assert.That(viewModel.FileTypeSummaries[0].Extension, Is.EqualTo(".dat"));
            Assert.That(viewModel.FileTypeSummaries[0].FileCount, Is.EqualTo(1));
            Assert.That(viewModel.FileTypeSummaries[0].TotalSizeBytes, Is.EqualTo(1_536));
        });
    }

    [Test]
    public async Task ScanFolderCommandPassesPackageExpansionPreferenceToScanner()
    {
        var root = new DiskItem("root", "/scan/root", isDirectory: true);
        var scanner = new CapturingDiskScanner(_ => CompletedScanAsync(root));
        var folderPicker = Substitute.For<IFolderPickerService>();
        folderPicker.SelectFolderAsync().Returns(root.Path);
        var viewModel = new MainWindowViewModel(
            folderPicker,
            scanner,
            new RecordingUiDispatcher());
        await viewModel.SelectFolderCommand.ExecuteAsync(null);
        viewModel.ExpandApplicationBundles = false;

        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.That(scanner.LastOptions, Is.Not.Null);
        Assert.That(scanner.LastOptions!.TreatPackagesAsDirectories, Is.False);
    }

    [Test]
    public async Task LogicalScanUsesReturnedLogicalMeasurementLabel()
    {
        var root = new DiskItem("root", "/scan/root", isDirectory: true);
        var scanner = new CapturingDiskScanner(
            _ => CompletedScanAsync(root, StorageMeasurementMode.Logical));
        var folderPicker = Substitute.For<IFolderPickerService>();
        folderPicker.SelectFolderAsync().Returns(root.Path);
        var viewModel = new MainWindowViewModel(
            folderPicker,
            scanner,
            new RecordingUiDispatcher())
        {
            MeasurementMode = StorageMeasurementMode.Logical
        };
        await viewModel.SelectFolderCommand.ExecuteAsync(null);

        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(
                scanner.LastOptions!.MeasurementMode,
                Is.EqualTo(StorageMeasurementMode.Logical));
            Assert.That(
                viewModel.ResultMeasurementMode,
                Is.EqualTo(StorageMeasurementMode.Logical));
            Assert.That(viewModel.MeasurementBasisLabel, Is.EqualTo("Logical size"));
        });
    }

    [Test]
    public async Task ChangingNextScanPreferenceDoesNotRelabelCompletedResult()
    {
        var root = new DiskItem("root", "/scan/root", isDirectory: true);
        var scanner = new StubDiskScanner(
            _ => CompletedScanAsync(root, StorageMeasurementMode.Allocated));
        var folderPicker = Substitute.For<IFolderPickerService>();
        folderPicker.SelectFolderAsync().Returns(root.Path);
        var viewModel = new MainWindowViewModel(
            folderPicker,
            scanner,
            new RecordingUiDispatcher());
        await viewModel.SelectFolderCommand.ExecuteAsync(null);
        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        viewModel.MeasurementMode = StorageMeasurementMode.Logical;

        Assert.Multiple(() =>
        {
            Assert.That(
                viewModel.ResultMeasurementMode,
                Is.EqualTo(StorageMeasurementMode.Allocated));
            Assert.That(
                viewModel.MeasurementBasisLabel,
                Is.EqualTo("Allocated size per path"));
        });
    }

    [Test]
    public async Task ScanFolderCommandExpandsApplicationBundlesByDefault()
    {
        var root = new DiskItem("root", "/scan/root", isDirectory: true);
        var scanner = new CapturingDiskScanner(_ => CompletedScanAsync(root));
        var folderPicker = Substitute.For<IFolderPickerService>();
        folderPicker.SelectFolderAsync().Returns(root.Path);
        var viewModel = new MainWindowViewModel(
            folderPicker,
            scanner,
            new RecordingUiDispatcher());
        await viewModel.SelectFolderCommand.ExecuteAsync(null);

        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.That(viewModel.ExpandApplicationBundles, Is.True);
        Assert.That(scanner.LastOptions!.TreatPackagesAsDirectories, Is.True);
    }

    [Test]
    public async Task ScanFolderCommandExcludesHiddenFilesByDefault()
    {
        var root = new DiskItem("root", "/scan/root", isDirectory: true);
        var scanner = new CapturingDiskScanner(_ => CompletedScanAsync(root));
        var folderPicker = Substitute.For<IFolderPickerService>();
        folderPicker.SelectFolderAsync().Returns(root.Path);
        var viewModel = new MainWindowViewModel(
            folderPicker,
            scanner,
            new RecordingUiDispatcher());
        await viewModel.SelectFolderCommand.ExecuteAsync(null);

        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.That(viewModel.IncludeHiddenFiles, Is.False);
        Assert.That(scanner.LastOptions!.IncludeHiddenFiles, Is.False);
    }

    [Test]
    public async Task ScanFolderCommandPassesHiddenFilePreferenceToScanner()
    {
        var root = new DiskItem("root", "/scan/root", isDirectory: true);
        var scanner = new CapturingDiskScanner(_ => CompletedScanAsync(root));
        var folderPicker = Substitute.For<IFolderPickerService>();
        folderPicker.SelectFolderAsync().Returns(root.Path);
        var viewModel = new MainWindowViewModel(
            folderPicker,
            scanner,
            new RecordingUiDispatcher());
        await viewModel.SelectFolderCommand.ExecuteAsync(null);
        viewModel.IncludeHiddenFiles = true;

        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.That(scanner.LastOptions, Is.Not.Null);
        Assert.That(scanner.LastOptions!.IncludeHiddenFiles, Is.True);
    }

    [Test]
    public async Task ScanFolderCommandDoesNotFollowSymbolicLinksByDefault()
    {
        var root = new DiskItem("root", "/scan/root", isDirectory: true);
        var scanner = new CapturingDiskScanner(_ => CompletedScanAsync(root));
        var folderPicker = Substitute.For<IFolderPickerService>();
        folderPicker.SelectFolderAsync().Returns(root.Path);
        var viewModel = new MainWindowViewModel(
            folderPicker,
            scanner,
            new RecordingUiDispatcher());
        await viewModel.SelectFolderCommand.ExecuteAsync(null);

        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.That(viewModel.FollowSymbolicLinks, Is.False);
        Assert.That(scanner.LastOptions!.FollowSymbolicLinks, Is.False);
    }

    [Test]
    public async Task ScanFolderCommandPassesSymbolicLinkPreferenceToScanner()
    {
        var root = new DiskItem("root", "/scan/root", isDirectory: true);
        var scanner = new CapturingDiskScanner(_ => CompletedScanAsync(root));
        var folderPicker = Substitute.For<IFolderPickerService>();
        folderPicker.SelectFolderAsync().Returns(root.Path);
        var viewModel = new MainWindowViewModel(
            folderPicker,
            scanner,
            new RecordingUiDispatcher());
        await viewModel.SelectFolderCommand.ExecuteAsync(null);
        viewModel.FollowSymbolicLinks = true;

        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.That(scanner.LastOptions, Is.Not.Null);
        Assert.That(scanner.LastOptions!.FollowSymbolicLinks, Is.True);
    }

    [Test]
    public void TreemapSelectionExposesSelectedItemDetails()
    {
        var item = new DiskItem("archive.zip", "/scan/root/archive.zip", isDirectory: false)
        {
            SizeBytes = 1_536
        };
        var rectangle = new TreemapRect(new TreemapItem(item), 0, 0, 100, 50);
        var viewModel = new MainWindowViewModel();

        viewModel.SelectedTreemapRectangle = rectangle;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedTreemapItem, Is.SameAs(item));
            Assert.That(viewModel.SelectedTreemapItem!.Name, Is.EqualTo("archive.zip"));
            Assert.That(viewModel.SelectedTreemapItem.Path, Is.EqualTo("/scan/root/archive.zip"));
            Assert.That(viewModel.SelectedItemMeasuredSize, Is.EqualTo("1.5 KB"));
            Assert.That(viewModel.SelectedItemCountedSize, Is.EqualTo("1.5 KB"));
            Assert.That(viewModel.HasSelectedItem, Is.True);
        });
    }

    [Test]
    public void ClearingTreemapSelectionClearsSelectedItemDetails()
    {
        var item = new DiskItem("file.dat", "/scan/root/file.dat", isDirectory: false);
        var viewModel = new MainWindowViewModel
        {
            SelectedTreemapRectangle = new TreemapRect(new TreemapItem(item), 0, 0, 100, 50)
        };

        viewModel.SelectedTreemapRectangle = null;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedTreemapItem, Is.Null);
            Assert.That(viewModel.SelectedItemMeasuredSize, Is.Empty);
            Assert.That(viewModel.SelectedItemCountedSize, Is.Empty);
            Assert.That(viewModel.HasSelectedItem, Is.False);
        });
    }

    [Test]
    public async Task SharedTreeItemShowsMeasuredAndCountedSizesWithoutTreemapWeight()
    {
        var root = new DiskItem("root", "/scan/root", isDirectory: true)
        {
            SizeBytes = 4096,
            MeasuredSizeBytes = 8192
        };
        var counted = new DiskItem(
            "counted.bin",
            "/scan/root/counted.bin",
            isDirectory: false)
        {
            SizeBytes = 4096,
            MeasuredSizeBytes = 4096
        };
        var shared = new DiskItem(
            "shared.bin",
            "/scan/root/shared.bin",
            isDirectory: false)
        {
            SizeBytes = 0,
            MeasuredSizeBytes = 4096,
            IsSizeCountedElsewhere = true
        };
        root.AddChild(counted);
        root.AddChild(shared);
        var scanner = new StubDiskScanner(_ => CompletedScanAsync(root));
        var folderPicker = Substitute.For<IFolderPickerService>();
        folderPicker.SelectFolderAsync().Returns(root.Path);
        var viewModel = new MainWindowViewModel(
            folderPicker,
            scanner,
            new RecordingUiDispatcher());
        await viewModel.SelectFolderCommand.ExecuteAsync(null);
        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        var sharedNode = viewModel.TreeItems.Single().Children.Single(
            item => item.Item.IsSizeCountedElsewhere);
        viewModel.SelectedTreeItem = sharedNode;

        Assert.Multiple(() =>
        {
            Assert.That(sharedNode.FormattedSize, Is.EqualTo("4.0 KB shared"));
            Assert.That(viewModel.SelectedItem, Is.SameAs(shared));
            Assert.That(viewModel.SelectedItemMeasuredSize, Is.EqualTo("4.0 KB"));
            Assert.That(viewModel.SelectedItemCountedSize, Is.EqualTo("0 B"));
            Assert.That(viewModel.SelectedItemIsCountedElsewhere, Is.True);
            Assert.That(viewModel.TreemapRectangles, Has.Count.EqualTo(0));
            Assert.That(
                viewModel.FileTypeSummaries.Single().TotalSizeBytes,
                Is.EqualTo(4096));
            Assert.That(
                viewModel.FileTypeSummaries.Single().FileCount,
                Is.EqualTo(2));
            Assert.That(viewModel.LargeFiles, Has.Count.EqualTo(2));
            Assert.That(viewModel.LargeFiles[0], Is.SameAs(counted));
            Assert.That(viewModel.LargeFiles[1], Is.SameAs(shared));
        });
    }

    [Test]
    public void RevealInFinderCommandIsDisabledWithoutASelection()
    {
        var revealService = Substitute.For<IFileRevealService>();
        var viewModel = CreateViewModel(revealService);

        Assert.That(viewModel.RevealInFinderCommand.CanExecute(null), Is.False);
    }

    [Test]
    public void RevealInFinderCommandRevealsSelectedTreeItem()
    {
        var revealService = Substitute.For<IFileRevealService>();
        revealService.Reveal(Arg.Any<string>()).Returns(true);
        var viewModel = CreateViewModel(revealService);
        var item = new DiskItem("file.dat", "/scan/root/file.dat", isDirectory: false);
        viewModel.SelectedTreeItem = new DiskItemTreeNodeViewModel(item);

        viewModel.RevealInFinderCommand.Execute(null);

        revealService.Received(1).Reveal(item.Path);
        Assert.That(viewModel.RevealStatusMessage, Is.Null);
    }

    [Test]
    public void RevealInFinderCommandHandlesADeletedSelectedPath()
    {
        var revealService = Substitute.For<IFileRevealService>();
        revealService.Reveal(Arg.Any<string>()).Returns(false);
        var viewModel = CreateViewModel(revealService);
        viewModel.SelectedTreemapRectangle = new TreemapRect(
            new TreemapItem(new DiskItem("deleted.dat", "/deleted.dat", isDirectory: false)),
            0,
            0,
            10,
            10);

        Assert.DoesNotThrow(() => viewModel.RevealInFinderCommand.Execute(null));
        Assert.That(viewModel.RevealStatusMessage, Does.Contain("no longer exists"));
    }

    [Test]
    public void RevealInFinderCommandHandlesAPlatformFailure()
    {
        var revealService = Substitute.For<IFileRevealService>();
        revealService
            .When(service => service.Reveal(Arg.Any<string>()))
            .Do(_ => throw new InvalidOperationException("Finder unavailable"));
        var viewModel = CreateViewModel(revealService);
        viewModel.SelectedTreeItem = new DiskItemTreeNodeViewModel(
            new DiskItem("file.dat", "/file.dat", isDirectory: false));

        Assert.DoesNotThrow(() => viewModel.RevealInFinderCommand.Execute(null));
        Assert.That(viewModel.RevealStatusMessage, Does.Contain("could not be revealed"));
    }

    [Test]
    public void MoveToTrashCommandIsDisabledWithoutASelection()
    {
        var viewModel = CreateTrashViewModel(
            Substitute.For<ITrashService>(),
            Substitute.For<ITrashConfirmationService>());

        Assert.That(viewModel.MoveToTrashCommand.CanExecute(null), Is.False);
    }

    [Test]
    public async Task MoveToTrashCommandDoesNothingWhenConfirmationIsCancelled()
    {
        var trashService = Substitute.For<ITrashService>();
        var confirmation = Substitute.For<ITrashConfirmationService>();
        confirmation.ConfirmMoveToTrashAsync(Arg.Any<DiskItem>()).Returns(false);
        var viewModel = CreateTrashViewModel(trashService, confirmation);
        var item = new DiskItem("file.dat", "/file.dat", isDirectory: false);
        viewModel.SelectedTreeItem = new DiskItemTreeNodeViewModel(item);

        await viewModel.MoveToTrashCommand.ExecuteAsync(null);

        await trashService.DidNotReceiveWithAnyArgs().MoveToTrashAsync(default!);
        Assert.That(viewModel.SelectedItem, Is.SameAs(item));
    }

    [TestCase(StorageMeasurementMode.Logical)]
    [TestCase(StorageMeasurementMode.Allocated)]
    public async Task MoveToTrashCommandRemovesConfirmedItemFromNonDeduplicatedResults(
        StorageMeasurementMode measurementMode)
    {
        var root = new DiskItem("root", "/scan/root", isDirectory: true);
        var item = new DiskItem("file.dat", "/scan/root/file.dat", isDirectory: false)
        {
            SizeBytes = 1_024
        };
        root.AddChild(item);
        root.SizeBytes = item.SizeBytes;
        var trashService = Substitute.For<ITrashService>();
        var confirmation = Substitute.For<ITrashConfirmationService>();
        confirmation.ConfirmMoveToTrashAsync(item).Returns(true);
        var scanner = new StubDiskScanner(
            _ => CompletedScanAsync(root, measurementMode));
        var folderPicker = Substitute.For<IFolderPickerService>();
        folderPicker.SelectFolderAsync().Returns(root.Path);
        var viewModel = new MainWindowViewModel(
            folderPicker,
            scanner,
            new RecordingUiDispatcher(),
            Substitute.For<IFileRevealService>(),
            trashService,
            confirmation);
        await viewModel.SelectFolderCommand.ExecuteAsync(null);
        await viewModel.ScanFolderCommand.ExecuteAsync(null);
        viewModel.SelectedTreeItem = viewModel.TreeItems.Single().Children.Single();

        await viewModel.MoveToTrashCommand.ExecuteAsync(null);

        await trashService.Received(1).MoveToTrashAsync(item.Path);
        Assert.Multiple(() =>
        {
            Assert.That(root.Children, Is.Empty);
            Assert.That(root.SizeBytes, Is.Zero);
            Assert.That(viewModel.TreeItems.Single().Children, Is.Empty);
            Assert.That(viewModel.FileTypeSummaries, Is.Empty);
            Assert.That(viewModel.SelectedItem, Is.Null);
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    public async Task MoveToTrashCommandRefreshesHardlinkAwareResult(
        int selectedChildIndex)
    {
        var originalRoot = HardlinkAwareRoot();
        var refreshedRoot = new DiskItem("root", "/scan/root", isDirectory: true)
        {
            SizeBytes = 4096,
            MeasuredSizeBytes = 4096
        };
        refreshedRoot.AddChild(new DiskItem(
            "remaining.bin",
            "/scan/root/remaining.bin",
            isDirectory: false)
        {
            SizeBytes = 4096,
            MeasuredSizeBytes = 4096
        });
        var scanner = new CapturingDiskScanner(
            (scanCount, _, _) => scanCount == 1
                ? CompletedScanAsync(originalRoot)
                : CompletedScanAsync(refreshedRoot));
        var trashService = Substitute.For<ITrashService>();
        var confirmation = Substitute.For<ITrashConfirmationService>();
        confirmation.ConfirmMoveToTrashAsync(Arg.Any<DiskItem>()).Returns(true);
        var folderPicker = Substitute.For<IFolderPickerService>();
        folderPicker.SelectFolderAsync().Returns(originalRoot.Path);
        var viewModel = new MainWindowViewModel(
            folderPicker,
            scanner,
            new RecordingUiDispatcher(),
            Substitute.For<IFileRevealService>(),
            trashService,
            confirmation)
        {
            IncludeHiddenFiles = true,
            FollowSymbolicLinks = true,
            ExpandApplicationBundles = false
        };
        await viewModel.SelectFolderCommand.ExecuteAsync(null);
        await viewModel.ScanFolderCommand.ExecuteAsync(null);
        viewModel.SelectedTreeItem =
            viewModel.TreeItems.Single().Children[selectedChildIndex];
        viewModel.MeasurementMode = StorageMeasurementMode.Logical;
        viewModel.IncludeHiddenFiles = false;
        viewModel.FollowSymbolicLinks = false;
        viewModel.ExpandApplicationBundles = true;

        await viewModel.MoveToTrashCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(scanner.ScanCount, Is.EqualTo(2));
            Assert.That(
                scanner.LastOptions!.MeasurementMode,
                Is.EqualTo(StorageMeasurementMode.HardlinkAwareAllocated));
            Assert.That(scanner.LastOptions.IncludeHiddenFiles, Is.True);
            Assert.That(scanner.LastOptions.FollowSymbolicLinks, Is.True);
            Assert.That(scanner.LastOptions.TreatPackagesAsDirectories, Is.False);
            Assert.That(viewModel.TreeItems.Single().Children, Has.Count.EqualTo(1));
            Assert.That(viewModel.BytesScanned, Is.EqualTo(4096));
            Assert.That(viewModel.SelectedItem, Is.Null);
        });
    }

    [Test]
    public async Task MoveToTrashCommandRefreshesWhenRemainingLinkIsInCollapsedPackage()
    {
        var originalRoot = HardlinkAwareRoot();
        var refreshedRoot = new DiskItem("root", "/scan/root", isDirectory: true)
        {
            SizeBytes = 4096,
            MeasuredSizeBytes = 4096
        };
        refreshedRoot.AddChild(new DiskItem(
            "Example.app",
            "/scan/root/Example.app",
            isDirectory: true)
        {
            SizeBytes = 4096,
            MeasuredSizeBytes = 4096
        });
        var scanner = new CapturingDiskScanner(
            (scanCount, _, _) => scanCount == 1
                ? CompletedScanAsync(originalRoot)
                : CompletedScanAsync(refreshedRoot));
        var trashService = Substitute.For<ITrashService>();
        var confirmation = Substitute.For<ITrashConfirmationService>();
        confirmation.ConfirmMoveToTrashAsync(Arg.Any<DiskItem>()).Returns(true);
        var folderPicker = Substitute.For<IFolderPickerService>();
        folderPicker.SelectFolderAsync().Returns(originalRoot.Path);
        var viewModel = new MainWindowViewModel(
            folderPicker,
            scanner,
            new RecordingUiDispatcher(),
            Substitute.For<IFileRevealService>(),
            trashService,
            confirmation)
        {
            ExpandApplicationBundles = false
        };
        await viewModel.SelectFolderCommand.ExecuteAsync(null);
        await viewModel.ScanFolderCommand.ExecuteAsync(null);
        viewModel.SelectedTreeItem = viewModel.TreeItems.Single().Children[0];

        await viewModel.MoveToTrashCommand.ExecuteAsync(null);

        var remaining = viewModel.TreeItems.Single().Children.Single();
        Assert.Multiple(() =>
        {
            Assert.That(scanner.ScanCount, Is.EqualTo(2));
            Assert.That(remaining.Name, Is.EqualTo("Example.app"));
            Assert.That(remaining.SizeBytes, Is.EqualTo(4096));
            Assert.That(remaining.Children, Is.Empty);
        });
    }

    [Test]
    public async Task MoveToTrashCommandClearsHardlinkAwareResultWhenRootIsMoved()
    {
        var root = HardlinkAwareRoot();
        var scanner = new CapturingDiskScanner((_, _, _) => CompletedScanAsync(root));
        var trashService = Substitute.For<ITrashService>();
        var confirmation = Substitute.For<ITrashConfirmationService>();
        confirmation.ConfirmMoveToTrashAsync(root).Returns(true);
        var folderPicker = Substitute.For<IFolderPickerService>();
        folderPicker.SelectFolderAsync().Returns(root.Path);
        var viewModel = new MainWindowViewModel(
            folderPicker,
            scanner,
            new RecordingUiDispatcher(),
            Substitute.For<IFileRevealService>(),
            trashService,
            confirmation);
        await viewModel.SelectFolderCommand.ExecuteAsync(null);
        await viewModel.ScanFolderCommand.ExecuteAsync(null);
        viewModel.SelectedTreeItem = viewModel.TreeItems.Single();

        await viewModel.MoveToTrashCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(scanner.ScanCount, Is.EqualTo(1));
            Assert.That(viewModel.TreeItems, Is.Empty);
            Assert.That(viewModel.TreemapRectangles, Is.Empty);
            Assert.That(viewModel.FileTypeSummaries, Is.Empty);
            Assert.That(viewModel.LargeFiles, Is.Empty);
        });
    }

    [Test]
    public async Task CancellingPostTrashRefreshDoesNotRestoreStaleCompletedResult()
    {
        var originalRoot = HardlinkAwareRoot();
        var progressApplied = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var scanner = new CapturingDiskScanner(
            (scanCount, _, cancellationToken) => scanCount == 1
                ? CompletedScanAsync(originalRoot)
                : ProgressThenAwaitCancellationAsync(
                    progressApplied,
                    cancellationToken));
        var trashService = Substitute.For<ITrashService>();
        var confirmation = Substitute.For<ITrashConfirmationService>();
        confirmation.ConfirmMoveToTrashAsync(Arg.Any<DiskItem>()).Returns(true);
        var folderPicker = Substitute.For<IFolderPickerService>();
        folderPicker.SelectFolderAsync().Returns(originalRoot.Path);
        var viewModel = new MainWindowViewModel(
            folderPicker,
            scanner,
            new RecordingUiDispatcher(),
            Substitute.For<IFileRevealService>(),
            trashService,
            confirmation);
        await viewModel.SelectFolderCommand.ExecuteAsync(null);
        await viewModel.ScanFolderCommand.ExecuteAsync(null);
        viewModel.SelectedTreeItem = viewModel.TreeItems.Single().Children[0];

        var trashTask = viewModel.MoveToTrashCommand.ExecuteAsync(null);
        await progressApplied.Task;
        viewModel.StopScanCommand.Execute(null);
        await trashTask;

        Assert.Multiple(() =>
        {
            Assert.That(scanner.ScanCount, Is.EqualTo(2));
            Assert.That(viewModel.IsScanning, Is.False);
            Assert.That(viewModel.TreeItems, Is.Empty);
            Assert.That(viewModel.StopScanCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public async Task MoveToTrashCommandKeepsItemAndShowsErrorWhenOperationFails()
    {
        var item = new DiskItem("file.dat", "/file.dat", isDirectory: false);
        var trashService = Substitute.For<ITrashService>();
        trashService
            .MoveToTrashAsync(item.Path)
            .Returns(Task.FromException(new InvalidOperationException("Trash is unavailable.")));
        var confirmation = Substitute.For<ITrashConfirmationService>();
        confirmation.ConfirmMoveToTrashAsync(item).Returns(true);
        var viewModel = CreateTrashViewModel(trashService, confirmation);
        viewModel.SelectedTreeItem = new DiskItemTreeNodeViewModel(item);

        await viewModel.MoveToTrashCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedItem, Is.SameAs(item));
            Assert.That(viewModel.TrashStatusMessage, Does.Contain("Could not move"));
            Assert.That(viewModel.TrashStatusMessage, Does.Contain("Trash is unavailable"));
        });
    }

    [Test]
    public void RescanCommandIsDisabledWithoutASelectedFolder()
    {
        var viewModel = new MainWindowViewModel(
            Substitute.For<IFolderPickerService>(),
            Substitute.For<IDiskScanner>(),
            new RecordingUiDispatcher());

        Assert.That(viewModel.RescanCommand.CanExecute(null), Is.False);
    }

    [Test]
    public async Task RescanCommandRunsAnotherScanForTheSelectedFolder()
    {
        var root = new DiskItem("root", "/scan/root", isDirectory: true);
        var scanner = new CapturingDiskScanner(_ => CompletedScanAsync(root));
        var folderPicker = Substitute.For<IFolderPickerService>();
        folderPicker.SelectFolderAsync().Returns(root.Path);
        var viewModel = new MainWindowViewModel(
            folderPicker,
            scanner,
            new RecordingUiDispatcher());
        await viewModel.SelectFolderCommand.ExecuteAsync(null);
        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.That(viewModel.RescanCommand.CanExecute(null), Is.True);
        await viewModel.RescanCommand.ExecuteAsync(null);

        Assert.That(scanner.ScanCount, Is.EqualTo(2));
    }

    [Test]
    public async Task ScanFolderCommandRecordsTheScannedLocationAsRecent()
    {
        var root = new DiskItem("root", "/scan/root", isDirectory: true);
        var viewModel = CreateScanningViewModel(root, new InMemorySettingsService());
        viewModel.SelectedFolderPath = "/scan/root";

        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.That(viewModel.RecentLocations, Is.EqualTo(new[] { "/scan/root" }));
    }

    [Test]
    public async Task RecentLocationsKeepMostRecentFirstWithoutDuplicates()
    {
        var root = new DiskItem("root", "/scan/root", isDirectory: true);
        var viewModel = CreateScanningViewModel(root, new InMemorySettingsService());

        await ScanPathAsync(viewModel, "/a");
        await ScanPathAsync(viewModel, "/b");
        await ScanPathAsync(viewModel, "/a");

        Assert.That(viewModel.RecentLocations, Is.EqualTo(new[] { "/a", "/b" }));
    }

    [Test]
    public async Task RecentLocationsAreLimitedToTen()
    {
        var root = new DiskItem("root", "/scan/root", isDirectory: true);
        var viewModel = CreateScanningViewModel(root, new InMemorySettingsService());

        for (var index = 0; index <= 10; index++)
        {
            await ScanPathAsync(viewModel, $"/path/{index}");
        }

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.RecentLocations, Has.Count.EqualTo(AppSettings.MaxRecentLocations));
            Assert.That(viewModel.RecentLocations[0], Is.EqualTo("/path/10"));
            Assert.That(viewModel.RecentLocations, Does.Not.Contain("/path/0"));
        });
    }

    [Test]
    public void OptionsAreRestoredFromSettingsOnConstruction()
    {
        var settingsService = new InMemorySettingsService();
        settingsService.Save(new AppSettings
        {
            IncludeHiddenFiles = true,
            FollowSymbolicLinks = true,
            TreatPackagesAsDirectories = false,
            MeasurementMode = StorageMeasurementMode.Allocated,
            RecentLocations = ["/Users/test/A"]
        });

        var viewModel = CreateScanningViewModel(
            new DiskItem("root", "/scan/root", isDirectory: true),
            settingsService);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IncludeHiddenFiles, Is.True);
            Assert.That(viewModel.FollowSymbolicLinks, Is.True);
            Assert.That(viewModel.ExpandApplicationBundles, Is.False);
            Assert.That(
                viewModel.MeasurementMode,
                Is.EqualTo(StorageMeasurementMode.Allocated));
            Assert.That(viewModel.RecentLocations, Is.EqualTo(new[] { "/Users/test/A" }));
        });
    }

    [Test]
    public void TogglingAnOptionPersistsSettings()
    {
        var settingsService = new InMemorySettingsService();
        var viewModel = CreateScanningViewModel(
            new DiskItem("root", "/scan/root", isDirectory: true),
            settingsService);

        viewModel.IncludeHiddenFiles = true;
        viewModel.FollowSymbolicLinks = true;
        viewModel.ExpandApplicationBundles = false;
        viewModel.MeasurementMode = StorageMeasurementMode.Logical;

        var saved = settingsService.Load();
        Assert.Multiple(() =>
        {
            Assert.That(saved.IncludeHiddenFiles, Is.True);
            Assert.That(saved.FollowSymbolicLinks, Is.True);
            Assert.That(saved.TreatPackagesAsDirectories, Is.False);
            Assert.That(
                saved.EffectiveMeasurementMode,
                Is.EqualTo(StorageMeasurementMode.Logical));
        });
    }

    [Test]
    public async Task CompletedScanPopulatesLargestFiles()
    {
        var root = new DiskItem("root", "/scan/root", isDirectory: true);
        var folder = new DiskItem("folder", "/scan/root/folder", isDirectory: true);
        var file = new DiskItem("file.dat", "/scan/root/folder/file.dat", isDirectory: false)
        {
            SizeBytes = 1_536
        };
        folder.AddChild(file);
        folder.SizeBytes = file.SizeBytes;
        root.AddChild(folder);
        root.SizeBytes = folder.SizeBytes;
        var viewModel = CreateScanningViewModel(root, new InMemorySettingsService());
        viewModel.SelectedFolderPath = root.Path;

        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.That(viewModel.LargeFiles.Select(item => item.Name), Is.EqualTo(new[] { "file.dat" }));
    }

    [Test]
    public void SelectingALargeFileExposesItAsSelectedItemAndEnablesReveal()
    {
        var revealService = Substitute.For<IFileRevealService>();
        var viewModel = CreateViewModel(revealService);
        var file = new DiskItem("big.bin", "/scan/root/big.bin", isDirectory: false)
        {
            SizeBytes = 4_096
        };

        viewModel.SelectedLargeFile = file;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SelectedItem, Is.SameAs(file));
            Assert.That(viewModel.RevealInFinderCommand.CanExecute(null), Is.True);
            Assert.That(viewModel.MoveToTrashCommand.CanExecute(null), Is.True);
        });
    }

    [Test]
    public void CopyErrorPathCommandIsDisabledWithoutASelectedError()
    {
        var viewModel = CreateScanningViewModel(
            new DiskItem("root", "/scan/root", isDirectory: true),
            new InMemorySettingsService());

        Assert.That(viewModel.CopyErrorPathCommand.CanExecute(null), Is.False);
    }

    [Test]
    public async Task CopyErrorPathCommandCopiesTheSelectedErrorPath()
    {
        var clipboard = new FakeClipboardService();
        var viewModel = new MainWindowViewModel(
            Substitute.For<IFolderPickerService>(),
            Substitute.For<IDiskScanner>(),
            new RecordingUiDispatcher(),
            clipboardService: clipboard);
        var error = new ScanError("/scan/root/restricted", "Access denied.", nameof(UnauthorizedAccessException));
        viewModel.ScanErrors = [error];
        viewModel.SelectedScanError = error;

        Assert.That(viewModel.CopyErrorPathCommand.CanExecute(null), Is.True);
        await viewModel.CopyErrorPathCommand.ExecuteAsync(null);

        Assert.That(clipboard.LastText, Is.EqualTo("/scan/root/restricted"));
    }

    [Test]
    public async Task ScanRecentLocationCommandRemovesAndReportsAMissingPath()
    {
        var viewModel = CreateScanningViewModel(
            new DiskItem("root", "/scan/root", isDirectory: true),
            new InMemorySettingsService());
        var missingPath = Path.Combine(Path.GetTempPath(), $"MacStorageAtlas-missing-{Guid.NewGuid():N}");
        viewModel.RecentLocations = [missingPath];

        await viewModel.ScanRecentLocationCommand.ExecuteAsync(missingPath);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.RecentLocations, Does.Not.Contain(missingPath));
            Assert.That(viewModel.RecentLocationStatusMessage, Does.Contain("no longer exists"));
        });
    }

    private static MainWindowViewModel CreateScanningViewModel(
        DiskItem root,
        ISettingsService settingsService) =>
        new(
            Substitute.For<IFolderPickerService>(),
            new StubDiskScanner(_ => CompletedScanAsync(root)),
            new RecordingUiDispatcher(),
            settingsService: settingsService);

    private static async Task ScanPathAsync(MainWindowViewModel viewModel, string path)
    {
        viewModel.SelectedFolderPath = path;
        await viewModel.ScanFolderCommand.ExecuteAsync(null);
    }

    private static MainWindowViewModel CreateViewModel(IFileRevealService revealService) =>
        new(
            Substitute.For<IFolderPickerService>(),
            Substitute.For<IDiskScanner>(),
            new RecordingUiDispatcher(),
            revealService);

    private static MainWindowViewModel CreateTrashViewModel(
        ITrashService trashService,
        ITrashConfirmationService confirmationService) =>
        new(
            Substitute.For<IFolderPickerService>(),
            Substitute.For<IDiskScanner>(),
            new RecordingUiDispatcher(),
            Substitute.For<IFileRevealService>(),
            trashService,
            confirmationService);

    private static DiskItem HardlinkAwareRoot()
    {
        var root = new DiskItem("root", "/scan/root", isDirectory: true)
        {
            SizeBytes = 4096,
            MeasuredSizeBytes = 8192
        };
        root.AddChild(new DiskItem(
            "counted.bin",
            "/scan/root/counted.bin",
            isDirectory: false)
        {
            SizeBytes = 4096,
            MeasuredSizeBytes = 4096
        });
        root.AddChild(new DiskItem(
            "shared.bin",
            "/scan/root/shared.bin",
            isDirectory: false)
        {
            SizeBytes = 0,
            MeasuredSizeBytes = 4096,
            IsSizeCountedElsewhere = true
        });
        return root;
    }

    private static async IAsyncEnumerable<ScanProgress> CompletedScanAsync(
        DiskItem root,
        StorageMeasurementMode measurementMode =
            StorageMeasurementMode.HardlinkAwareAllocated)
    {
        await Task.Yield();
        yield return new ScanProgress(
            root.Path,
            FilesScanned: 1,
            DirectoriesScanned: 2,
            BytesScanned: root.SizeBytes,
            root,
            Errors: [],
            IsCompleted: true,
            MeasurementMode: measurementMode);
    }

    private static async IAsyncEnumerable<ScanProgress> ProgressUntilReleasedAsync(
        Task continueScan,
        TaskCompletionSource progressApplied,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var root = new DiskItem("root", "/scan/root", isDirectory: true);
        yield return new ScanProgress(
            "/scan/root/file.dat",
            FilesScanned: 1,
            DirectoriesScanned: 2,
            BytesScanned: 4_096,
            root,
            Errors:
            [
                new ScanError(
                    "/scan/root/restricted",
                    "Access denied.",
                    nameof(UnauthorizedAccessException))
            ],
            MeasurementMode: StorageMeasurementMode.HardlinkAwareAllocated);

        progressApplied.SetResult();
        await continueScan.WaitAsync(cancellationToken);
    }

    private static async IAsyncEnumerable<ScanProgress> ProgressThenAwaitCancellationAsync(
        TaskCompletionSource progressApplied,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var root = new DiskItem("root", "/scan/root", isDirectory: true);
        yield return new ScanProgress(
            "/scan/root/partial.dat",
            FilesScanned: 3,
            DirectoriesScanned: 1,
            BytesScanned: 2_048,
            root,
            Errors: [],
            MeasurementMode: StorageMeasurementMode.HardlinkAwareAllocated);

        progressApplied.SetResult();
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    private sealed class StubDiskScanner(
        Func<CancellationToken, IAsyncEnumerable<ScanProgress>> scan) : IDiskScanner
    {
        public IAsyncEnumerable<ScanProgress> ScanAsync(
            string rootPath,
            ScanOptions? options = null,
            CancellationToken cancellationToken = default) => scan(cancellationToken);
    }

    private sealed class CapturingDiskScanner : IDiskScanner
    {
        private readonly Func<
            int,
            ScanOptions?,
            CancellationToken,
            IAsyncEnumerable<ScanProgress>> _scan;

        public CapturingDiskScanner(
            Func<CancellationToken, IAsyncEnumerable<ScanProgress>> scan)
            : this((_, _, cancellationToken) => scan(cancellationToken))
        {
        }

        public CapturingDiskScanner(
            Func<
                int,
                ScanOptions?,
                CancellationToken,
                IAsyncEnumerable<ScanProgress>> scan)
        {
            _scan = scan;
        }

        public ScanOptions? LastOptions { get; private set; }

        public int ScanCount { get; private set; }

        public IAsyncEnumerable<ScanProgress> ScanAsync(
            string rootPath,
            ScanOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            LastOptions = options;
            ScanCount++;
            return _scan(ScanCount, options, cancellationToken);
        }
    }

    private sealed class FakeClipboardService : IClipboardService
    {
        public string? LastText { get; private set; }

        public Task SetTextAsync(string text)
        {
            LastText = text;
            return Task.CompletedTask;
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
