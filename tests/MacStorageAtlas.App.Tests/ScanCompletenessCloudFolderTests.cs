using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core.History;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.App.Tests;

public class ScanCompletenessCloudFolderTests
{
    private static readonly DateTimeOffset Completed = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void UnlistedCloudFoldersMakeAnOtherwiseCompleteScanIncomplete()
    {
        var completeness = ScanCompletenessClassifier.Classify(
            AccessGuidance.None,
            [],
            ["/scan/Unlisted"]);

        Assert.That(completeness, Is.EqualTo(ScanCompleteness.IncompleteCloudFoldersNotListed));
    }

    [Test]
    public void RecoverableErrorsTakePrecedenceOverUnlistedCloudFolders()
    {
        var completeness = ScanCompletenessClassifier.Classify(
            AccessGuidance.None,
            [new ScanError("/scan/locked", "Denied.", "IOException")],
            ["/scan/Unlisted"]);

        Assert.That(completeness, Is.EqualTo(ScanCompleteness.IncompleteRecoverableErrors));
    }

    [Test]
    public void MissingAccessTakesPrecedenceOverUnlistedCloudFolders()
    {
        var completeness = ScanCompletenessClassifier.Classify(
            new AccessGuidance(AccessGuidanceStatus.LikelyMissingFullDiskAccess, 1),
            [],
            ["/scan/Unlisted"]);

        Assert.That(completeness, Is.EqualTo(ScanCompleteness.IncompleteAccessRestricted));
    }

    [Test]
    public void AScanWithoutUnlistedFoldersOrErrorsIsComplete()
    {
        Assert.That(
            ScanCompletenessClassifier.Classify(AccessGuidance.None, [], []),
            Is.EqualTo(ScanCompleteness.Complete));
    }

    [Test]
    public void TheHistoryDescribesASnapshotWithUnlistedCloudFolders()
    {
        var metadata = new ScanSnapshotMetadata(
            "20261122T090000Z-cloud",
            Completed,
            "/scan",
            Completed,
            ScanOptions.Default,
            StorageMeasurementMode.Logical,
            CloneAccountingCoverage.Unavailable,
            1,
            0,
            0,
            ScanCompleteness.IncompleteCloudFoldersNotListed);

        var entry = new ScanHistoryEntryViewModel(
            ScanHistoryEntry.Readable(
                metadata.SnapshotId,
                new ScanSnapshotDescriptor(metadata, 128)));

        Assert.Multiple(() =>
        {
            Assert.That(entry.Completeness, Is.EqualTo("Partial scan, some cloud folders were not listed"));
            Assert.That(entry.IsComplete, Is.False);
        });
    }
}
