using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core.History;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.App.Tests;

public class ScanHistoryEntryRefreshedFolderTests
{
    private static readonly DateTimeOffset Completed =
        new(2026, 11, 5, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void ASnapshotWithRescannedFoldersIsMarked()
    {
        var entry = new ScanHistoryEntryViewModel(Entry(
            "refreshed",
            Completed.AddMinutes(30),
            [
                new RefreshedFolder("/scan/a", Completed.AddMinutes(10)),
                new RefreshedFolder("/scan/b", Completed.AddMinutes(20))
            ]));

        Assert.Multiple(() =>
        {
            Assert.That(entry.HasRefreshedFolders, Is.True);
            Assert.That(entry.RefreshedFolderCount, Is.EqualTo(2));
            Assert.That(
                entry.RefreshedFoldersLabel,
                Is.EqualTo("Contains rescanned folders (2 folders rescanned after the original scan)"));
            Assert.That(entry.CapturedAt, Is.EqualTo(Completed.AddMinutes(30)));
        });
    }

    [Test]
    public void ASnapshotOfAFullScanIsNotMarked()
    {
        var entry = new ScanHistoryEntryViewModel(Entry("full", Completed, []));

        Assert.Multiple(() =>
        {
            Assert.That(entry.HasRefreshedFolders, Is.False);
            Assert.That(entry.RefreshedFoldersLabel, Is.Empty);
        });
    }

    [Test]
    public void AVersionOneSnapshotIsShownWithoutRescannedFolders()
    {
        var entry = new ScanHistoryEntryViewModel(Entry("legacy", Completed, [], schemaVersion: 1));

        Assert.Multiple(() =>
        {
            Assert.That(entry.IsReadable, Is.True);
            Assert.That(entry.HasRefreshedFolders, Is.False);
        });
    }

    [Test]
    public void AnUnreadableSnapshotIsNotMarked()
    {
        var entry = new ScanHistoryEntryViewModel(
            ScanHistoryEntry.Unreadable("broken", 128, "The file is corrupt."));

        Assert.That(entry.HasRefreshedFolders, Is.False);
    }

    private static ScanHistoryEntry Entry(
        string snapshotId,
        DateTimeOffset capturedAt,
        IReadOnlyList<RefreshedFolder> refreshedFolders,
        int schemaVersion = ScanSnapshotSchema.CurrentVersion) =>
        ScanHistoryEntry.Readable(
            snapshotId,
            new ScanSnapshotDescriptor(
                new ScanSnapshotMetadata(
                    snapshotId,
                    capturedAt,
                    "/scan",
                    Completed,
                    ScanOptions.Default,
                    StorageMeasurementMode.Logical,
                    CloneAccountingCoverage.Unavailable,
                    10,
                    4096,
                    0,
                    ScanCompleteness.Complete)
                {
                    RefreshedFolders = refreshedFolders,
                    SchemaVersion = schemaVersion
                },
                1024));
}
