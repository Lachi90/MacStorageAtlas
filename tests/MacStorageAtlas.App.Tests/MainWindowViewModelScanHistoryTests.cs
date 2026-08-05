using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using MacStorageAtlas.App.Models;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.Tests;

public class MainWindowViewModelScanHistoryTests
{
    private static readonly DateTimeOffset Reference =
        new(2026, 8, 5, 14, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task ACompletedScanIsRecordedWhenHistoryIsEnabled()
    {
        var store = new RecordingScanHistoryStore();
        var viewModel = CreateViewModel(store, historyEnabled: true);

        await ScanAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(store.CaptureCount, Is.EqualTo(1));
            Assert.That(store.LastRequest!.Metadata.RootPath, Is.EqualTo("/Users/test"));
            Assert.That(
                store.LastRequest!.Metadata.ScanCompletedAt,
                Is.EqualTo(Reference));
        });
    }

    [Test]
    public async Task NothingIsRecordedWhenHistoryIsDisabled()
    {
        var store = new RecordingScanHistoryStore();
        var viewModel = CreateViewModel(store, historyEnabled: false);

        await ScanAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(store.CaptureCount, Is.Zero);
            Assert.That(viewModel.ScanHistoryStatusMessage, Is.Null);
        });
    }

    [Test]
    public async Task ARecordedSnapshotCoversEveryScannedItem()
    {
        var store = new RecordingScanHistoryStore();
        var viewModel = CreateViewModel(store, historyEnabled: true);

        await ScanAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(store.LastRowCount, Is.EqualTo(5));
            Assert.That(store.LastRequest!.Metadata.ItemCount, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task AnActiveFilterDoesNotNarrowTheRecordedSnapshot()
    {
        var store = new RecordingScanHistoryStore();
        var viewModel = CreateViewModel(store, historyEnabled: true);

        await ScanAsync(viewModel);
        viewModel.Filter.TextTerm = "big";
        await viewModel.TreePreparation;

        store.Reset();
        await ScanAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(store.LastRowCount, Is.EqualTo(5));
            Assert.That(store.LastRequest!.Metadata.ItemCount, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task ACancelledScanIsNotRecorded()
    {
        var store = new RecordingScanHistoryStore();
        var viewModel = new MainWindowViewModel(
            new NullFolderPickerService(),
            new CancellingDiskScanner(),
            new ImmediateUiDispatcher(),
            settingsService: new InMemorySettingsService(),
            scanHistoryStore: store,
            searchDebounceInterval: TimeSpan.Zero,
            referenceTimeProvider: () => Reference)
        {
            ScanHistoryEnabled = true
        };

        viewModel.SelectedFolderPath = "/Users/test";
        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.That(store.CaptureCount, Is.Zero);
    }

    [Test]
    public async Task AFailedScanIsNotRecorded()
    {
        var store = new RecordingScanHistoryStore();
        var viewModel = new MainWindowViewModel(
            new NullFolderPickerService(),
            new NeverCompletingDiskScanner(),
            new ImmediateUiDispatcher(),
            settingsService: new InMemorySettingsService(),
            scanHistoryStore: store,
            searchDebounceInterval: TimeSpan.Zero,
            referenceTimeProvider: () => Reference)
        {
            ScanHistoryEnabled = true
        };

        viewModel.SelectedFolderPath = "/Users/test";
        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.That(store.CaptureCount, Is.Zero);
    }

    [Test]
    public async Task StartingANewScanCancelsACaptureInProgress()
    {
        var store = new BlockingScanHistoryStore();
        var viewModel = CreateViewModel(store, historyEnabled: true);

        var firstScan = ScanAsync(viewModel);
        await store.CaptureStarted.Task;

        var secondScan = ScanAsync(viewModel);
        store.Release();

        await firstScan;
        await secondScan;

        Assert.That(store.FirstCaptureWasCancelled, Is.True);
    }

    [Test]
    public async Task AScanWithoutErrorsIsRecordedAsComplete()
    {
        var store = new RecordingScanHistoryStore();
        var viewModel = CreateViewModel(store, historyEnabled: true);

        await ScanAsync(viewModel);

        Assert.That(
            store.LastRequest!.Metadata.Completeness,
            Is.EqualTo(ScanCompleteness.Complete));
    }

    [Test]
    public async Task AScanWithUnreadablePathsIsRecordedAsIncomplete()
    {
        var store = new RecordingScanHistoryStore();
        var viewModel = CreateViewModel(
            store,
            historyEnabled: true,
            errors: [new ScanError("/Users/test/locked", "Denied.", "IOException")]);

        await ScanAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(
                store.LastRequest!.Metadata.Completeness,
                Is.Not.EqualTo(ScanCompleteness.Complete));
            Assert.That(store.LastRequest!.Metadata.ErrorCount, Is.EqualTo(1));
            Assert.That(store.LastRequest!.Errors, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task ARefusedCaptureIsReportedAndLeavesTheResultUsable()
    {
        var store = new RefusingScanHistoryStore();
        var viewModel = CreateViewModel(store, historyEnabled: true);

        await ScanAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(
                viewModel.ScanHistoryStatusMessage,
                Does.Contain("was not recorded"));
            Assert.That(viewModel.ScanHistoryStatusMessage, Does.Contain("too large"));
            Assert.That(viewModel.TreeItems, Is.Not.Empty);
            Assert.That(viewModel.ScanCompletedAt, Is.EqualTo(Reference));
            Assert.That(viewModel.ExportJsonCommand.CanExecute(null), Is.True);
        });
    }

    [Test]
    public async Task AFailedCaptureLeavesTheDisplayedResultUsable()
    {
        var store = new ThrowingScanHistoryStore();
        var viewModel = CreateViewModel(store, historyEnabled: true);

        await ScanAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(
                viewModel.ScanHistoryStatusMessage,
                Does.Contain("was not recorded"));
            Assert.That(viewModel.TreeItems, Is.Not.Empty);
            Assert.That(viewModel.ExportJsonCommand.CanExecute(null), Is.True);
        });
    }

    [Test]
    public async Task ASuccessfulCaptureIsReportedToTheUser()
    {
        var store = new RecordingScanHistoryStore();
        var viewModel = CreateViewModel(store, historyEnabled: true);

        await ScanAsync(viewModel);

        Assert.That(viewModel.ScanHistoryStatusMessage, Does.Contain("Recorded"));
    }

    [Test]
    public async Task LoweringASnapshotLimitPrunesImmediately()
    {
        var store = new RecordingScanHistoryStore();
        var viewModel = CreateViewModel(store, historyEnabled: true);

        viewModel.MaxScanHistorySnapshotsPerRoot = 2;
        await Task.Yield();

        Assert.That(store.AppliedLimits, Has.Count.GreaterThanOrEqualTo(1));
    }

    [Test]
    public void HistorySettingsPersistThroughTheSettingsService()
    {
        var settings = new InMemorySettingsService();
        var viewModel = new MainWindowViewModel(
            new NullFolderPickerService(),
            new StubDiskScanner(CreateTree(), []),
            new ImmediateUiDispatcher(),
            settingsService: settings,
            scanHistoryStore: new RecordingScanHistoryStore(),
            searchDebounceInterval: TimeSpan.Zero,
            referenceTimeProvider: () => Reference);

        viewModel.ScanHistoryEnabled = true;
        viewModel.MaxScanHistorySnapshotsPerRoot = 7;
        viewModel.MaxScanHistoryStoreSizeBytes = 2048;

        var saved = settings.Load();

        Assert.Multiple(() =>
        {
            Assert.That(saved.ScanHistoryEnabled, Is.True);
            Assert.That(saved.MaxScanHistorySnapshotsPerRoot, Is.EqualTo(7));
            Assert.That(saved.MaxScanHistoryStoreSizeBytes, Is.EqualTo(2048));
        });
    }

    private static async Task ScanAsync(MainWindowViewModel viewModel)
    {
        viewModel.SelectedFolderPath = "/Users/test";
        await viewModel.ScanFolderCommand.ExecuteAsync(null);
        await viewModel.TreePreparation;
    }

    private static MainWindowViewModel CreateViewModel(
        IScanHistoryStore store,
        bool historyEnabled,
        IReadOnlyList<ScanError>? errors = null)
    {
        var viewModel = new MainWindowViewModel(
            new NullFolderPickerService(),
            new StubDiskScanner(CreateTree(), errors ?? []),
            new ImmediateUiDispatcher(),
            settingsService: new InMemorySettingsService(),
            scanHistoryStore: store,
            searchDebounceInterval: TimeSpan.Zero,
            referenceTimeProvider: () => Reference);

        viewModel.ScanHistoryEnabled = historyEnabled;
        return viewModel;
    }

    private static DiskItem CreateTree()
    {
        var root = new DiskItem("test", "/Users/test", isDirectory: true)
        {
            SizeBytes = 600
        };

        var docs = new DiskItem("docs", "/Users/test/docs", isDirectory: true)
        {
            SizeBytes = 500
        };
        docs.AddChild(new DiskItem(
            "big.txt",
            "/Users/test/docs/big.txt",
            isDirectory: false)
        {
            SizeBytes = 300
        });
        docs.AddChild(new DiskItem(
            "mid.txt",
            "/Users/test/docs/mid.txt",
            isDirectory: false)
        {
            SizeBytes = 200
        });

        root.AddChild(docs);
        root.AddChild(new DiskItem(
            "small.txt",
            "/Users/test/small.txt",
            isDirectory: false)
        {
            SizeBytes = 100
        });

        return root;
    }

    private class RecordingScanHistoryStore : IScanHistoryStore
    {
        public string Location => "/tmp/history";

        public int CaptureCount { get; private set; }

        public ScanSnapshotRequest? LastRequest { get; private set; }

        public int LastRowCount { get; private set; }

        public List<ScanHistoryLimits> AppliedLimits { get; } = [];

        public void Reset()
        {
            CaptureCount = 0;
            LastRequest = null;
            LastRowCount = 0;
        }

        public virtual Task<ScanHistoryCaptureResult> CaptureAsync(
            ScanSnapshotRequest request,
            ScanHistoryLimits limits,
            CancellationToken cancellationToken = default)
        {
            CaptureCount++;
            LastRequest = request;
            LastRowCount = request.Rows.Count();

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
            CancellationToken cancellationToken = default)
        {
            AppliedLimits.Add(limits);
            return Task.FromResult<IReadOnlyList<ScanSnapshotDescriptor>>([]);
        }
    }

    private sealed class RefusingScanHistoryStore : RecordingScanHistoryStore
    {
        public override Task<ScanHistoryCaptureResult> CaptureAsync(
            ScanSnapshotRequest request,
            ScanHistoryLimits limits,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ScanHistoryCaptureResult.Refused(
                "The snapshot is too large for the scan history store limit."));
    }

    private sealed class ThrowingScanHistoryStore : RecordingScanHistoryStore
    {
        public override Task<ScanHistoryCaptureResult> CaptureAsync(
            ScanSnapshotRequest request,
            ScanHistoryLimits limits,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ScanHistoryCaptureResult.Failed("The disk is full."));
    }

    private sealed class BlockingScanHistoryStore : RecordingScanHistoryStore
    {
        private readonly TaskCompletionSource _release = new();
        private int _captures;

        public TaskCompletionSource CaptureStarted { get; } = new();

        public bool FirstCaptureWasCancelled { get; private set; }

        public void Release() => _release.TrySetResult();

        public override async Task<ScanHistoryCaptureResult> CaptureAsync(
            ScanSnapshotRequest request,
            ScanHistoryLimits limits,
            CancellationToken cancellationToken = default)
        {
            var capture = Interlocked.Increment(ref _captures);

            if (capture > 1)
            {
                return await base
                    .CaptureAsync(request, limits, cancellationToken)
                    .ConfigureAwait(false);
            }

            CaptureStarted.TrySetResult();
            await _release.Task.ConfigureAwait(false);

            if (cancellationToken.IsCancellationRequested)
            {
                FirstCaptureWasCancelled = true;
                cancellationToken.ThrowIfCancellationRequested();
            }

            return await base
                .CaptureAsync(request, limits, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private sealed class StubDiskScanner : IDiskScanner
    {
        private readonly DiskItem _root;
        private readonly IReadOnlyList<ScanError> _errors;

        public StubDiskScanner(DiskItem root, IReadOnlyList<ScanError> errors)
        {
            _root = root;
            _errors = errors;
        }

        public async IAsyncEnumerable<ScanProgress> ScanAsync(
            string rootPath,
            ScanOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            yield return new ScanProgress(
                rootPath,
                FilesScanned: 3,
                DirectoriesScanned: 2,
                BytesScanned: _root.SizeBytes,
                Root: _root,
                Errors: _errors,
                IsCompleted: true,
                MeasurementMode: (options ?? ScanOptions.Default).MeasurementMode);
        }
    }

    private sealed class CancellingDiskScanner : IDiskScanner
    {
        public async IAsyncEnumerable<ScanProgress> ScanAsync(
            string rootPath,
            ScanOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            yield return new ScanProgress(
                rootPath,
                FilesScanned: 1,
                DirectoriesScanned: 1,
                BytesScanned: 10,
                Root: new DiskItem("test", "/Users/test", isDirectory: true),
                Errors: [],
                IsCompleted: false);

            throw new OperationCanceledException();
        }
    }

    private sealed class NeverCompletingDiskScanner : IDiskScanner
    {
        public async IAsyncEnumerable<ScanProgress> ScanAsync(
            string rootPath,
            ScanOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();

            yield return new ScanProgress(
                rootPath,
                FilesScanned: 1,
                DirectoriesScanned: 1,
                BytesScanned: 10,
                Root: new DiskItem("test", "/Users/test", isDirectory: true),
                Errors: [],
                IsCompleted: false);
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
