using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Input;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.App.Views;
using MacStorageAtlas.Core.Export;
using MacStorageAtlas.Core.History;
using MacStorageAtlas.Core.Insights;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;
using NSubstitute;

namespace MacStorageAtlas.App.Tests;

public class MainWindowViewModelFolderRescanTests
{
    private static readonly DateTimeOffset ScanTime =
        new(2026, 11, 4, 9, 0, 0, TimeSpan.Zero);

    private string _root = null!;
    private string _project = null!;
    private string _other = null!;
    private DateTimeOffset _now;

    [SetUp]
    public async Task SetUp()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            $"MacStorageAtlas-{Guid.NewGuid():N}");
        _project = Path.Combine(_root, "project");
        _other = Path.Combine(_root, "other");
        _now = ScanTime;

        await WriteFileAsync(Path.Combine(_project, "a.bin"), 100);
        await WriteFileAsync(Path.Combine(_project, "src", "main.bin"), 20);
        await WriteFileAsync(Path.Combine(_other, "b.bin"), 50);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Test]
    public async Task RescanningAFolderRefreshesOnlyThatFolderAndItsAncestors()
    {
        var viewModel = CreateViewModel(new ControlledScanner());
        await ScanAsync(viewModel);
        var rootItem = RootItem(viewModel);
        var other = Find(viewModel, _other);
        await WriteFileAsync(Path.Combine(_project, "added.bin"), 30);
        await WriteFileAsync(Path.Combine(_other, "ignored.bin"), 999);
        SelectTreeNode(viewModel, _project);
        _now = ScanTime.AddMinutes(10);

        await viewModel.RescanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(RootItem(viewModel), Is.SameAs(rootItem));
            Assert.That(Find(viewModel, _project).SizeBytes, Is.EqualTo(150));
            Assert.That(rootItem.SizeBytes, Is.EqualTo(200));
            Assert.That(Find(viewModel, _other), Is.SameAs(other));
            Assert.That(other.SizeBytes, Is.EqualTo(50));
            Assert.That(viewModel.ScanCompletedAt, Is.EqualTo(ScanTime));
            Assert.That(viewModel.SelectedTreeItem?.Item.Path, Is.EqualTo(_project));
            Assert.That(viewModel.SelectedItem?.SizeBytes, Is.EqualTo(150));
            Assert.That(
                viewModel.LargeFiles.Select(file => file.Name),
                Does.Contain("added.bin"));
            Assert.That(viewModel.FilesScanned, Is.EqualTo(4));
            Assert.That(viewModel.BytesScanned, Is.EqualTo(200));
            Assert.That(viewModel.IsScanning, Is.False);
            Assert.That(viewModel.IsRescanningFolder, Is.False);
            Assert.That(
                viewModel.FolderRescanStatusMessage,
                Is.EqualTo("Rescanned “project” with the scan options of the displayed result."));
        });
    }

    [Test]
    public async Task ARescanRecordsTheRefreshedFolderAndDisclosesMixedMeasurementTimes()
    {
        var viewModel = CreateViewModel(new ControlledScanner());
        await ScanAsync(viewModel);
        SelectTreeNode(viewModel, _project);
        _now = ScanTime.AddMinutes(10);

        await viewModel.RescanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasRefreshedFolders, Is.True);
            Assert.That(viewModel.ResultProvenance!.ScanCompletedAt, Is.EqualTo(ScanTime));
            Assert.That(
                viewModel.ResultProvenance.RefreshedFolders,
                Is.EqualTo(new[] { new RefreshedFolder(_project, ScanTime.AddMinutes(10)) }));
            Assert.That(viewModel.RefreshedFoldersSummary, Does.Contain("1 folder was rescanned"));
            Assert.That(viewModel.RefreshedFoldersDetails, Does.Contain(_project));
        });
    }

    [Test]
    public async Task AFullScanClearsTheRefreshedFolders()
    {
        var viewModel = CreateViewModel(new ControlledScanner());
        await ScanAsync(viewModel);
        SelectTreeNode(viewModel, _project);
        await viewModel.RescanFolderCommand.ExecuteAsync(null);

        await ScanAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasRefreshedFolders, Is.False);
            Assert.That(viewModel.ResultProvenance!.RefreshedFolders, Is.Empty);
            Assert.That(viewModel.FolderRescanStatusMessage, Is.Null);
        });
    }

    [Test]
    public async Task RescanningAFolderKeepsItsAncestorsAndOtherExpandedFoldersExpanded()
    {
        var viewModel = CreateViewModel(new ControlledScanner());
        await ScanAsync(viewModel);
        Node(viewModel, _other).IsExpanded = true;
        SelectTreeNode(viewModel, Path.Combine(_project, "src"));

        await viewModel.RescanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.TreeItems[0].IsExpanded, Is.True);
            Assert.That(Node(viewModel, _project).IsExpanded, Is.True);
            Assert.That(Node(viewModel, _other).IsExpanded, Is.True);
            Assert.That(
                viewModel.SelectedTreeItem?.Item.Path,
                Is.EqualTo(Path.Combine(_project, "src")));
        });
    }

    [Test]
    public async Task RescanningADeletedFolderRemovesItFromTheResult()
    {
        var viewModel = CreateViewModel(new ControlledScanner());
        await ScanAsync(viewModel);
        Directory.Delete(_project, recursive: true);
        SelectTreeNode(viewModel, _project);

        await viewModel.RescanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(
                RootItem(viewModel).Children.Select(child => child.Path),
                Is.EqualTo(new[] { _other }));
            Assert.That(RootItem(viewModel).SizeBytes, Is.EqualTo(50));
            Assert.That(viewModel.SelectedTreeItem?.Item, Is.SameAs(RootItem(viewModel)));
            Assert.That(
                viewModel.FolderRescanStatusMessage,
                Is.EqualTo("“project” no longer exists and was removed from the result."));
        });
    }

    [Test]
    public async Task CancellingAFolderRescanLeavesTheResultUnchanged()
    {
        var scanner = new ScriptedScanner(SyntheticTree())
        {
            SubtreeReplacement = SyntheticFolder("/scan/project", ("a.bin", 999))
        };
        var store = new RecordingScanHistoryStore();
        var viewModel = CreateViewModel(scanner, store, historyEnabled: true);
        await viewModel.ScanFolderCommand.ExecuteAsync(null);
        var file = Find(viewModel, "/scan/project/a.bin");
        AddToBasket(viewModel, "/scan/project/a.bin");
        var basket = viewModel.CleanupBasketItems;
        var treeItems = viewModel.TreeItems;
        SelectTreeNode(viewModel, "/scan/project");
        var started = new TaskCompletionSource();
        var browsable = false;
        scanner.BeforeSubtreeCompletes = async cancellationToken =>
        {
            browsable = viewModel.IsRescanningFolder
                && !viewModel.ShowScanOverlay
                && ReferenceEquals(viewModel.TreeItems, treeItems)
                && viewModel.FolderRescanProgressMessage is not null;
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        };

        var rescan = viewModel.RescanFolderCommand.ExecuteAsync(null);
        await started.Task;
        viewModel.StopScanCommand.Execute(null);
        await rescan;

        Assert.Multiple(() =>
        {
            Assert.That(browsable, Is.True);
            Assert.That(Find(viewModel, "/scan/project").SizeBytes, Is.EqualTo(105));
            Assert.That(RootItem(viewModel).SizeBytes, Is.EqualTo(155));
            Assert.That(viewModel.TreeItems, Is.SameAs(treeItems));
            Assert.That(viewModel.CleanupBasketItems, Is.EqualTo(basket));
            Assert.That(viewModel.CleanupBasketItems.Single().Item, Is.SameAs(file));
            Assert.That(viewModel.HasRefreshedFolders, Is.False);
            Assert.That(store.Requests, Has.Count.EqualTo(1));
            Assert.That(viewModel.IsScanning, Is.False);
            Assert.That(viewModel.CurrentPath, Is.EqualTo("/scan"));
            Assert.That(
                viewModel.FolderRescanStatusMessage,
                Is.EqualTo("The rescan of “project” was stopped. The result was not changed."));
        });
    }

    [Test]
    public async Task AFailedFolderRescanLeavesTheResultUnchanged()
    {
        var scanner = new ControlledScanner
        {
            SubtreeFailure = new InvalidOperationException("The volume disappeared.")
        };
        var viewModel = CreateViewModel(scanner);
        await ScanAsync(viewModel);
        await WriteFileAsync(Path.Combine(_project, "added.bin"), 30);
        SelectTreeNode(viewModel, _project);

        await viewModel.RescanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(Find(viewModel, _project).SizeBytes, Is.EqualTo(120));
            Assert.That(viewModel.HasRefreshedFolders, Is.False);
            Assert.That(viewModel.IsScanning, Is.False);
            Assert.That(
                viewModel.FolderRescanStatusMessage,
                Does.Contain("stopped unexpectedly and the result was not changed"));
            Assert.That(
                viewModel.FolderRescanStatusMessage,
                Does.Contain("The volume disappeared."));
        });
    }

    [Test]
    public async Task AFolderRescanUsesTheResultOptionsAndSaysSoWhenTheToolbarDiffers()
    {
        var scanner = new ControlledScanner();
        var viewModel = CreateViewModel(scanner);
        await ScanAsync(viewModel);
        await WriteFileAsync(Path.Combine(_project, ".hidden"), 40);
        viewModel.MeasurementMode = StorageMeasurementMode.Allocated;
        viewModel.IncludeHiddenFiles = true;
        SelectTreeNode(viewModel, _project);

        await viewModel.RescanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(
                scanner.SubtreeRequests.Single().Options,
                Is.EqualTo(scanner.FullScanOptions.Single()));
            Assert.That(
                scanner.SubtreeRequests.Single().Options!.MeasurementMode,
                Is.EqualTo(StorageMeasurementMode.Logical));
            Assert.That(Find(viewModel, _project).SizeBytes, Is.EqualTo(120));
            Assert.That(viewModel.ResultMeasurementMode, Is.EqualTo(StorageMeasurementMode.Logical));
            Assert.That(
                viewModel.FolderRescanStatusMessage,
                Is.EqualTo(
                    "Rescanned “project” with the scan options of the displayed result. "
                    + "The current toolbar options apply to the next full scan."));
        });
    }

    [Test]
    public async Task RescanningTheScanRootRescansTheWholeResultWithTheResultOptions()
    {
        var scanner = new ControlledScanner();
        var viewModel = CreateViewModel(scanner);
        await ScanAsync(viewModel);
        viewModel.IncludeHiddenFiles = true;
        viewModel.SelectedTreeItem = viewModel.TreeItems[0];

        await viewModel.RescanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(scanner.SubtreeRequests, Is.Empty);
            Assert.That(scanner.FullScanOptions, Has.Count.EqualTo(2));
            Assert.That(scanner.FullScanOptions[1], Is.EqualTo(scanner.FullScanOptions[0]));
            Assert.That(scanner.FullScanOptions[1]!.IncludeHiddenFiles, Is.False);
        });
    }

    [TestCase(StorageMeasurementMode.SharedAwareAllocated, false)]
    [TestCase(StorageMeasurementMode.Logical, true)]
    public async Task AnOrderDependentResultOffersAConfirmedFullRescanInstead(
        StorageMeasurementMode mode,
        bool followSymbolicLinks)
    {
        var scanner = new ScriptedScanner(Tree());
        var confirmation = Substitute.For<IFolderRescanConfirmationService>();
        confirmation
            .ConfirmFullRescanAsync(Arg.Any<DiskItem>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(true);
        var viewModel = CreateViewModel(scanner, confirmation: confirmation);
        viewModel.MeasurementMode = mode;
        viewModel.FollowSymbolicLinks = followSymbolicLinks;
        await viewModel.ScanFolderCommand.ExecuteAsync(null);
        viewModel.MeasurementMode = StorageMeasurementMode.Logical;
        viewModel.FollowSymbolicLinks = false;
        SelectTreeNode(viewModel, "/scan/project");

        await viewModel.RescanFolderCommand.ExecuteAsync(null);

        var expectedExplanation = followSymbolicLinks
            ? MainWindowViewModel.FollowedSymbolicLinkFolderRescanExplanation
            : MainWindowViewModel.SharedAwareFolderRescanExplanation;
        await confirmation.Received(1).ConfirmFullRescanAsync(
            Arg.Is<DiskItem>(item => item.Path == "/scan/project"),
            Arg.Is("/scan"),
            Arg.Is(expectedExplanation));
        Assert.Multiple(() =>
        {
            Assert.That(scanner.SubtreeRequestCount, Is.Zero);
            Assert.That(scanner.FullScanOptions, Has.Count.EqualTo(2));
            Assert.That(scanner.FullScanOptions[1], Is.EqualTo(scanner.FullScanOptions[0]));
            Assert.That(scanner.FullScanOptions[1]!.MeasurementMode, Is.EqualTo(mode));
        });
    }

    [Test]
    public async Task DecliningTheFullRescanLeavesTheResultUnchanged()
    {
        var scanner = new ScriptedScanner(Tree());
        var confirmation = Substitute.For<IFolderRescanConfirmationService>();
        confirmation
            .ConfirmFullRescanAsync(Arg.Any<DiskItem>(), Arg.Any<string>(), Arg.Any<string>())
            .Returns(false);
        var viewModel = CreateViewModel(scanner, confirmation: confirmation);
        viewModel.MeasurementMode = StorageMeasurementMode.SharedAwareAllocated;
        await viewModel.ScanFolderCommand.ExecuteAsync(null);
        var treeItems = viewModel.TreeItems;
        SelectTreeNode(viewModel, "/scan/project");

        await viewModel.RescanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(scanner.FullScanOptions, Has.Count.EqualTo(1));
            Assert.That(scanner.SubtreeRequestCount, Is.Zero);
            Assert.That(viewModel.TreeItems, Is.SameAs(treeItems));
            Assert.That(
                viewModel.FolderRescanStatusMessage,
                Does.StartWith(MainWindowViewModel.SharedAwareFolderRescanExplanation));
            Assert.That(viewModel.FolderRescanStatusMessage, Does.EndWith("“project” was not rescanned."));
        });
    }

    [Test]
    public async Task BasketEntriesInsideTheFolderAreRevalidatedAndVanishedOnesReported()
    {
        var scanner = new ScriptedScanner(SyntheticTree())
        {
            SubtreeReplacement = SyntheticFolder("/scan/project", ("a.bin", 400))
        };
        var viewModel = CreateViewModel(scanner);
        await viewModel.ScanFolderCommand.ExecuteAsync(null);
        AddToBasket(viewModel, "/scan/project/a.bin");
        AddToBasket(viewModel, "/scan/project/gone.bin");
        AddToBasket(viewModel, "/scan/other/b.bin");
        var outside = viewModel.CleanupBasketItems.Single(item => item.Snapshot.Name == "b.bin");
        SelectTreeNode(viewModel, "/scan/project");

        await viewModel.RescanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CleanupBasketItems, Has.Count.EqualTo(2));
            Assert.That(
                viewModel.CleanupBasketItems.Single(item => item.Snapshot.Name == "a.bin").Snapshot.SizeBytes,
                Is.EqualTo(400));
            Assert.That(
                viewModel.CleanupBasketItems.Single(item => item.Snapshot.Name == "a.bin").Item,
                Is.SameAs(Find(viewModel, "/scan/project/a.bin")));
            Assert.That(viewModel.CleanupBasketItems, Does.Contain(outside));
            Assert.That(viewModel.CleanupBasketTotalLogicalSize, Is.EqualTo(450));
            Assert.That(
                viewModel.CleanupBasketStatusMessage,
                Is.EqualTo("Removed 1 cleanup basket item that no longer exists after the folder rescan."));
        });
    }

    [Test]
    public async Task DuplicateResultsAreClearedAfterAFolderRescan()
    {
        var viewModel = CreateViewModel(new ControlledScanner());
        await ScanAsync(viewModel);
        var first = Find(viewModel, Path.Combine(_project, "a.bin"));
        var second = Find(viewModel, Path.Combine(_other, "b.bin"));
        viewModel.DuplicateGroups =
        [
            new DuplicateGroup(
                100,
                [
                    new DuplicateGroupEntry(first, 100, DuplicateGroupEntryKind.RetainedCopy),
                    new DuplicateGroupEntry(second, 100, DuplicateGroupEntryKind.ReclaimableCopy)
                ])
        ];
        SelectTreeNode(viewModel, _project);

        await viewModel.RescanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.DuplicateGroups, Is.Empty);
            Assert.That(
                viewModel.DuplicateAnalysisStatusMessage,
                Is.EqualTo(
                    "Duplicate results were cleared because a folder was rescanned. "
                    + "Run duplicate analysis again to refresh them."));
        });
    }

    [Test]
    public async Task GrantingAccessAndRescanningTheBlockedFolderClearsOnlyItsError()
    {
        var locked = Path.Combine(_project, "locked");
        var otherLocked = Path.Combine(_other, "locked");
        Directory.CreateDirectory(locked);
        Directory.CreateDirectory(otherLocked);
        var denied = new HashSet<string>(StringComparer.Ordinal) { locked, otherLocked };
        var scanner = new ControlledScanner(path =>
            denied.Contains(path)
                ? throw new UnauthorizedAccessException("Access denied.")
                : Directory.EnumerateFileSystemEntries(path));
        var viewModel = CreateViewModel(scanner);
        await ScanAsync(viewModel);
        denied.Remove(locked);
        SelectTreeNode(viewModel, _project);

        await viewModel.RescanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ScanErrors.Select(error => error.Path), Is.EqualTo(new[] { otherLocked }));
            Assert.That(viewModel.AccessGuidance.InaccessiblePathCount, Is.EqualTo(1));
        });

        denied.Remove(otherLocked);
        SelectTreeNode(viewModel, _other);
        await viewModel.RescanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ScanErrors, Is.Empty);
            Assert.That(viewModel.AccessGuidance.Status, Is.EqualTo(AccessGuidanceStatus.None));
            Assert.That(
                ScanCompletenessClassifier.Classify(viewModel.AccessGuidance, viewModel.ScanErrors),
                Is.EqualTo(ScanCompleteness.Complete));
        });
    }

    [Test]
    public async Task ACompletedFolderRescanRecordsAWholeRootSnapshotWithItsProvenance()
    {
        var store = new RecordingScanHistoryStore();
        var viewModel = CreateViewModel(new ControlledScanner(), store, historyEnabled: true);
        await ScanAsync(viewModel);
        await WriteFileAsync(Path.Combine(_project, "added.bin"), 30);
        SelectTreeNode(viewModel, _project);
        _now = ScanTime.AddMinutes(15);

        await viewModel.RescanFolderCommand.ExecuteAsync(null);

        var metadata = store.Requests[^1].Metadata;
        Assert.Multiple(() =>
        {
            Assert.That(store.Requests, Has.Count.EqualTo(2));
            Assert.That(metadata.RootPath, Is.EqualTo(_root));
            Assert.That(metadata.ScanCompletedAt, Is.EqualTo(ScanTime));
            Assert.That(metadata.CapturedAt, Is.EqualTo(ScanTime.AddMinutes(15)));
            Assert.That(
                metadata.RefreshedFolders,
                Is.EqualTo(new[] { new RefreshedFolder(_project, ScanTime.AddMinutes(15)) }));
            Assert.That(metadata.TotalCountedSizeBytes, Is.EqualTo(200));
            Assert.That(store.RowCounts[^1], Is.EqualTo(store.RowCounts[0] + 1));
            Assert.That(store.Requests[0].Metadata.RefreshedFolders, Is.Empty);
        });
    }

    [Test]
    public async Task TheFolderRescanCommandRequiresACompletedResultAndASelectedDirectory()
    {
        var viewModel = CreateViewModel(new ControlledScanner());

        Assert.That(viewModel.RescanFolderCommand.CanExecute(null), Is.False);

        await ScanAsync(viewModel);
        viewModel.SelectedLargeFile = Find(viewModel, Path.Combine(_project, "a.bin"));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSelectedItemDirectory, Is.False);
            Assert.That(viewModel.RescanFolderCommand.CanExecute(null), Is.False);
        });

        SelectTreeNode(viewModel, _project);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsSelectedItemDirectory, Is.True);
            Assert.That(viewModel.RescanFolderCommand.CanExecute(null), Is.True);
            Assert.That(viewModel.RescanSelectedFolderAccessibleName, Is.EqualTo("Rescan folder project"));
        });
    }

    [Test]
    public async Task TheFolderRescanCommandIsUnavailableWhileScanningOrRestartingForAnUpdate()
    {
        var scanner = new ControlledScanner();
        var viewModel = CreateViewModel(scanner);
        await ScanAsync(viewModel);
        SelectTreeNode(viewModel, _project);
        var started = new TaskCompletionSource();
        var availableWhileRunning = true;
        scanner.BeforeSubtreeCompletes = async cancellationToken =>
        {
            availableWhileRunning = viewModel.RescanFolderCommand.CanExecute(null)
                || viewModel.RescanCommand.CanExecute(null);
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        };

        var rescan = viewModel.RescanFolderCommand.ExecuteAsync(null);
        await started.Task;
        viewModel.StopScanCommand.Execute(null);
        await rescan;
        await viewModel.PrepareForUpdateRestartAsync();

        Assert.Multiple(() =>
        {
            Assert.That(availableWhileRunning, Is.False);
            Assert.That(viewModel.RescanFolderCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public async Task AnExportBlocksAFolderRescan()
    {
        var viewModel = CreateViewModel(new ControlledScanner());
        await ScanAsync(viewModel);
        SelectTreeNode(viewModel, _project);

        viewModel.IsExporting = true;

        Assert.That(viewModel.RescanFolderCommand.CanExecute(null), Is.False);
    }

    [Test]
    public async Task AFolderRescanBlocksMovingToTrashAndBasketOperations()
    {
        var scanner = new ScriptedScanner(SyntheticTree())
        {
            SubtreeReplacement = SyntheticFolder("/scan/project", ("a.bin", 100))
        };
        var viewModel = CreateViewModel(scanner);
        await viewModel.ScanFolderCommand.ExecuteAsync(null);
        AddToBasket(viewModel, "/scan/other/b.bin");
        SelectTreeNode(viewModel, "/scan/project");
        var started = new TaskCompletionSource();
        var cleanupAvailable = true;
        scanner.BeforeSubtreeCompletes = async cancellationToken =>
        {
            cleanupAvailable = viewModel.MoveToTrashCommand.CanExecute(null)
                || viewModel.MoveCleanupBasketToTrashCommand.CanExecute(null)
                || viewModel.MoveCleanupBasketToLocationCommand.CanExecute(null)
                || viewModel.CopyCleanupBasketToLocationCommand.CanExecute(null);
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
        };

        var rescan = viewModel.RescanFolderCommand.ExecuteAsync(null);
        await started.Task;
        viewModel.StopScanCommand.Execute(null);
        await rescan;

        Assert.Multiple(() =>
        {
            Assert.That(cleanupAvailable, Is.False);
            Assert.That(viewModel.MoveToTrashCommand.CanExecute(null), Is.True);
            Assert.That(viewModel.MoveCleanupBasketToTrashCommand.CanExecute(null), Is.True);
        });
    }

    [TestCase(ScanExportFormat.Csv, true)]
    [TestCase(ScanExportFormat.Json, false)]
    public async Task ACsvExportOfARefreshedResultReportsTheMixedMeasurementTimes(
        ScanExportFormat format,
        bool expectsDisclosure)
    {
        var destination = Path.Combine(_root, $"export.{format.ToString().ToLowerInvariant()}");
        var picker = Substitute.For<ISaveFilePickerService>();
        picker.SelectSaveFileAsync(Arg.Any<ScanExportFormat>(), Arg.Any<string>())
            .Returns(destination);
        var viewModel = CreateViewModel(new ControlledScanner(), savePicker: picker);
        await ScanAsync(viewModel);
        SelectTreeNode(viewModel, _project);
        await viewModel.RescanFolderCommand.ExecuteAsync(null);

        if (format == ScanExportFormat.Csv)
        {
            await viewModel.ExportCsvCommand.ExecuteAsync(null);
        }
        else
        {
            await viewModel.ExportJsonCommand.ExecuteAsync(null);
        }

        var disclosure = "This result mixes measurement times, because 1 folder was rescanned "
            + "after the original scan. The JSON export lists the rescanned folders.";
        Assert.That(
            viewModel.ExportStatusMessage,
            expectsDisclosure ? Does.Contain(disclosure) : Does.Not.Contain(disclosure));
        if (format == ScanExportFormat.Json)
        {
            Assert.That(await File.ReadAllTextAsync(destination), Does.Contain(_project));
            Assert.That(await File.ReadAllTextAsync(destination), Does.Contain("\"refreshedFolders\""));
        }
    }

    [Test]
    public async Task ShiftCommandRRescansTheSelectedFolder()
    {
        var viewModel = CreateViewModel(new ControlledScanner());
        await ScanAsync(viewModel);
        await WriteFileAsync(Path.Combine(_project, "added.bin"), 30);
        SelectTreeNode(viewModel, _project);
        var keyEvent = KeyDown(Key.R, KeyModifiers.Meta | KeyModifiers.Shift, new Border());

        var handled = MainWindow.TryHandleSelectedItemShortcut(viewModel, keyEvent);
        await viewModel.RescanFolderCommand.ExecutionTask!;

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(keyEvent.Handled, Is.True);
            Assert.That(Find(viewModel, _project).SizeBytes, Is.EqualTo(150));
        });
    }

    [Test]
    public async Task ShiftCommandRIsIgnoredWhileATextFieldHasFocus()
    {
        var scanner = new ControlledScanner();
        var viewModel = CreateViewModel(scanner);
        await ScanAsync(viewModel);
        SelectTreeNode(viewModel, _project);
        var keyEvent = KeyDown(Key.R, KeyModifiers.Meta | KeyModifiers.Shift, new TextBox());

        var handled = MainWindow.TryHandleSelectedItemShortcut(viewModel, keyEvent);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.False);
            Assert.That(scanner.SubtreeRequests, Is.Empty);
        });
    }

    [Test]
    public async Task ShiftCommandRIsIgnoredForAFile()
    {
        var scanner = new ControlledScanner();
        var viewModel = CreateViewModel(scanner);
        await ScanAsync(viewModel);
        viewModel.SelectedLargeFile = Find(viewModel, Path.Combine(_project, "a.bin"));
        var keyEvent = KeyDown(Key.R, KeyModifiers.Meta | KeyModifiers.Shift, new Border());

        var handled = MainWindow.TryHandleSelectedItemShortcut(viewModel, keyEvent);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.False);
            Assert.That(scanner.SubtreeRequests, Is.Empty);
        });
    }

    private static KeyEventArgs KeyDown(Key key, KeyModifiers modifiers, Control source) =>
        new()
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = modifiers,
            Source = source
        };

    private MainWindowViewModel CreateViewModel(
        IDiskScanner scanner,
        IScanHistoryStore? store = null,
        bool historyEnabled = false,
        IFolderRescanConfirmationService? confirmation = null,
        ISaveFilePickerService? savePicker = null)
    {
        var viewModel = new MainWindowViewModel(
            new NullFolderPickerService(),
            scanner,
            new ImmediateDispatcher(),
            saveFilePickerService: savePicker,
            scanHistoryStore: store,
            searchDebounceInterval: TimeSpan.Zero,
            referenceTimeProvider: () => _now,
            folderRescanConfirmationService: confirmation)
        {
            SelectedFolderPath = scanner is ScriptedScanner ? "/scan" : _root,
            MeasurementMode = StorageMeasurementMode.Logical,
            IncludeHiddenFiles = false,
            FollowSymbolicLinks = false,
            ScanHistoryEnabled = historyEnabled
        };
        return viewModel;
    }

    private async Task ScanAsync(MainWindowViewModel viewModel)
    {
        _now = ScanTime;
        await viewModel.ScanFolderCommand.ExecuteAsync(null);
    }

    private static DiskItem RootItem(MainWindowViewModel viewModel) =>
        viewModel.TreeItems[0].Item;

    private static DiskItem Find(MainWindowViewModel viewModel, string path) =>
        ScanResultMerger.FindByPath(RootItem(viewModel), path)
        ?? throw new InvalidOperationException($"{path} is not in the result.");

    private static DiskItemTreeNodeViewModel Node(MainWindowViewModel viewModel, string path)
    {
        var node = viewModel.TreeItems[0];
        while (!string.Equals(node.Item.Path, path, StringComparison.Ordinal))
        {
            node = node.Children.Single(child =>
                string.Equals(child.Item.Path, path, StringComparison.Ordinal)
                || path.StartsWith(child.Item.Path + Path.DirectorySeparatorChar, StringComparison.Ordinal));
        }

        return node;
    }

    private static void SelectTreeNode(MainWindowViewModel viewModel, string path) =>
        viewModel.SelectedTreeItem = Node(viewModel, path);

    private static void AddToBasket(MainWindowViewModel viewModel, string path)
    {
        viewModel.SelectedLargeFile = Find(viewModel, path);
        viewModel.AddSelectedItemToCleanupBasketCommand.Execute(null);
    }

    private static DiskItem Tree()
    {
        var root = new DiskItem("scan", "/scan", isDirectory: true) { SizeBytes = 10 };
        var project = new DiskItem("project", "/scan/project", isDirectory: true) { SizeBytes = 10 };
        project.AddChild(new DiskItem("a.bin", "/scan/project/a.bin", isDirectory: false) { SizeBytes = 10 });
        root.AddChild(project);
        return root;
    }

    private static DiskItem SyntheticTree()
    {
        var root = new DiskItem("scan", "/scan", isDirectory: true);
        var project = SyntheticFolder("/scan/project", ("a.bin", 100), ("gone.bin", 5));
        var other = SyntheticFolder("/scan/other", ("b.bin", 50));
        root.AddChild(project);
        root.AddChild(other);
        root.SizeBytes = project.SizeBytes + other.SizeBytes;
        return root;
    }

    private static DiskItem SyntheticFolder(string path, params (string Name, long Size)[] files)
    {
        var folder = new DiskItem(Path.GetFileName(path), path, isDirectory: true);
        foreach (var (name, size) in files)
        {
            folder.AddChild(new DiskItem(name, $"{path}/{name}", isDirectory: false) { SizeBytes = size });
        }

        folder.SizeBytes = files.Sum(file => file.Size);
        return folder;
    }

    private static async Task WriteFileAsync(string path, int length)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, new byte[length]);
    }

    private sealed class ControlledScanner(
        Func<string, IEnumerable<string>>? enumerate = null) : IDiskScanner
    {
        private readonly DiskScanner _inner =
            new(enumerate ?? Directory.EnumerateFileSystemEntries);

        public List<ScanOptions?> FullScanOptions { get; } = [];

        public List<(string Root, string Subtree, ScanOptions? Options)> SubtreeRequests { get; } = [];

        public Func<CancellationToken, Task>? BeforeSubtreeCompletes { get; set; }

        public Exception? SubtreeFailure { get; set; }

        public IAsyncEnumerable<ScanProgress> ScanAsync(
            string rootPath,
            ScanOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            FullScanOptions.Add(options);
            return _inner.ScanAsync(rootPath, options, cancellationToken);
        }

        public async IAsyncEnumerable<ScanProgress> ScanSubtreeAsync(
            string scanRootPath,
            string subtreePath,
            ScanOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            SubtreeRequests.Add((scanRootPath, subtreePath, options));
            await foreach (var progress in _inner
                               .ScanSubtreeAsync(scanRootPath, subtreePath, options, cancellationToken)
                               .ConfigureAwait(false))
            {
                if (progress.IsCompleted)
                {
                    if (BeforeSubtreeCompletes is { } hook)
                    {
                        await hook(cancellationToken).ConfigureAwait(false);
                    }

                    if (SubtreeFailure is { } failure)
                    {
                        throw failure;
                    }
                }

                yield return progress;
            }
        }
    }

    private sealed class ScriptedScanner(DiskItem root) : IDiskScanner
    {
        public List<ScanOptions?> FullScanOptions { get; } = [];

        public int SubtreeRequestCount { get; private set; }

        public DiskItem? SubtreeReplacement { get; set; }

        public Func<CancellationToken, Task>? BeforeSubtreeCompletes { get; set; }

        public async IAsyncEnumerable<ScanProgress> ScanAsync(
            string rootPath,
            ScanOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            FullScanOptions.Add(options);
            await Task.Yield();
            yield return new ScanProgress(
                rootPath,
                FilesScanned: 1,
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
            SubtreeRequestCount++;
            await Task.Yield();
            var placeholder = new DiskItem(Path.GetFileName(subtreePath), subtreePath, isDirectory: true);
            yield return new ScanProgress(subtreePath, 0, 1, 0, placeholder, Errors: []);

            if (BeforeSubtreeCompletes is { } hook)
            {
                await hook(cancellationToken).ConfigureAwait(false);
            }

            yield return new ScanProgress(
                subtreePath,
                FilesScanned: 1,
                DirectoriesScanned: 1,
                BytesScanned: SubtreeReplacement?.SizeBytes ?? 0,
                SubtreeReplacement ?? placeholder,
                Errors: [],
                IsCompleted: true,
                options?.MeasurementMode ?? StorageMeasurementMode.Logical)
            {
                IsRootPresent = SubtreeReplacement is not null
            };
        }
    }

    private sealed class RecordingScanHistoryStore : IScanHistoryStore
    {
        public string Location => "/tmp/history";

        public List<ScanSnapshotRequest> Requests { get; } = [];

        public List<int> RowCounts { get; } = [];

        public Task<ScanHistoryCaptureResult> CaptureAsync(
            ScanSnapshotRequest request,
            ScanHistoryLimits limits,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
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
