using MacStorageAtlas.Core;

namespace MacStorageAtlas.Core.Tests;

public class ScanSnapshotModelTests
{
    private static readonly DateTimeOffset Captured =
        new(2026, 8, 5, 14, 2, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Completed =
        new(2026, 8, 5, 14, 1, 30, TimeSpan.Zero);

    [Test]
    public void MetadataRetainsEveryRecordedField()
    {
        var metadata = Metadata();

        Assert.Multiple(() =>
        {
            Assert.That(metadata.SnapshotId, Is.EqualTo("20260805T140200Z-abcd1234"));
            Assert.That(metadata.CapturedAt, Is.EqualTo(Captured));
            Assert.That(metadata.RootPath, Is.EqualTo("/scan"));
            Assert.That(metadata.ScanCompletedAt, Is.EqualTo(Completed));
            Assert.That(metadata.Options.IncludeHiddenFiles, Is.True);
            Assert.That(
                metadata.MeasurementMode,
                Is.EqualTo(StorageMeasurementMode.SharedAwareAllocated));
            Assert.That(
                metadata.CloneAccountingCoverage,
                Is.EqualTo(CloneAccountingCoverage.Available));
            Assert.That(metadata.ItemCount, Is.EqualTo(512));
            Assert.That(metadata.TotalCountedSizeBytes, Is.EqualTo(4096));
            Assert.That(metadata.ErrorCount, Is.EqualTo(2));
            Assert.That(
                metadata.Completeness,
                Is.EqualTo(ScanCompleteness.IncompleteRecoverableErrors));
        });
    }

    [Test]
    public void MetadataDefaultsToTheCurrentSchemaVersion()
    {
        Assert.That(
            Metadata().SchemaVersion,
            Is.EqualTo(ScanSnapshotSchema.CurrentVersion));
    }

    [Test]
    public void MetadataRejectsAnEmptySnapshotIdentity()
    {
        Assert.Throws<ArgumentException>(() => Metadata(snapshotId: " "));
    }

    [Test]
    public void MetadataRejectsAnEmptyRootPath()
    {
        Assert.Throws<ArgumentException>(() => Metadata(rootPath: string.Empty));
    }

    [Test]
    public void MetadataRejectsNegativeCounts()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Metadata(itemCount: -1));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => Metadata(totalCountedSizeBytes: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Metadata(errorCount: -1));
        });
    }

    [Test]
    public void OnlyTheCurrentSchemaVersionIsSupported()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                ScanSnapshotSchema.IsSupported(ScanSnapshotSchema.CurrentVersion),
                Is.True);
            Assert.That(
                ScanSnapshotSchema.IsSupported(ScanSnapshotSchema.CurrentVersion + 1),
                Is.False);
            Assert.That(ScanSnapshotSchema.IsSupported(0), Is.False);
        });
    }

    [Test]
    public void UndeterminedIsTheDefaultCompleteness()
    {
        Assert.That(default(ScanCompleteness), Is.EqualTo(ScanCompleteness.Undetermined));
    }

    [Test]
    public void DescriptorSurfacesTheListingFieldsFromItsMetadata()
    {
        var descriptor = new ScanSnapshotDescriptor(Metadata(), 12_582_912);

        Assert.Multiple(() =>
        {
            Assert.That(descriptor.SnapshotId, Is.EqualTo("20260805T140200Z-abcd1234"));
            Assert.That(descriptor.RootPath, Is.EqualTo("/scan"));
            Assert.That(descriptor.ScanCompletedAt, Is.EqualTo(Completed));
            Assert.That(descriptor.CapturedAt, Is.EqualTo(Captured));
            Assert.That(descriptor.ItemCount, Is.EqualTo(512));
            Assert.That(descriptor.StoredSizeBytes, Is.EqualTo(12_582_912));
            Assert.That(
                descriptor.MeasurementMode,
                Is.EqualTo(StorageMeasurementMode.SharedAwareAllocated));
            Assert.That(descriptor.IsComplete, Is.False);
        });
    }

    [Test]
    public void DescriptorReportsACompleteScanAsComplete()
    {
        var descriptor = new ScanSnapshotDescriptor(
            Metadata(completeness: ScanCompleteness.Complete),
            1024);

        Assert.That(descriptor.IsComplete, Is.True);
    }

    [Test]
    public void DescriptorRejectsANegativeStoredSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new ScanSnapshotDescriptor(Metadata(), -1));
    }

    private static ScanSnapshotMetadata Metadata(
        string snapshotId = "20260805T140200Z-abcd1234",
        string rootPath = "/scan",
        long itemCount = 512,
        long totalCountedSizeBytes = 4096,
        long errorCount = 2,
        ScanCompleteness completeness =
            ScanCompleteness.IncompleteRecoverableErrors) =>
        new(
            snapshotId,
            Captured,
            rootPath,
            Completed,
            new ScanOptions
            {
                IncludeHiddenFiles = true,
                MeasurementMode = StorageMeasurementMode.SharedAwareAllocated
            },
            StorageMeasurementMode.SharedAwareAllocated,
            CloneAccountingCoverage.Available,
            itemCount,
            totalCountedSizeBytes,
            errorCount,
            completeness);
}
