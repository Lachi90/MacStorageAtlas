using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.Tests;

public class ScanHistoryViewModelTests
{
    private static readonly DateTimeOffset Origin =
        new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task AnEmptyStoreReportsThatNoScansWereRecorded()
    {
        var viewModel = CreateViewModel(new FakeScanHistoryStore());

        await viewModel.RefreshAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsEmpty, Is.True);
            Assert.That(viewModel.Roots, Is.Empty);
            Assert.That(viewModel.EmptyStateMessage, Does.Contain("No scans"));
        });
    }

    [Test]
    public async Task SnapshotsAreGroupedByScanRoot()
    {
        var store = new FakeScanHistoryStore();
        store.Add(Entry("a", "/home", ageOrder: 0));
        store.Add(Entry("b", "/home", ageOrder: 1));
        store.Add(Entry("c", "/projects", ageOrder: 2));

        var viewModel = CreateViewModel(store);
        await viewModel.RefreshAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Roots, Has.Count.EqualTo(2));
            Assert.That(viewModel.Roots[0].RootPath, Is.EqualTo("/home"));
            Assert.That(viewModel.Roots[0].Snapshots, Has.Count.EqualTo(2));
            Assert.That(viewModel.Roots[1].RootPath, Is.EqualTo("/projects"));
            Assert.That(viewModel.SnapshotCount, Is.EqualTo(3));
            Assert.That(viewModel.IsEmpty, Is.False);
        });
    }

    [Test]
    public async Task SnapshotsAreListedNewestFirstWithinTheirRoot()
    {
        var store = new FakeScanHistoryStore();
        store.Add(Entry("oldest", "/home", ageOrder: 0));
        store.Add(Entry("newest", "/home", ageOrder: 5));
        store.Add(Entry("middle", "/home", ageOrder: 2));

        var viewModel = CreateViewModel(store);
        await viewModel.RefreshAsync();

        Assert.That(
            viewModel.Roots[0].Snapshots.Select(snapshot => snapshot.SnapshotId),
            Is.EqualTo(new[] { "newest", "middle", "oldest" }));
    }

    [Test]
    public async Task AnEntryStatesItsListingFields()
    {
        var store = new FakeScanHistoryStore();
        store.Add(Entry("a", "/home", ageOrder: 0, itemCount: 4096, storedSize: 2048));

        var viewModel = CreateViewModel(store);
        await viewModel.RefreshAsync();

        var entry = viewModel.Roots[0].Snapshots[0];

        Assert.Multiple(() =>
        {
            Assert.That(entry.ItemCount, Is.EqualTo(4096.ToString("N0")));
            Assert.That(entry.StoredSize, Is.Not.Empty);
            Assert.That(entry.MeasurementBasis, Is.Not.Empty);
            Assert.That(entry.CompletedAt, Is.Not.Empty);
            Assert.That(entry.IsReadable, Is.True);
        });
    }

    [Test]
    public async Task APartialScanIsMarkedAsIncomplete()
    {
        var store = new FakeScanHistoryStore();
        store.Add(Entry(
            "a",
            "/home",
            ageOrder: 0,
            completeness: ScanCompleteness.IncompleteAccessRestricted));

        var viewModel = CreateViewModel(store);
        await viewModel.RefreshAsync();

        var entry = viewModel.Roots[0].Snapshots[0];

        Assert.Multiple(() =>
        {
            Assert.That(entry.IsComplete, Is.False);
            Assert.That(entry.Completeness, Does.Contain("Partial"));
            Assert.That(entry.Completeness, Does.Contain("Full Disk Access"));
        });
    }

    [Test]
    public async Task AnUnreadableSnapshotIsPresentedAndReported()
    {
        var store = new FakeScanHistoryStore();
        store.Add(Entry("good", "/home", ageOrder: 0));
        store.Add(ScanHistoryEntry.Unreadable("broken", 128, "The file is corrupt."));

        var viewModel = CreateViewModel(store);
        await viewModel.RefreshAsync();

        var unreadableGroup = viewModel.Roots.Single(root => root.RootPath.Length == 0);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.UnreadableWarning, Does.Contain("could not be read"));
            Assert.That(unreadableGroup.Header, Is.EqualTo("Unreadable snapshots"));
            Assert.That(unreadableGroup.Snapshots[0].IsReadable, Is.False);
            Assert.That(
                unreadableGroup.Snapshots[0].Completeness,
                Is.EqualTo("The file is corrupt."));
        });
    }

    [Test]
    public async Task TheStoreLocationAndTotalSizeArePresented()
    {
        var store = new FakeScanHistoryStore();
        store.Add(Entry("a", "/home", ageOrder: 0, storedSize: 4096));

        var viewModel = CreateViewModel(store);
        await viewModel.RefreshAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.StoreLocation, Is.EqualTo("/tmp/history"));
            Assert.That(viewModel.TotalStoreSize, Is.Not.Empty);
        });
    }

    [Test]
    public async Task DeletingOneSnapshotLeavesTheOthers()
    {
        var store = new FakeScanHistoryStore();
        store.Add(Entry("a", "/home", ageOrder: 0));
        store.Add(Entry("b", "/home", ageOrder: 1));

        var viewModel = CreateViewModel(store);
        await viewModel.RefreshAsync();

        await viewModel.DeleteSnapshotCommand.ExecuteAsync(
            viewModel.Roots[0].Snapshots.Single(
                snapshot => snapshot.SnapshotId == "a"));

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.SnapshotCount, Is.EqualTo(1));
            Assert.That(viewModel.Roots[0].Snapshots[0].SnapshotId, Is.EqualTo("b"));
            Assert.That(viewModel.StatusMessage, Does.Contain("Removed one"));
        });
    }

    [Test]
    public async Task ClearingRequiresConfirmation()
    {
        var store = new FakeScanHistoryStore();
        store.Add(Entry("a", "/home", ageOrder: 0));
        var confirmation = new StubClearConfirmation(confirm: false);

        var viewModel = CreateViewModel(store, confirmation);
        await viewModel.RefreshAsync();
        await viewModel.ClearCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(confirmation.RequestCount, Is.EqualTo(1));
            Assert.That(store.Entries, Has.Count.EqualTo(1));
            Assert.That(viewModel.SnapshotCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ConfirmedClearingRemovesEverySnapshot()
    {
        var store = new FakeScanHistoryStore();
        store.Add(Entry("a", "/home", ageOrder: 0));
        store.Add(Entry("b", "/projects", ageOrder: 1));
        var confirmation = new StubClearConfirmation(confirm: true);

        var viewModel = CreateViewModel(store, confirmation);
        await viewModel.RefreshAsync();
        await viewModel.ClearCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(store.Entries, Is.Empty);
            Assert.That(viewModel.IsEmpty, Is.True);
            Assert.That(viewModel.StatusMessage, Does.Contain("Removed 2"));
            Assert.That(confirmation.LastSnapshotCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task ClearingAnEmptyHistoryDoesNotAskForConfirmation()
    {
        var confirmation = new StubClearConfirmation(confirm: true);
        var viewModel = CreateViewModel(new FakeScanHistoryStore(), confirmation);

        await viewModel.ClearCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(confirmation.RequestCount, Is.Zero);
            Assert.That(viewModel.StatusMessage, Does.Contain("No scans"));
        });
    }

    [Test]
    public async Task RevealingTheStoreOpensItsLocation()
    {
        var store = new FakeScanHistoryStore();
        store.Add(Entry("a", "/home", ageOrder: 0));
        var reveal = new RecordingRevealService(succeeds: true);

        var viewModel = CreateViewModel(store, revealService: reveal);
        await viewModel.RefreshAsync();
        viewModel.RevealStoreCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(reveal.RevealedPath, Is.EqualTo("/tmp/history"));
            Assert.That(viewModel.StatusMessage, Is.Null);
        });
    }

    [Test]
    public async Task RevealingIsUnavailableWhileNothingIsStored()
    {
        var viewModel = CreateViewModel(new FakeScanHistoryStore());

        await viewModel.RefreshAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CanRevealStore, Is.False);
            Assert.That(viewModel.RevealStoreCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public async Task RevealingBecomesAvailableOnceASnapshotIsStored()
    {
        var store = new FakeScanHistoryStore();
        var viewModel = CreateViewModel(store);
        await viewModel.RefreshAsync();

        store.Add(Entry("a", "/home", ageOrder: 0));
        await viewModel.RefreshAsync();

        Assert.That(viewModel.RevealStoreCommand.CanExecute(null), Is.True);
    }

    [Test]
    public async Task AFailedRevealIsReported()
    {
        var store = new FakeScanHistoryStore();
        store.Add(Entry("a", "/home", ageOrder: 0));
        var reveal = new RecordingRevealService(succeeds: false);

        var viewModel = CreateViewModel(store, revealService: reveal);
        await viewModel.RefreshAsync();
        viewModel.RevealStoreCommand.Execute(null);

        Assert.That(
            viewModel.StatusMessage,
            Does.Contain("could not be revealed"));
    }

    [Test]
    public async Task AnUnreadableStoreIsReportedWithoutThrowing()
    {
        var viewModel = CreateViewModel(new ThrowingScanHistoryStore());

        await viewModel.RefreshAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Roots, Is.Empty);
            Assert.That(viewModel.UnreadableWarning, Does.Contain("could not be read"));
        });
    }

    private static ScanHistoryViewModel CreateViewModel(
        IScanHistoryStore store,
        IScanHistoryClearConfirmationService? confirmation = null,
        IFileRevealService? revealService = null) =>
        new(
            store,
            confirmation ?? new StubClearConfirmation(confirm: true),
            new ImmediateUiDispatcher(),
            revealService ?? new RecordingRevealService(succeeds: true));

    private static ScanHistoryEntry Entry(
        string snapshotId,
        string rootPath,
        int ageOrder,
        long itemCount = 10,
        long storedSize = 1024,
        ScanCompleteness completeness = ScanCompleteness.Complete) =>
        ScanHistoryEntry.Readable(
            snapshotId,
            new ScanSnapshotDescriptor(
                new ScanSnapshotMetadata(
                    snapshotId,
                    Origin.AddHours(ageOrder),
                    rootPath,
                    Origin.AddHours(ageOrder),
                    ScanOptions.Default,
                    StorageMeasurementMode.SharedAwareAllocated,
                    CloneAccountingCoverage.Available,
                    itemCount,
                    4096,
                    0,
                    completeness),
                storedSize));

    private class FakeScanHistoryStore : IScanHistoryStore
    {
        public List<ScanHistoryEntry> Entries { get; } = [];

        public string Location => "/tmp/history";

        public void Add(ScanHistoryEntry entry) => Entries.Add(entry);

        public virtual Task<IReadOnlyList<ScanHistoryEntry>> ListAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ScanHistoryEntry>>(Entries.ToArray());

        public virtual Task<long> GetTotalSizeBytesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Entries.Sum(entry => entry.StoredSizeBytes));

        public Task<ScanHistoryCaptureResult> CaptureAsync(
            ScanSnapshotRequest request,
            ScanHistoryLimits limits,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(ScanHistoryCaptureResult.Failed("not used"));

        public Task<ScanSnapshotReadResult<ScanSnapshotDocument>> ReadAsync(
            string snapshotId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                ScanSnapshotReadResult<ScanSnapshotDocument>.Unreadable("not used"));

        public Task<bool> DeleteAsync(
            string snapshotId,
            CancellationToken cancellationToken = default)
        {
            var removed = Entries.RemoveAll(entry => entry.SnapshotId == snapshotId);
            return Task.FromResult(removed > 0);
        }

        public Task ClearAsync(CancellationToken cancellationToken = default)
        {
            Entries.Clear();
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ScanSnapshotDescriptor>> ApplyLimitsAsync(
            ScanHistoryLimits limits,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ScanSnapshotDescriptor>>([]);
    }

    private sealed class ThrowingScanHistoryStore : FakeScanHistoryStore
    {
        public override Task<IReadOnlyList<ScanHistoryEntry>> ListAsync(
            CancellationToken cancellationToken = default) =>
            throw new IOException("The store is unreadable.");
    }

    private sealed class StubClearConfirmation(bool confirm)
        : IScanHistoryClearConfirmationService
    {
        public int RequestCount { get; private set; }

        public int LastSnapshotCount { get; private set; }

        public Task<bool> ConfirmClearHistoryAsync(
            int snapshotCount,
            long totalSizeBytes)
        {
            RequestCount++;
            LastSnapshotCount = snapshotCount;
            return Task.FromResult(confirm);
        }
    }

    private sealed class RecordingRevealService(bool succeeds) : IFileRevealService
    {
        public string? RevealedPath { get; private set; }

        public bool Reveal(string path)
        {
            RevealedPath = path;
            return succeeds;
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
