using System.Runtime.CompilerServices;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using NSubstitute;
using MacStorageAtlas.Core.Cleanup;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Platform;
using MacStorageAtlas.Core.Relocation;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.App.Tests;

public class MainWindowViewModelRelocationTests
{
    private const string Destination = "/Volumes/Archive";

    [Test]
    public async Task SwitchingBetweenOperationsLeavesBasketContentsUnchanged()
    {
        var context = await ScannedContextAsync(confirmReview: false);

        await context.ViewModel.MoveCleanupBasketToTrashCommand.ExecuteAsync(null);
        var afterTrash = context.ViewModel.CleanupBasketItems;
        await context.ViewModel.MoveCleanupBasketToLocationCommand.ExecuteAsync(null);
        var afterMove = context.ViewModel.CleanupBasketItems;
        await context.ViewModel.CopyCleanupBasketToLocationCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(afterTrash, Has.Count.EqualTo(1));
            Assert.That(afterMove, Has.Count.EqualTo(1));
            Assert.That(context.ViewModel.CleanupBasketItems, Has.Count.EqualTo(1));
            Assert.That(context.Relocation.Operations, Is.Empty);
        });
    }

    [Test]
    public async Task CancelledDestinationSelectionLeavesEverythingUnchanged()
    {
        var context = await ScannedContextAsync(
            confirmReview: true,
            selectedFolderPath: null);

        await context.ViewModel.MoveCleanupBasketToLocationCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(
                context.ViewModel.CleanupBasketStatusMessage,
                Is.EqualTo("Destination selection cancelled."));
            Assert.That(context.Review.ReviewCount, Is.Zero);
            Assert.That(context.Relocation.Operations, Is.Empty);
            Assert.That(context.ViewModel.CleanupBasketItems, Has.Count.EqualTo(1));
            Assert.That(context.Root.Children, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task BlockedDestinationStopsBeforeReviewAndTransfer()
    {
        var context = await ScannedContextAsync(
            confirmReview: true,
            probe: new FakeDestinationProbe { DestinationExists = false });

        await context.ViewModel.MoveCleanupBasketToLocationCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(
                context.ViewModel.CleanupBasketStatusMessage,
                Does.Contain("no longer exists"));
            Assert.That(context.Review.ReviewCount, Is.Zero);
            Assert.That(context.Relocation.Operations, Is.Empty);
            Assert.That(context.Root.Children, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task ReadOnlyDestinationStopsBeforeReviewAndTransfer()
    {
        var context = await ScannedContextAsync(
            confirmReview: true,
            probe: new FakeDestinationProbe { IsWritable = false });

        await context.ViewModel.CopyCleanupBasketToLocationCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(
                context.ViewModel.CleanupBasketStatusMessage,
                Does.Contain("cannot be written"));
            Assert.That(context.Review.ReviewCount, Is.Zero);
            Assert.That(context.Relocation.Operations, Is.Empty);
        });
    }

    [Test]
    public async Task InsufficientFreeSpaceStopsBeforeReviewAndTransfer()
    {
        var context = await ScannedContextAsync(
            confirmReview: true,
            probe: new FakeDestinationProbe
            {
                FreeSpace = RelocationFreeSpace.FromBytes(1)
            });

        await context.ViewModel.MoveCleanupBasketToLocationCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(
                context.ViewModel.CleanupBasketStatusMessage,
                Does.Contain("free space"));
            Assert.That(context.Review.ReviewCount, Is.Zero);
            Assert.That(context.Relocation.Operations, Is.Empty);
        });
    }

    [Test]
    public async Task CancelledMoveReviewPerformsNoTransfer()
    {
        var context = await ScannedContextAsync(confirmReview: false);

        await context.ViewModel.MoveCleanupBasketToLocationCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(context.Review.ReviewCount, Is.EqualTo(1));
            Assert.That(
                context.ViewModel.CleanupBasketStatusMessage,
                Is.EqualTo("Move cancelled."));
            Assert.That(context.Relocation.Operations, Is.Empty);
            Assert.That(context.ViewModel.CleanupBasketItems, Has.Count.EqualTo(1));
            Assert.That(context.Root.Children, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task CancelledCopyReviewPerformsNoTransfer()
    {
        var context = await ScannedContextAsync(confirmReview: false);

        await context.ViewModel.CopyCleanupBasketToLocationCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(
                context.ViewModel.CleanupBasketStatusMessage,
                Is.EqualTo("Copy cancelled."));
            Assert.That(context.Relocation.Operations, Is.Empty);
        });
    }

    [Test]
    public async Task SuccessfulMoveTransfersEachItemAndUpdatesTheDisplayedResult()
    {
        var context = await ScannedContextAsync(confirmReview: true);

        await context.ViewModel.MoveCleanupBasketToLocationCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(context.Relocation.Operations, Has.Count.EqualTo(1));
            Assert.That(context.Relocation.Operations[0].IsCopy, Is.False);
            Assert.That(
                context.Relocation.Operations[0].SourcePath,
                Is.EqualTo("/scan/root/file.bin"));
            Assert.That(
                context.Relocation.Operations[0].DestinationPath,
                Is.EqualTo(Destination));
            Assert.That(context.ViewModel.CleanupBasketSucceededCount, Is.EqualTo(1));
            Assert.That(
                context.ViewModel.CleanupBasketStatusMessage,
                Does.Contain("Moved 1 item(s) to the destination."));
            Assert.That(context.Root.Children, Has.Count.EqualTo(1));
            Assert.That(context.ViewModel.CleanupBasketItems, Is.Empty);
        });
    }

    [Test]
    public async Task SuccessfulCopyLeavesTheResultBasketAndTotalsUnchanged()
    {
        var context = await ScannedContextAsync(confirmReview: true);
        var totalBefore = context.ViewModel.CleanupBasketTotalLogicalSize;

        await context.ViewModel.CopyCleanupBasketToLocationCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(context.Relocation.Operations, Has.Count.EqualTo(1));
            Assert.That(context.Relocation.Operations[0].IsCopy, Is.True);
            Assert.That(context.ViewModel.CleanupBasketSucceededCount, Is.EqualTo(1));
            Assert.That(
                context.ViewModel.CleanupBasketStatusMessage,
                Does.Contain("Copied 1 item(s) to the destination."));
            Assert.That(context.Root.Children, Has.Count.EqualTo(2));
            Assert.That(context.ViewModel.CleanupBasketItems, Has.Count.EqualTo(1));
            Assert.That(
                context.ViewModel.CleanupBasketTotalLogicalSize,
                Is.EqualTo(totalBefore));
        });
    }

    [Test]
    public async Task SharedAwareResultRescansAfterASuccessfulMove()
    {
        var context = await ScannedContextAsync(
            confirmReview: true,
            measurementMode: StorageMeasurementMode.SharedAwareAllocated);

        await context.ViewModel.MoveCleanupBasketToLocationCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(context.Scanner.ScanCount, Is.EqualTo(2));
            Assert.That(context.ViewModel.CleanupBasketSucceededCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SharedAwareResultDoesNotRescanAfterASuccessfulCopy()
    {
        var context = await ScannedContextAsync(
            confirmReview: true,
            measurementMode: StorageMeasurementMode.SharedAwareAllocated);

        await context.ViewModel.CopyCleanupBasketToLocationCommand.ExecuteAsync(null);

        Assert.That(context.Scanner.ScanCount, Is.EqualTo(1));
    }

    [Test]
    public async Task PartialFailureKeepsSucceededItemsAndReportsTheFailure()
    {
        var context = await ScannedContextAsync(
            confirmReview: true,
            addSecondItem: true,
            relocation: new RecordingRelocationService(
                (sourcePath, _) => sourcePath.EndsWith("movie.mov", StringComparison.Ordinal)
                    ? throw new InvalidOperationException("The destination rejected the item.")
                    : Task.CompletedTask));

        await context.ViewModel.MoveCleanupBasketToLocationCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.CleanupBasketSucceededCount, Is.EqualTo(1));
            Assert.That(context.ViewModel.CleanupBasketFailedCount, Is.EqualTo(1));
            Assert.That(
                context.ViewModel.CleanupBasketStatusMessage,
                Does.Contain("Failed: 1"));
            Assert.That(context.ViewModel.CleanupBasketItems, Has.Count.EqualTo(1));
            Assert.That(
                context.ViewModel.CleanupBasketItems[0].Snapshot.Name,
                Is.EqualTo("movie.mov"));
        });
    }

    [Test]
    public async Task CancellationDuringRelocationReportsRemainingItemsAsUnattempted()
    {
        MainWindowViewModel? viewModel = null;
        var context = await ScannedContextAsync(
            confirmReview: true,
            addSecondItem: true,
            relocation: new RecordingRelocationService((_, cancellationToken) =>
            {
                viewModel?.CancelCleanupBasketMoveCommand.Execute(null);
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            }));
        viewModel = context.ViewModel;

        await context.ViewModel.MoveCleanupBasketToLocationCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.CleanupBasketSucceededCount, Is.Zero);
            Assert.That(context.ViewModel.CleanupBasketUnattemptedCount, Is.EqualTo(2));
            Assert.That(context.Root.Children, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task AllItemsBlockedByCollisionStopsBeforeTransfer()
    {
        var context = await ScannedContextAsync(
            confirmReview: true,
            probe: new FakeDestinationProbe
            {
                CollidingPaths = ["/Volumes/Archive/file.bin"]
            });

        await context.ViewModel.MoveCleanupBasketToLocationCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(
                context.ViewModel.CleanupBasketStatusMessage,
                Does.Contain("ready to move to the destination"));
            Assert.That(context.Review.ReviewCount, Is.Zero);
            Assert.That(context.Relocation.Operations, Is.Empty);
            Assert.That(
                context.ViewModel.CleanupBasketPreflightResults[0].Status.Kind,
                Is.EqualTo(CleanupPreflightStatusKind.DestinationCollision));
        });
    }

    [Test]
    public async Task MoveReviewReceivesTheOperationDestinationAndReclaimedSize()
    {
        var context = await ScannedContextAsync(confirmReview: false);

        await context.ViewModel.MoveCleanupBasketToLocationCommand.ExecuteAsync(null);

        var review = context.Review.LastReview;
        Assert.That(review, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(review!.Operation, Is.EqualTo(CleanupOperationKind.Move));
            Assert.That(review.Destination!.Path, Is.EqualTo(Destination));
            Assert.That(review.ExpectedReclaimedSizeBytes, Is.EqualTo(4096));
            Assert.That(review.OperationTitle, Does.Contain("Move items"));
            Assert.That(review.OperationDescription, Does.Not.Contain("Trash"));
        });
    }

    [Test]
    public async Task CopyReviewReportsZeroReclaimedSize()
    {
        var context = await ScannedContextAsync(confirmReview: false);

        await context.ViewModel.CopyCleanupBasketToLocationCommand.ExecuteAsync(null);

        var review = context.Review.LastReview;
        Assert.That(review, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(review!.Operation, Is.EqualTo(CleanupOperationKind.Copy));
            Assert.That(review.Destination!.Path, Is.EqualTo(Destination));
            Assert.That(review.ExpectedReclaimedSizeBytes, Is.Zero);
            Assert.That(review.Summary.TotalLogicalSizeBytes, Is.EqualTo(1024));
            Assert.That(review.ConfirmButtonText, Is.EqualTo("Copy Items"));
        });
    }

    [Test]
    public async Task TrashReviewStillDescribesTheTrashOperation()
    {
        var context = await ScannedContextAsync(confirmReview: false);

        await context.ViewModel.MoveCleanupBasketToTrashCommand.ExecuteAsync(null);

        var review = context.Review.LastReview;
        Assert.That(review, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(review!.Operation, Is.EqualTo(CleanupOperationKind.Trash));
            Assert.That(review.Destination, Is.Null);
            Assert.That(review.OperationTitle, Does.Contain("Trash"));
            Assert.That(review.ConfirmButtonText, Is.EqualTo("Move to Trash"));
        });
    }

    [Test]
    public async Task RelocationExposesTheDestinationAndClearsProgressWhenFinished()
    {
        var context = await ScannedContextAsync(confirmReview: true);

        await context.ViewModel.MoveCleanupBasketToLocationCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(
                context.ViewModel.CleanupBasketDestinationPath,
                Is.EqualTo(Destination));
            Assert.That(context.ViewModel.CleanupBasketProgressMessage, Is.Null);
            Assert.That(context.ViewModel.IsRunningCleanupBasketOperation, Is.False);
        });
    }

    [Test]
    public async Task RelocationReportsPerItemProgressWhileRunning()
    {
        var progressMessages = new List<string?>();
        var context = await ScannedContextAsync(
            confirmReview: true,
            addSecondItem: true,
            relocation: new RecordingRelocationService((_, _) => Task.CompletedTask));
        context.Relocation.OnTransfer = () =>
            progressMessages.Add(context.ViewModel.CleanupBasketProgressMessage);

        await context.ViewModel.MoveCleanupBasketToLocationCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(progressMessages, Has.Count.EqualTo(2));
            Assert.That(progressMessages[0], Does.Contain("file.bin"));
            Assert.That(progressMessages[0], Does.Contain("0 of 2"));
            Assert.That(progressMessages[1], Does.Contain("movie.mov"));
            Assert.That(progressMessages[1], Does.Contain("1 of 2"));
        });
    }

    private static async Task<RelocationContext> ScannedContextAsync(
        bool confirmReview,
        string? selectedFolderPath = Destination,
        bool addSecondItem = false,
        StorageMeasurementMode measurementMode = StorageMeasurementMode.Allocated,
        FakeDestinationProbe? probe = null,
        RecordingRelocationService? relocation = null)
    {
        var root = BasketRoot();
        var scanner = new CountingDiskScanner(root, measurementMode);
        var review = new FakeCleanupBasketReviewService(confirmReview);
        var relocationService = relocation ?? new RecordingRelocationService();
        var folderPicker = Substitute.For<IFolderPickerService>();
        folderPicker.SelectFolderAsync().Returns(Task.FromResult(selectedFolderPath));

        var viewModel = new MainWindowViewModel(
            folderPicker,
            scanner,
            new ImmediateUiDispatcher(),
            Substitute.For<IFileRevealService>(),
            Substitute.For<ITrashService>(),
            Substitute.For<ITrashConfirmationService>(),
            cleanupBasketReviewService: review,
            cleanupFileSystemMetadataReader: new FakeCleanupMetadataReader(
                Snapshot(root.Children[0]),
                Snapshot(root.Children[1])),
            itemRelocationService: relocationService,
            relocationDestinationProbe: probe ?? new FakeDestinationProbe())
        {
            SelectedFolderPath = root.Path
        };

        await viewModel.ScanFolderCommand.ExecuteAsync(null);
        viewModel.SelectedTreeItem = viewModel.TreeItems.Single().Children[0];
        viewModel.AddSelectedItemToCleanupBasketCommand.Execute(null);

        if (addSecondItem)
        {
            viewModel.SelectedTreeItem = viewModel.TreeItems.Single().Children[1];
            viewModel.AddSelectedItemToCleanupBasketCommand.Execute(null);
        }

        return new RelocationContext(
            viewModel,
            root,
            scanner,
            review,
            relocationService);
    }

    private static CleanupFileSystemSnapshot Snapshot(DiskItem item) =>
        new(item.Path, item.IsDirectory, item.SizeBytes, item.MeasuredSizeBytes);

    private static DiskItem BasketRoot()
    {
        var root = new DiskItem("root", "/scan/root", isDirectory: true)
        {
            SizeBytes = 3072,
            MeasuredSizeBytes = 8192
        };
        root.AddChild(new DiskItem("file.bin", "/scan/root/file.bin", isDirectory: false)
        {
            SizeBytes = 1024,
            MeasuredSizeBytes = 4096
        });
        root.AddChild(new DiskItem("movie.mov", "/scan/root/movie.mov", isDirectory: false)
        {
            SizeBytes = 2048,
            MeasuredSizeBytes = 4096
        });
        return root;
    }

    private sealed record RelocationContext(
        MainWindowViewModel ViewModel,
        DiskItem Root,
        CountingDiskScanner Scanner,
        FakeCleanupBasketReviewService Review,
        RecordingRelocationService Relocation);

    private sealed record RelocationOperation(
        string SourcePath,
        string DestinationPath,
        bool IsCopy);

    private sealed class RecordingRelocationService : IItemRelocationService
    {
        private readonly Func<string, CancellationToken, Task> _transfer;

        public RecordingRelocationService()
            : this((_, _) => Task.CompletedTask)
        {
        }

        public RecordingRelocationService(Func<string, CancellationToken, Task> transfer)
        {
            _transfer = transfer;
        }

        public List<RelocationOperation> Operations { get; } = [];

        public Action? OnTransfer { get; set; }

        public Task MoveAsync(
            string sourcePath,
            string destinationDirectoryPath,
            CancellationToken cancellationToken = default) =>
            RecordAsync(sourcePath, destinationDirectoryPath, isCopy: false, cancellationToken);

        public Task CopyAsync(
            string sourcePath,
            string destinationDirectoryPath,
            CancellationToken cancellationToken = default) =>
            RecordAsync(sourcePath, destinationDirectoryPath, isCopy: true, cancellationToken);

        private Task RecordAsync(
            string sourcePath,
            string destinationDirectoryPath,
            bool isCopy,
            CancellationToken cancellationToken)
        {
            OnTransfer?.Invoke();
            Operations.Add(
                new RelocationOperation(sourcePath, destinationDirectoryPath, isCopy));
            return _transfer(sourcePath, cancellationToken);
        }
    }

    private sealed class FakeDestinationProbe : IRelocationDestinationProbe
    {
        public bool DestinationExists { get; init; } = true;

        public bool IsWritable { get; init; } = true;

        public RelocationFreeSpace FreeSpace { get; init; } =
            RelocationFreeSpace.FromBytes(long.MaxValue);

        public IReadOnlyList<string> CollidingPaths { get; init; } = [];

        public bool Exists(string path) =>
            string.Equals(path, Destination, StringComparison.Ordinal)
                ? DestinationExists
                : CollidingPaths.Contains(path, StringComparer.Ordinal);

        public bool IsDirectory(string path) => DestinationExists;

        bool IRelocationDestinationProbe.IsWritable(string path) => IsWritable;

        public RelocationFreeSpace GetFreeSpace(string path) => FreeSpace;
    }

    private sealed class FakeCleanupBasketReviewService(bool confirm)
        : ICleanupBasketReviewService
    {
        public int ReviewCount { get; private set; }

        public CleanupBasketReview? LastReview { get; private set; }

        public Task<bool> ConfirmCleanupAsync(CleanupBasketReview review)
        {
            ReviewCount++;
            LastReview = review;
            return Task.FromResult(confirm);
        }
    }

    private sealed class FakeCleanupMetadataReader(params CleanupFileSystemSnapshot[] snapshots)
        : ICleanupFileSystemMetadataReader
    {
        private readonly Dictionary<string, CleanupFileSystemSnapshot> _snapshots =
            snapshots.ToDictionary(
                snapshot => CleanupProtectedPathPolicy.NormalizePath(snapshot.Path),
                StringComparer.Ordinal);

        public bool TryReadSnapshot(string path, out CleanupFileSystemSnapshot snapshot) =>
            _snapshots.TryGetValue(
                CleanupProtectedPathPolicy.NormalizePath(path),
                out snapshot!);
    }

    private sealed class CountingDiskScanner(
        DiskItem root,
        StorageMeasurementMode measurementMode) : IDiskScanner
    {
        public int ScanCount { get; private set; }

        public IAsyncEnumerable<ScanProgress> ScanAsync(
            string rootPath,
            ScanOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ScanCount++;
            return CompletedScanAsync(cancellationToken);
        }

        private async IAsyncEnumerable<ScanProgress> CompletedScanAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return new ScanProgress(
                root.Path,
                FilesScanned: 2,
                DirectoriesScanned: 1,
                BytesScanned: root.SizeBytes,
                root,
                Errors: [],
                IsCompleted: true,
                MeasurementMode: measurementMode,
                CloneAccountingCoverage: CloneAccountingCoverage.Unavailable);
        }
    }

    private sealed class ImmediateUiDispatcher : IUiDispatcher
    {
        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
    }
}
