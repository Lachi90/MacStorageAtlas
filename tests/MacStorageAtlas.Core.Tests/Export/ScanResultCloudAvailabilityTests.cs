using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using MacStorageAtlas.Core.Export;
using MacStorageAtlas.Core.Filtering;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Tests.Export;

public partial class ScanResultCloudAvailabilityTests
{
    private static readonly DateTimeOffset Completed =
        new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public void AnExportRowTakesTheFileOrFolderAvailability()
    {
        var folder = new DiskItem("folder", "/scan/folder", isDirectory: true);
        var placeholder = new DiskItem("a.pdf", "/scan/folder/a.pdf", isDirectory: false)
        {
            Metadata = DiskItemMetadata.Unknown(DiskItemKind.File) with
            {
                CloudAvailability = CloudAvailability.CloudOnly
            }
        };
        var local = new DiskItem("b.pdf", "/scan/folder/b.pdf", isDirectory: false)
        {
            Metadata = DiskItemMetadata.Unknown(DiskItemKind.File) with
            {
                CloudAvailability = CloudAvailability.Local
            }
        };
        folder.AddChild(placeholder);
        folder.AddChild(local);
        folder.AddCloudTotals(placeholder.CloudTotals);
        folder.AddCloudTotals(local.CloudTotals);

        Assert.Multiple(() =>
        {
            Assert.That(
                ScanExportRow.FromDiskItem(placeholder, 2, StorageMeasurementMode.Logical).Availability,
                Is.EqualTo(CloudAvailability.CloudOnly));
            Assert.That(
                ScanExportRow.FromDiskItem(folder, 1, StorageMeasurementMode.Logical).Availability,
                Is.EqualTo(CloudAvailability.PartlyLocal));
        });
    }

    [Test]
    public async Task AJsonExportWritesEachItemAvailabilityUnderSchemaVersionThree()
    {
        var json = await WriteAsync(Metadata(), [Row("/scan/a.pdf", CloudAvailability.CloudOnly)]);
        using var document = JsonDocument.Parse(json);

        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(3));
            Assert.That(
                document.RootElement.GetProperty("items")[0].GetProperty("availability").GetString(),
                Is.EqualTo("CloudOnly"));
        });
    }

    [Test]
    public async Task AJsonExportReadsBackEachAvailability()
    {
        var rows = new[]
        {
            Row("/scan/a.pdf", CloudAvailability.CloudOnly),
            Row("/scan/b.pdf", CloudAvailability.Local),
            Row("/scan/c.pdf", CloudAvailability.Unknown)
        };

        var document = Read(await WriteAsync(Metadata(), rows));

        Assert.That(document.Items, Is.EqualTo(rows));
    }

    [TestCase(1)]
    [TestCase(2)]
    public async Task AnOlderJsonExportReadsWithUnknownAvailability(int schemaVersion)
    {
        var json = await WriteAsync(
            Metadata() with { SchemaVersion = schemaVersion },
            [Row("/scan/a.pdf", CloudAvailability.CloudOnly)]);
        var older = AvailabilityField().Replace(json, string.Empty);

        var document = Read(older);

        Assert.Multiple(() =>
        {
            Assert.That(older, Does.Not.Contain("availability"));
            Assert.That(document.Metadata.SchemaVersion, Is.EqualTo(schemaVersion));
            Assert.That(document.Items.Single().Availability, Is.EqualTo(CloudAvailability.Unknown));
        });
    }

    [Test]
    public async Task AFilteredExportRecordsTheCloudAvailabilityCriterion()
    {
        var metadata = Metadata() with
        {
            Scope = ScanExportScope.Filtered,
            Filter = new DiskItemFilter { CloudAvailability = CloudAvailabilityCriterion.CloudOnly }
        };

        var document = Read(await WriteAsync(metadata, []));

        Assert.That(
            document.Metadata.Filter!.CloudAvailability,
            Is.EqualTo(CloudAvailabilityCriterion.CloudOnly));
    }

    [Test]
    public async Task AnOlderFilteredExportWithoutTheCriterionReadsAsAny()
    {
        var metadata = Metadata() with
        {
            Scope = ScanExportScope.Filtered,
            Filter = new DiskItemFilter { MinimumSizeBytes = 10 },
            SchemaVersion = 2
        };
        var json = CriterionField().Replace(await WriteAsync(metadata, []), string.Empty);

        var document = Read(json);

        Assert.That(
            document.Metadata.Filter!.CloudAvailability,
            Is.EqualTo(CloudAvailabilityCriterion.Any));
    }

    private static ScanExportDocument Read(string json) =>
        ScanResultJsonReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(json)));

    private static async Task<string> WriteAsync(
        ScanExportMetadata metadata,
        IEnumerable<ScanExportRow> rows)
    {
        using var stream = new MemoryStream();
        await ScanResultJsonWriter.WriteAsync(new ScanExportRequest(metadata, rows), stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static ScanExportRow Row(string path, CloudAvailability availability) =>
        new(
            path,
            Path.GetFileName(path),
            DiskItemKind.File,
            Depth: 1,
            StorageMeasurementMode.Logical,
            MeasuredSizeBytes: 100,
            CountedSizeBytes: 100,
            SharedSizeBytes: 0,
            IsSharedStorage: false,
            ".pdf",
            FileCategory.Document,
            null,
            Completed,
            null,
            availability);

    private static ScanExportMetadata Metadata() =>
        new(
            "/scan",
            Completed,
            ScanOptions.Default,
            StorageMeasurementMode.Logical,
            CloneAccountingCoverage.Unavailable,
            ScanExportScope.Full,
            Filter: null,
            ItemCount: 1,
            TotalCountedSizeBytes: 100);

    [GeneratedRegex(",\\s*\"availability\":\\s*\"[A-Za-z]+\"")]
    private static partial Regex AvailabilityField();

    [GeneratedRegex(",\\s*\"cloudAvailability\":\\s*\"[A-Za-z]+\"")]
    private static partial Regex CriterionField();
}
