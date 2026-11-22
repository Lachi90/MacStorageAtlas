using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Platform.Mac.Tests;

public sealed class MacCloudAvailabilityIntegrationTests
{
    private const string PlaceholderPathVariable = "MACSTORAGEATLAS_CLOUD_PLACEHOLDER_PATH";
    private string _temporaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"MacStorageAtlas-cloud-availability-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public async Task ReadCloudAvailabilityReportsLocalForAnOrdinaryFileOnMacOs()
    {
        RequireMacOs();
        var path = Path.Combine(_temporaryDirectory, "local.bin");
        await File.WriteAllBytesAsync(path, new byte[12_345]);

        var reading = new MacFileMetadataReader().ReadCloudAvailability(path);

        Assert.Multiple(() =>
        {
            Assert.That(reading.Availability, Is.EqualTo(CloudAvailability.Local));
            Assert.That(reading.LogicalSizeBytes, Is.EqualTo(12_345));
        });
    }

    [Test]
    public async Task ReadCarriesLocalAvailabilityAndLogicalLengthForAnOrdinaryFileOnMacOs()
    {
        RequireMacOs();
        var path = Path.Combine(_temporaryDirectory, "local.bin");
        await File.WriteAllBytesAsync(path, new byte[54_321]);

        var metadata = new MacFileMetadataReader().Read(path);

        Assert.That(
            metadata.CloudAvailability,
            Is.EqualTo(new CloudAvailabilityReading(CloudAvailability.Local, 54_321)));
    }

    [Test]
    public void ReadCloudAvailabilityReportsUnknownForAMissingPathOnMacOs()
    {
        RequireMacOs();
        var path = Path.Combine(_temporaryDirectory, "missing.bin");

        var reading = new MacFileMetadataReader().ReadCloudAvailability(path);

        Assert.That(reading, Is.EqualTo(CloudAvailabilityReading.Unknown));
    }

    [TestCase(StorageMeasurementMode.Logical)]
    [TestCase(StorageMeasurementMode.Allocated)]
    [TestCase(StorageMeasurementMode.SharedAwareAllocated)]
    public async Task ScanMarksOrdinaryFilesAndFoldersAsLocalOnMacOs(
        StorageMeasurementMode measurementMode)
    {
        RequireMacOs();
        var folder = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "folder"));
        await File.WriteAllBytesAsync(Path.Combine(folder.FullName, "a.bin"), new byte[100]);
        await File.WriteAllBytesAsync(Path.Combine(_temporaryDirectory, "b.bin"), new byte[200]);
        var reader = new MacFileMetadataReader();
        var scanner = new DiskScanner(reader, reader);

        ScanProgress? last = null;
        await foreach (var progress in scanner.ScanAsync(
                           _temporaryDirectory,
                           new ScanOptions { MeasurementMode = measurementMode }))
        {
            last = progress;
        }

        var root = last!.Root;
        Assert.Multiple(() =>
        {
            Assert.That(root.CloudAvailability, Is.EqualTo(CloudAvailability.Local));
            Assert.That(root.CloudTotals.LocalFileCount, Is.EqualTo(2));
            Assert.That(root.CloudTotals.CloudOnlyFileCount, Is.Zero);
            Assert.That(last.UnlistedCloudFolders, Is.Empty);
        });
    }

    [Test]
    public void ReadCloudAvailabilityReportsCloudOnlyForAProvidedPlaceholderOnMacOs()
    {
        RequireMacOs();
        var placeholderPath = Environment.GetEnvironmentVariable(PlaceholderPathVariable);
        if (string.IsNullOrWhiteSpace(placeholderPath) || !File.Exists(placeholderPath))
        {
            Assert.Ignore(
                "No cloud-only placeholder is available. Set "
                + PlaceholderPathVariable
                + " to a file that is not downloaded to run this check.");
        }

        var reading = new MacFileMetadataReader().ReadCloudAvailability(placeholderPath);

        Assert.That(reading.Availability, Is.EqualTo(CloudAvailability.CloudOnly));
    }

    private static void RequireMacOs()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Ignore("macOS-specific cloud availability integration.");
        }
    }
}
