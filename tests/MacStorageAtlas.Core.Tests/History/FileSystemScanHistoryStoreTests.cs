using System.IO;
using System.Text;
using MacStorageAtlas.Core.Export;
using MacStorageAtlas.Core.History;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Tests.History;

public class FileSystemScanHistoryStoreTests
{
    private static readonly DateTimeOffset Origin =
        new(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

    private string _root = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "MacStorageAtlasHistoryTests",
            Guid.NewGuid().ToString("N"));
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
    public async Task AnEmptyStoreListsNothing()
    {
        var store = new FileSystemScanHistoryStore(_root);

        Assert.Multiple(async () =>
        {
            Assert.That(await store.ListAsync(), Is.Empty);
            Assert.That(await store.GetTotalSizeBytesAsync(), Is.Zero);
        });
    }

    [Test]
    public async Task CapturePublishesASnapshotThatListsBack()
    {
        var store = new FileSystemScanHistoryStore(_root);

        var result = await store.CaptureAsync(Request("a", "/home"), Limits());

        Assert.That(result.IsCaptured, Is.True);

        var entries = await store.ListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].IsReadable, Is.True);
            Assert.That(entries[0].Descriptor!.RootPath, Is.EqualTo("/home"));
            Assert.That(entries[0].Descriptor!.ItemCount, Is.EqualTo(2));
            Assert.That(entries[0].StoredSizeBytes, Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task CaptureLeavesNoPendingFileBehind()
    {
        var store = new FileSystemScanHistoryStore(_root);

        await store.CaptureAsync(Request("a", "/home"), Limits());

        Assert.That(Directory.GetFiles(_root, "*.pending"), Is.Empty);
    }

    [Test]
    public async Task ACapturedSnapshotReadsBackInFull()
    {
        var store = new FileSystemScanHistoryStore(_root);
        await store.CaptureAsync(Request("a", "/home"), Limits());

        var result = await store.ReadAsync("a");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsOk, Is.True);
            Assert.That(result.Payload!.Items, Has.Count.EqualTo(2));
            Assert.That(result.Payload!.Metadata.RootPath, Is.EqualTo("/home"));
        });
    }

    [Test]
    public void CancellingCaptureLeavesNoPendingFile()
    {
        var store = new FileSystemScanHistoryStore(_root);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await store.CaptureAsync(
                Request("a", "/home", rowCount: 5000),
                Limits(),
                cancellation.Token));

        Assert.Multiple(() =>
        {
            Assert.That(Directory.GetFiles(_root, "*.pending"), Is.Empty);
            Assert.That(Directory.GetFiles(_root, "*.msascan.gz"), Is.Empty);
        });
    }

    [Test]
    public async Task AnOversizedSnapshotIsRefusedAndChangesNothing()
    {
        var store = new FileSystemScanHistoryStore(_root);
        await store.CaptureAsync(Request("existing", "/home"), Limits());

        var result = await store.CaptureAsync(
            Request("huge", "/home", rowCount: 5000),
            new ScanHistoryLimits(10, 64));

        var entries = await store.ListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(ScanHistoryCaptureStatus.Refused));
            Assert.That(result.Message, Is.Not.Null.And.Not.Empty);
            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].SnapshotId, Is.EqualTo("existing"));
            Assert.That(Directory.GetFiles(_root, "*.pending"), Is.Empty);
        });
    }

    [Test]
    public async Task CapturePrunesTheOldestSnapshotOfTheSameRoot()
    {
        var store = new FileSystemScanHistoryStore(_root);
        await store.CaptureAsync(Request("oldest", "/home", ageOrder: 0), Limits());
        await store.CaptureAsync(Request("newest", "/home", ageOrder: 1), Limits());

        var result = await store.CaptureAsync(
            Request("incoming", "/home", ageOrder: 2),
            new ScanHistoryLimits(2, 10_000_000));

        var entries = await store.ListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsCaptured, Is.True);
            Assert.That(
                result.PrunedSnapshots.Select(snapshot => snapshot.SnapshotId),
                Is.EqualTo(new[] { "oldest" }));
            Assert.That(
                entries.Select(entry => entry.SnapshotId).Order(),
                Is.EqualTo(new[] { "incoming", "newest" }));
        });
    }

    [Test]
    public async Task DeletingOneSnapshotLeavesTheOthers()
    {
        var store = new FileSystemScanHistoryStore(_root);
        await store.CaptureAsync(Request("a", "/home", ageOrder: 0), Limits());
        await store.CaptureAsync(Request("b", "/home", ageOrder: 1), Limits());

        var deleted = await store.DeleteAsync("a");
        var entries = await store.ListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.True);
            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].SnapshotId, Is.EqualTo("b"));
        });
    }

    [Test]
    public async Task DeletingAnAbsentSnapshotReportsNothingRemoved()
    {
        var store = new FileSystemScanHistoryStore(_root);

        Assert.That(await store.DeleteAsync("missing"), Is.False);
    }

    [Test]
    public async Task ClearingRemovesEverySnapshot()
    {
        var store = new FileSystemScanHistoryStore(_root);
        await store.CaptureAsync(Request("a", "/home", ageOrder: 0), Limits());
        await store.CaptureAsync(Request("b", "/projects", ageOrder: 1), Limits());

        await store.ClearAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await store.ListAsync(), Is.Empty);
            Assert.That(await store.GetTotalSizeBytesAsync(), Is.Zero);
        });
    }

    [Test]
    public async Task AnUnreadableSnapshotIsListedBesideReadableOnes()
    {
        var store = new FileSystemScanHistoryStore(_root);
        await store.CaptureAsync(Request("good", "/home"), Limits());
        await File.WriteAllBytesAsync(
            Path.Combine(_root, "broken.msascan.gz"),
            Encoding.UTF8.GetBytes("not a snapshot"));

        var entries = await store.ListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(entries, Has.Count.EqualTo(2));
            Assert.That(
                entries.Single(entry => entry.SnapshotId == "good").IsReadable,
                Is.True);

            var broken = entries.Single(entry => entry.SnapshotId == "broken");
            Assert.That(broken.IsReadable, Is.False);
            Assert.That(broken.UnreadableMessage, Is.Not.Null.And.Not.Empty);
            Assert.That(broken.Descriptor, Is.Null);
        });
    }

    [Test]
    public async Task AnUnreadableSnapshotIsNotDeletedOnItsOwn()
    {
        var store = new FileSystemScanHistoryStore(_root);
        var brokenPath = Path.Combine(_root, "broken.msascan.gz");
        Directory.CreateDirectory(_root);
        await File.WriteAllBytesAsync(brokenPath, Encoding.UTF8.GetBytes("broken"));

        await store.ListAsync();

        Assert.That(File.Exists(brokenPath), Is.True);
    }

    [Test]
    public async Task AnUnreadableSnapshotCanBeDeletedByTheUser()
    {
        var store = new FileSystemScanHistoryStore(_root);
        Directory.CreateDirectory(_root);
        await File.WriteAllBytesAsync(
            Path.Combine(_root, "broken.msascan.gz"),
            Encoding.UTF8.GetBytes("broken"));

        var deleted = await store.DeleteAsync("broken");

        Assert.Multiple(async () =>
        {
            Assert.That(deleted, Is.True);
            Assert.That(await store.ListAsync(), Is.Empty);
        });
    }

    [Test]
    public async Task CaptureStillSucceedsAlongsideAnUnreadableSnapshot()
    {
        var store = new FileSystemScanHistoryStore(_root);
        Directory.CreateDirectory(_root);
        await File.WriteAllBytesAsync(
            Path.Combine(_root, "broken.msascan.gz"),
            Encoding.UTF8.GetBytes("broken"));

        var result = await store.CaptureAsync(Request("a", "/home"), Limits());

        Assert.That(result.IsCaptured, Is.True);
    }

    [Test]
    public async Task OrphanedPendingFilesAreSweptOnConstruction()
    {
        Directory.CreateDirectory(_root);
        var orphan = Path.Combine(_root, "interrupted.msascan.gz.pending");
        await File.WriteAllBytesAsync(orphan, [1, 2, 3]);

        var store = new FileSystemScanHistoryStore(_root);

        Assert.Multiple(async () =>
        {
            Assert.That(File.Exists(orphan), Is.False);
            Assert.That(await store.ListAsync(), Is.Empty);
        });
    }

    [Test]
    public async Task ApplyingALoweredLimitPrunesImmediately()
    {
        var store = new FileSystemScanHistoryStore(_root);
        await store.CaptureAsync(Request("a", "/home", ageOrder: 0), Limits());
        await store.CaptureAsync(Request("b", "/home", ageOrder: 1), Limits());
        await store.CaptureAsync(Request("c", "/home", ageOrder: 2), Limits());

        var pruned = await store.ApplyLimitsAsync(new ScanHistoryLimits(1, 10_000_000));
        var entries = await store.ListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                pruned.Select(snapshot => snapshot.SnapshotId),
                Is.EqualTo(new[] { "a", "b" }));
            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].SnapshotId, Is.EqualTo("c"));
        });
    }

    [Test]
    public async Task TheStoreExcludesItselfFromSpotlightIndexing()
    {
        var store = new FileSystemScanHistoryStore(_root);
        await store.CaptureAsync(Request("a", "/home"), Limits());

        Assert.That(
            File.Exists(Path.Combine(_root, ".metadata_never_index")),
            Is.True);
    }

    [Test]
    [Platform("MacOsX", Reason = "Unix file modes only apply on macOS.")]
    public async Task APublishedSnapshotIsReadableOnlyByItsOwner()
    {
        var store = new FileSystemScanHistoryStore(_root);
        await store.CaptureAsync(Request("a", "/home"), Limits());

        var mode = File.GetUnixFileMode(Path.Combine(_root, "a.msascan.gz"));

        Assert.Multiple(() =>
        {
            Assert.That(mode.HasFlag(UnixFileMode.GroupRead), Is.False);
            Assert.That(mode.HasFlag(UnixFileMode.OtherRead), Is.False);
            Assert.That(mode.HasFlag(UnixFileMode.UserRead), Is.True);
        });
    }

    [Test]
    [Platform("MacOsX", Reason = "Unix file modes only apply on macOS.")]
    public async Task TheStoreDirectoryIsReadableOnlyByItsOwner()
    {
        var store = new FileSystemScanHistoryStore(_root);
        await store.CaptureAsync(Request("a", "/home"), Limits());

        var mode = File.GetUnixFileMode(_root);

        Assert.Multiple(() =>
        {
            Assert.That(mode.HasFlag(UnixFileMode.GroupRead), Is.False);
            Assert.That(mode.HasFlag(UnixFileMode.OtherRead), Is.False);
        });
    }

    [Test]
    public async Task AStoreThatCannotBeReadDoesNotThrow()
    {
        var store = new FileSystemScanHistoryStore(
            Path.Combine(_root, "never", "created"));

        Assert.Multiple(async () =>
        {
            Assert.That(await store.ListAsync(), Is.Empty);
            Assert.That(await store.GetTotalSizeBytesAsync(), Is.Zero);
            Assert.That(await store.DeleteAsync("anything"), Is.False);
            Assert.That(
                (await store.ReadAsync("anything")).Status,
                Is.EqualTo(ScanSnapshotReadStatus.Unreadable));
        });
    }

    [Test]
    public async Task RemovingTheStoreOutsideTheApplicationLeavesAnEmptyHistory()
    {
        var store = new FileSystemScanHistoryStore(_root);
        await store.CaptureAsync(Request("a", "/home"), Limits());

        Directory.Delete(_root, recursive: true);

        Assert.Multiple(async () =>
        {
            Assert.That(await store.ListAsync(), Is.Empty);
            Assert.That(await store.GetTotalSizeBytesAsync(), Is.Zero);
            Assert.That(await store.DeleteAsync("a"), Is.False);
        });
    }

    [Test]
    public async Task CaptureRecreatesAStoreRemovedOutsideTheApplication()
    {
        var store = new FileSystemScanHistoryStore(_root);
        await store.CaptureAsync(Request("a", "/home", ageOrder: 0), Limits());

        Directory.Delete(_root, recursive: true);

        var result = await store.CaptureAsync(
            Request("b", "/home", ageOrder: 1),
            Limits());
        var entries = await store.ListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsCaptured, Is.True);
            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].SnapshotId, Is.EqualTo("b"));
        });
    }

    [Test]
    public async Task ASnapshotRemovedDuringListingIsNotReportedAsCorrupt()
    {
        var store = new FileSystemScanHistoryStore(_root);
        await store.CaptureAsync(Request("a", "/home", ageOrder: 0), Limits());
        await store.CaptureAsync(Request("b", "/home", ageOrder: 1), Limits());

        File.Delete(Path.Combine(_root, "a.msascan.gz"));

        var entries = await store.ListAsync();

        Assert.Multiple(() =>
        {
            Assert.That(entries, Has.Count.EqualTo(1));
            Assert.That(entries[0].SnapshotId, Is.EqualTo("b"));
            Assert.That(entries.Any(entry => !entry.IsReadable), Is.False);
        });
    }

    [Test]
    public async Task ClearingAnAlreadyRemovedStoreDoesNotThrow()
    {
        var store = new FileSystemScanHistoryStore(_root);
        await store.CaptureAsync(Request("a", "/home"), Limits());

        Directory.Delete(_root, recursive: true);

        await store.ClearAsync();

        Assert.That(await store.ListAsync(), Is.Empty);
    }

    [Test]
    public async Task ReadingAnAbsentSnapshotIsReportedAsUnreadable()
    {
        var store = new FileSystemScanHistoryStore(_root);

        var result = await store.ReadAsync("missing");

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(ScanSnapshotReadStatus.Unreadable));
            Assert.That(result.Message, Does.Contain("missing"));
        });
    }

    [Test]
    public async Task TheTotalSizeCountsEveryStoredSnapshot()
    {
        var store = new FileSystemScanHistoryStore(_root);
        await store.CaptureAsync(Request("a", "/home", ageOrder: 0), Limits());
        await store.CaptureAsync(Request("b", "/projects", ageOrder: 1), Limits());

        var entries = await store.ListAsync();
        var total = await store.GetTotalSizeBytesAsync();

        Assert.That(total, Is.EqualTo(entries.Sum(entry => entry.StoredSizeBytes)));
    }

    private static ScanHistoryLimits Limits() => new(10, 10_000_000);

    private static ScanSnapshotRequest Request(
        string snapshotId,
        string rootPath,
        int ageOrder = 0,
        int rowCount = 2)
    {
        var completedAt = Origin.AddHours(ageOrder);

        var metadata = new ScanSnapshotMetadata(
            snapshotId,
            completedAt,
            rootPath,
            completedAt,
            ScanOptions.Default,
            StorageMeasurementMode.SharedAwareAllocated,
            CloneAccountingCoverage.Available,
            rowCount,
            4096 * rowCount,
            0,
            ScanCompleteness.Complete);

        return new ScanSnapshotRequest(metadata, Rows(rootPath, rowCount));
    }

    private static IEnumerable<ScanExportRow> Rows(string rootPath, int count)
    {
        for (var index = 0; index < count; index++)
        {
            yield return new ScanExportRow(
                $"{rootPath}/file-{index}.bin",
                $"file-{index}.bin",
                DiskItemKind.File,
                1,
                StorageMeasurementMode.SharedAwareAllocated,
                4096,
                4096,
                0,
                false,
                ".bin",
                null,
                null,
                Origin,
                null);
        }
    }
}
