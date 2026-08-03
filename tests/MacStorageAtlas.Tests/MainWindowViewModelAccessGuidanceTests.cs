using System.Runtime.CompilerServices;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.Tests;

public class MainWindowViewModelAccessGuidanceTests
{
    [Test]
    public async Task CompletedScanWithPermissionErrorsShowsAccessGuidanceAndKeepsScanErrors()
    {
        var error = new ScanError(
            "/Users/test/Library/Mail",
            "Access denied.",
            nameof(UnauthorizedAccessException));
        var root = CreateRoot();
        var accessService = new FakeFullDiskAccessService
        {
            Assessment = new FullDiskAccessAssessment(FullDiskAccessStatus.LikelyMissing)
        };
        var viewModel = CreateViewModel(root, [error], accessService);
        viewModel.SelectedFolderPath = root.Path;

        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsAccessGuidanceVisible, Is.True);
            Assert.That(
                viewModel.AccessGuidanceStatus,
                Is.EqualTo(AccessGuidanceStatus.LikelyMissingFullDiskAccess));
            Assert.That(viewModel.InaccessiblePathCount, Is.EqualTo(1));
            Assert.That(viewModel.AccessGuidanceMessage, Does.Contain("1 path was inaccessible"));
            Assert.That(viewModel.AccessGuidanceMessage, Does.Not.Contain("purgeable"));
            Assert.That(viewModel.AccessGuidanceMessage, Does.Not.Contain("safe to delete"));
            Assert.That(viewModel.ScanErrors, Is.EqualTo(new[] { error }));
            Assert.That(viewModel.BytesScanned, Is.EqualTo(root.SizeBytes));
            Assert.That(
                viewModel.ResultMeasurementMode,
                Is.EqualTo(StorageMeasurementMode.SharedAwareAllocated));
        });
    }

    [Test]
    public async Task CompletedScanWithoutErrorsDoesNotShowAccessGuidance()
    {
        var root = CreateRoot();
        var accessService = new FakeFullDiskAccessService
        {
            Assessment = FullDiskAccessAssessment.NotApplicable
        };
        var viewModel = CreateViewModel(root, [], accessService);
        viewModel.SelectedFolderPath = root.Path;

        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsAccessGuidanceVisible, Is.False);
            Assert.That(viewModel.AccessGuidanceStatus, Is.EqualTo(AccessGuidanceStatus.None));
            Assert.That(viewModel.OpenFullDiskAccessSettingsCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.RescanAfterFullDiskAccessCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public async Task IndeterminateAccessShowsGuidanceWithoutClaimingGrantedOrDenied()
    {
        var root = CreateRoot();
        var accessService = new FakeFullDiskAccessService
        {
            Assessment = FullDiskAccessAssessment.Indeterminate
        };
        var viewModel = CreateViewModel(root, [], accessService);
        viewModel.SelectedFolderPath = root.Path;

        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.AccessGuidanceStatus, Is.EqualTo(AccessGuidanceStatus.Indeterminate));
            Assert.That(viewModel.AccessGuidanceMessage, Does.Contain("cannot confirm"));
            Assert.That(viewModel.AccessGuidanceMessage, Does.Not.Contain("granted"));
            Assert.That(viewModel.AccessGuidanceMessage, Does.Not.Contain("denied"));
        });
    }

    [Test]
    public async Task OpenFullDiskAccessSettingsKeepsGuidanceWhenSettingsOpenDirectly()
    {
        var error = new ScanError(
            "/scan/root/restricted",
            "Access denied.",
            nameof(UnauthorizedAccessException));
        var root = CreateRoot();
        var accessService = new FakeFullDiskAccessService
        {
            Assessment = new FullDiskAccessAssessment(FullDiskAccessStatus.LikelyMissing),
            SettingsResult = FullDiskAccessSettingsResult.OpenedDirectly
        };
        var viewModel = CreateViewModel(root, [error], accessService);
        viewModel.SelectedFolderPath = root.Path;
        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        viewModel.OpenFullDiskAccessSettingsCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(accessService.OpenSettingsCount, Is.EqualTo(1));
            Assert.That(
                viewModel.AccessGuidanceStatus,
                Is.EqualTo(AccessGuidanceStatus.LikelyMissingFullDiskAccess));
            Assert.That(viewModel.ShowFullDiskAccessManualFallback, Is.False);
        });
    }

    [Test]
    public async Task OpenFullDiskAccessSettingsShowsManualFallbackWhenSettingsUseFallback()
    {
        var error = new ScanError(
            "/scan/root/restricted",
            "Access denied.",
            nameof(UnauthorizedAccessException));
        var root = CreateRoot();
        var accessService = new FakeFullDiskAccessService
        {
            Assessment = new FullDiskAccessAssessment(FullDiskAccessStatus.LikelyMissing),
            SettingsResult = FullDiskAccessSettingsResult.OpenedFallback
        };
        var viewModel = CreateViewModel(root, [error], accessService);
        viewModel.SelectedFolderPath = root.Path;
        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        viewModel.OpenFullDiskAccessSettingsCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ShowFullDiskAccessManualFallback, Is.True);
            Assert.That(
                viewModel.FullDiskAccessManualFallback,
                Is.EqualTo("System Settings > Privacy & Security > Full Disk Access"));
        });
    }

    [Test]
    public async Task OpenFullDiskAccessSettingsReportsFailureAndKeepsCompletedScan()
    {
        var error = new ScanError(
            "/scan/root/restricted",
            "Access denied.",
            nameof(UnauthorizedAccessException));
        var root = CreateRoot();
        var accessService = new FakeFullDiskAccessService
        {
            Assessment = new FullDiskAccessAssessment(FullDiskAccessStatus.LikelyMissing),
            SettingsResult = FullDiskAccessSettingsResult.Failed
        };
        var viewModel = CreateViewModel(root, [error], accessService);
        viewModel.SelectedFolderPath = root.Path;
        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        viewModel.OpenFullDiskAccessSettingsCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(
                viewModel.AccessGuidanceStatus,
                Is.EqualTo(AccessGuidanceStatus.SettingsOpenFailure));
            Assert.That(viewModel.ShowFullDiskAccessManualFallback, Is.True);
            Assert.That(viewModel.HasScanResult, Is.True);
            Assert.That(viewModel.ScanErrors, Is.EqualTo(new[] { error }));
        });
    }

    [Test]
    public async Task RescanAfterFullDiskAccessUsesTheCompletedRootAndOptions()
    {
        var root = CreateRoot();
        var error = new ScanError(
            "/scan/root/restricted",
            "Access denied.",
            nameof(UnauthorizedAccessException));
        var scanner = new CapturingDiskScanner(root, [error]);
        var accessService = new FakeFullDiskAccessService
        {
            Assessment = new FullDiskAccessAssessment(FullDiskAccessStatus.LikelyMissing)
        };
        var viewModel = new MainWindowViewModel(
            new NullFolderPickerService(),
            scanner,
            new ImmediateDispatcher(),
            fullDiskAccessService: accessService,
            searchDebounceInterval: TimeSpan.Zero);
        viewModel.SelectedFolderPath = root.Path;
        viewModel.IncludeHiddenFiles = true;
        viewModel.FollowSymbolicLinks = true;
        viewModel.ExpandApplicationBundles = false;
        viewModel.MeasurementMode = StorageMeasurementMode.Allocated;
        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.That(viewModel.RescanAfterFullDiskAccessCommand.CanExecute(null), Is.True);
        await viewModel.RescanAfterFullDiskAccessCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(scanner.RootPaths, Is.EqualTo(new[] { root.Path, root.Path }));
            Assert.That(scanner.Options, Has.Count.EqualTo(2));
            Assert.That(scanner.Options[1].IncludeHiddenFiles, Is.True);
            Assert.That(scanner.Options[1].FollowSymbolicLinks, Is.True);
            Assert.That(scanner.Options[1].TreatPackagesAsDirectories, Is.False);
            Assert.That(scanner.Options[1].MeasurementMode, Is.EqualTo(StorageMeasurementMode.Allocated));
        });
    }

    [Test]
    public async Task RescanAfterFullDiskAccessCanBeCancelledWithoutAddingAScanError()
    {
        var root = CreateRoot();
        var error = new ScanError(
            "/scan/root/restricted",
            "Access denied.",
            nameof(UnauthorizedAccessException));
        var continueScan = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var progressApplied = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var scanner = new CancellableRescanDiskScanner(
            root,
            [error],
            continueScan.Task,
            progressApplied);
        var accessService = new FakeFullDiskAccessService
        {
            Assessment = new FullDiskAccessAssessment(FullDiskAccessStatus.LikelyMissing)
        };
        var viewModel = new MainWindowViewModel(
            new NullFolderPickerService(),
            scanner,
            new ImmediateDispatcher(),
            fullDiskAccessService: accessService,
            searchDebounceInterval: TimeSpan.Zero);
        viewModel.SelectedFolderPath = root.Path;
        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        var rescanTask = viewModel.RescanAfterFullDiskAccessCommand.ExecuteAsync(null);
        await progressApplied.Task;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsScanning, Is.True);
            Assert.That(viewModel.RescanAfterFullDiskAccessCommand.CanExecute(null), Is.False);
        });

        viewModel.StopScanCommand.Execute(null);
        await rescanTask;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsScanning, Is.False);
            Assert.That(
                viewModel.ScanErrors.Select(scanError => scanError.ExceptionType),
                Does.Not.Contain(nameof(OperationCanceledException)));
        });
    }

    private static MainWindowViewModel CreateViewModel(
        DiskItem root,
        IReadOnlyList<ScanError> errors,
        IFullDiskAccessService fullDiskAccessService) =>
        new(
            new NullFolderPickerService(),
            new StubDiskScanner(cancellationToken => CompletedScanAsync(
                root,
                errors,
                cancellationToken)),
            new ImmediateDispatcher(),
            fullDiskAccessService: fullDiskAccessService,
            searchDebounceInterval: TimeSpan.Zero);

    private static DiskItem CreateRoot()
    {
        var root = new DiskItem("root", "/scan/root", isDirectory: true)
        {
            SizeBytes = 1_024,
            MeasuredSizeBytes = 1_024
        };
        root.AddChild(new DiskItem("file.bin", "/scan/root/file.bin", isDirectory: false)
        {
            SizeBytes = 1_024,
            MeasuredSizeBytes = 1_024
        });
        return root;
    }

    private static async IAsyncEnumerable<ScanProgress> CompletedScanAsync(
        DiskItem root,
        IReadOnlyList<ScanError> errors,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();

        yield return new ScanProgress(
            root.Path,
            FilesScanned: 1,
            DirectoriesScanned: 1,
            BytesScanned: root.SizeBytes,
            root,
            errors,
            IsCompleted: true,
            MeasurementMode: StorageMeasurementMode.SharedAwareAllocated);
    }

    private sealed class StubDiskScanner(
        Func<CancellationToken, IAsyncEnumerable<ScanProgress>> scan) : IDiskScanner
    {
        public IAsyncEnumerable<ScanProgress> ScanAsync(
            string rootPath,
            ScanOptions? options = null,
            CancellationToken cancellationToken = default) => scan(cancellationToken);
    }

    private sealed class CapturingDiskScanner(
        DiskItem root,
        IReadOnlyList<ScanError> firstErrors) : IDiskScanner
    {
        public List<string> RootPaths { get; } = [];

        public List<ScanOptions> Options { get; } = [];

        public IAsyncEnumerable<ScanProgress> ScanAsync(
            string rootPath,
            ScanOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            RootPaths.Add(rootPath);
            Options.Add(options ?? ScanOptions.Default);

            return CompletedScanAsync(
                root,
                RootPaths.Count == 1 ? firstErrors : [],
                cancellationToken);
        }
    }

    private sealed class CancellableRescanDiskScanner(
        DiskItem root,
        IReadOnlyList<ScanError> firstErrors,
        Task continueScan,
        TaskCompletionSource progressApplied) : IDiskScanner
    {
        private int _scanCount;

        public IAsyncEnumerable<ScanProgress> ScanAsync(
            string rootPath,
            ScanOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            _scanCount++;

            return _scanCount == 1
                ? CompletedScanAsync(root, firstErrors, cancellationToken)
                : ProgressThenAwaitCancellationAsync(
                    root,
                    continueScan,
                    progressApplied,
                    cancellationToken);
        }
    }

    private static async IAsyncEnumerable<ScanProgress> ProgressThenAwaitCancellationAsync(
        DiskItem root,
        Task continueScan,
        TaskCompletionSource progressApplied,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        yield return new ScanProgress(
            root.Path,
            FilesScanned: 1,
            DirectoriesScanned: 1,
            BytesScanned: root.SizeBytes,
            root,
            Errors: [],
            MeasurementMode: StorageMeasurementMode.SharedAwareAllocated);

        progressApplied.SetResult();
        await continueScan.WaitAsync(cancellationToken);
    }

    private sealed class FakeFullDiskAccessService : IFullDiskAccessService
    {
        public FullDiskAccessAssessment Assessment { get; init; } =
            FullDiskAccessAssessment.NotApplicable;

        public FullDiskAccessSettingsResult SettingsResult { get; init; } =
            FullDiskAccessSettingsResult.OpenedDirectly;

        public int OpenSettingsCount { get; private set; }

        public FullDiskAccessAssessment CheckAccess(string scanRootPath) =>
            Assessment;

        public FullDiskAccessSettingsResult OpenSettings()
        {
            OpenSettingsCount++;
            return SettingsResult;
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
