using MacStorageAtlas.Core.Export;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Tests.Export;

public class ScanExportModelTests
{
    private static readonly DateTimeOffset Reference =
        new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void ARowCarriesTheItemKindAsADomainValueRatherThanADisplayLabel()
    {
        var bundle = new DiskItem("Xcode.app", "/Applications/Xcode.app", isDirectory: true);
        bundle.Metadata = bundle.Metadata with { Kind = DiskItemKind.ApplicationBundle };

        var row = ScanExportRow.FromDiskItem(bundle, depth: 1, StorageMeasurementMode.Logical);

        Assert.Multiple(() =>
        {
            Assert.That(row.Kind, Is.EqualTo(DiskItemKind.ApplicationBundle));
            Assert.That(row.Kind.ToString(), Is.EqualTo("ApplicationBundle"));
        });
    }

    [Test]
    public void ARowCarriesTheFileCategoryAsADomainValue()
    {
        var file = new DiskItem("clip.MOV", "/scan/clip.MOV", isDirectory: false);

        var row = ScanExportRow.FromDiskItem(file, depth: 1, StorageMeasurementMode.Logical);

        Assert.Multiple(() =>
        {
            Assert.That(row.Category, Is.EqualTo(FileCategory.Video));
            Assert.That(row.Extension, Is.EqualTo(".mov"));
        });
    }

    [Test]
    public void AnUnrecognisedExtensionLeavesTheCategoryUnset()
    {
        var file = new DiskItem("data.unknownext", "/scan/data.unknownext", isDirectory: false);

        var row = ScanExportRow.FromDiskItem(file, depth: 1, StorageMeasurementMode.Logical);

        Assert.Multiple(() =>
        {
            Assert.That(row.Category, Is.Null);
            Assert.That(row.Extension, Is.EqualTo(".unknownext"));
        });
    }

    [Test]
    public void AFileWithoutAnExtensionReportsAnEmptyExtension()
    {
        var file = new DiskItem("LICENSE", "/scan/LICENSE", isDirectory: false);

        var row = ScanExportRow.FromDiskItem(file, depth: 1, StorageMeasurementMode.Logical);

        Assert.Multiple(() =>
        {
            Assert.That(row.Extension, Is.Empty);
            Assert.That(row.Category, Is.Null);
        });
    }

    [Test]
    public void ADirectoryReportsNeitherExtensionNorCategory()
    {
        var directory = new DiskItem("archive.zip", "/scan/archive.zip", isDirectory: true);

        var row = ScanExportRow.FromDiskItem(directory, depth: 1, StorageMeasurementMode.Logical);

        Assert.Multiple(() =>
        {
            Assert.That(row.Extension, Is.Empty);
            Assert.That(row.Category, Is.Null);
        });
    }

    [Test]
    public void AnUnknownTimestampStaysUnsetRatherThanDefaultingToAnInstant()
    {
        var file = new DiskItem("a.txt", "/scan/a.txt", isDirectory: false);
        file.Metadata = file.Metadata with
        {
            CreatedTimeUtc = null,
            ModifiedTimeUtc = Reference,
            LastAccessTimeUtc = null
        };

        var row = ScanExportRow.FromDiskItem(file, depth: 1, StorageMeasurementMode.Logical);

        Assert.Multiple(() =>
        {
            Assert.That(row.CreatedUtc, Is.Null);
            Assert.That(row.ModifiedUtc, Is.EqualTo(Reference));
            Assert.That(row.LastAccessedUtc, Is.Null);
        });
    }

    [Test]
    public void ARowCarriesTheThreeSizesAndTheSharedIndicatorFromTheItem()
    {
        var file = new DiskItem("clone.bin", "/scan/clone.bin", isDirectory: false)
        {
            MeasuredSizeBytes = 4096,
            SizeBytes = 1024,
            SharedSizeBytes = 3072
        };

        var row = ScanExportRow.FromDiskItem(
            file,
            depth: 2,
            StorageMeasurementMode.SharedAwareAllocated);

        Assert.Multiple(() =>
        {
            Assert.That(row.MeasuredSizeBytes, Is.EqualTo(4096));
            Assert.That(row.CountedSizeBytes, Is.EqualTo(1024));
            Assert.That(row.SharedSizeBytes, Is.EqualTo(3072));
            Assert.That(row.IsSharedStorage, Is.True);
            Assert.That(
                row.MeasurementMode,
                Is.EqualTo(StorageMeasurementMode.SharedAwareAllocated));
            Assert.That(row.Depth, Is.EqualTo(2));
        });
    }

    [Test]
    public void ANegativeDepthIsRejected()
    {
        var file = new DiskItem("a.txt", "/scan/a.txt", isDirectory: false);

        Assert.Throws<ArgumentOutOfRangeException>(
            () => ScanExportRow.FromDiskItem(file, depth: -1, StorageMeasurementMode.Logical));
    }

    [Test]
    public void MetadataDefaultsToTheCurrentSchemaVersion()
    {
        var metadata = CreateMetadata();

        Assert.Multiple(() =>
        {
            Assert.That(metadata.SchemaVersion, Is.EqualTo(1));
            Assert.That(
                metadata.SchemaVersion,
                Is.EqualTo(ScanExportMetadata.CurrentSchemaVersion));
        });
    }

    [Test]
    public void ARequestWithoutErrorsReportsAnEmptyErrorList()
    {
        var request = new ScanExportRequest(CreateMetadata(), []);

        Assert.That(request.Errors, Is.Empty);
    }

    private static ScanExportMetadata CreateMetadata() =>
        new(
            "/scan",
            Reference,
            ScanOptions.Default,
            StorageMeasurementMode.Logical,
            CloneAccountingCoverage.Unavailable,
            ScanExportScope.Full,
            Filter: null,
            ItemCount: 0,
            TotalCountedSizeBytes: 0);
}
