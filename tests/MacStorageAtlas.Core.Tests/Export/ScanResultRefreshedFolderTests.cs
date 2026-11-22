using System.Text;
using System.Text.Json;
using MacStorageAtlas.Core.Export;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Tests.Export;

public class ScanResultRefreshedFolderTests
{
    private static readonly DateTimeOffset Completed =
        new(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Refreshed =
        new(2026, 11, 3, 10, 45, 0, TimeSpan.Zero);

    private const string VersionOneExport =
        """
        {"schemaVersion":1,
        "scan":{"rootPath":"/scan",
        "completedAt":"2026-11-03T10:00:00.0000000Z",
        "options":{"includeHiddenFiles":false,"followSymbolicLinks":false,"treatPackagesAsDirectories":true},
        "measurementMode":"Logical",
        "cloneAccountingCoverage":"Unavailable",
        "scope":"Full",
        "filter":null,
        "itemCount":0,
        "totalCountedSizeBytes":0},
        "errors":[],
        "items":[]}
        """;

    [Test]
    public async Task AJsonExportOfARefreshedResultListsItsRefreshedFolders()
    {
        var request = ScanExportRequestFactory.Create(
            Root(),
            ScanOptions.Default,
            StorageMeasurementMode.Logical,
            CloneAccountingCoverage.Unavailable,
            Completed,
            refreshedFolders: [new RefreshedFolder("/scan/project", Refreshed)]);

        var json = await WriteAsync(request);
        using var document = JsonDocument.Parse(json);
        var folders = document.RootElement.GetProperty("scan").GetProperty("refreshedFolders");

        Assert.Multiple(() =>
        {
            Assert.That(document.RootElement.GetProperty("schemaVersion").GetInt32(), Is.EqualTo(2));
            Assert.That(
                document.RootElement.GetProperty("scan").GetProperty("completedAt").GetString(),
                Is.EqualTo("2026-11-03T10:00:00.0000000Z"));
            Assert.That(folders.GetArrayLength(), Is.EqualTo(1));
            Assert.That(folders[0].GetProperty("path").GetString(), Is.EqualTo("/scan/project"));
            Assert.That(
                folders[0].GetProperty("completedAt").GetString(),
                Is.EqualTo("2026-11-03T10:45:00.0000000Z"));
        });
    }

    [Test]
    public async Task ARefreshedExportReadsBackWithItsRefreshedFolders()
    {
        var request = ScanExportRequestFactory.Create(
            Root(),
            ScanOptions.Default,
            StorageMeasurementMode.Logical,
            CloneAccountingCoverage.Unavailable,
            Completed,
            refreshedFolders: [new RefreshedFolder("/scan/project", Refreshed)]);

        var read = ScanResultJsonReader.Read(
            new MemoryStream(Encoding.UTF8.GetBytes(await WriteAsync(request))));

        Assert.Multiple(() =>
        {
            Assert.That(read.Metadata.SchemaVersion, Is.EqualTo(2));
            Assert.That(read.Metadata.ScanCompletedAt, Is.EqualTo(Completed));
            Assert.That(read.Metadata.HasRefreshedFolders, Is.True);
            Assert.That(
                read.Metadata.RefreshedFolders,
                Is.EqualTo(new[] { new RefreshedFolder("/scan/project", Refreshed) }));
        });
    }

    [Test]
    public async Task AnExportOfAFullScanRecordsAnEmptyRefreshedFolderList()
    {
        var request = ScanExportRequestFactory.Create(
            Root(),
            ScanOptions.Default,
            StorageMeasurementMode.Logical,
            CloneAccountingCoverage.Unavailable,
            Completed);

        var json = await WriteAsync(request);
        using var document = JsonDocument.Parse(json);

        Assert.Multiple(() =>
        {
            Assert.That(request.Metadata.RefreshedFolders, Is.Empty);
            Assert.That(
                document.RootElement.GetProperty("scan").GetProperty("refreshedFolders").GetArrayLength(),
                Is.Zero);
        });
    }

    [Test]
    public void AVersionOneExportReadsBackWithoutRefreshedFolders()
    {
        var read = ScanResultJsonReader.Read(
            new MemoryStream(Encoding.UTF8.GetBytes(VersionOneExport)));

        Assert.Multiple(() =>
        {
            Assert.That(read.Metadata.SchemaVersion, Is.EqualTo(1));
            Assert.That(read.Metadata.RootPath, Is.EqualTo("/scan"));
            Assert.That(read.Metadata.ScanCompletedAt, Is.EqualTo(Completed));
            Assert.That(read.Metadata.RefreshedFolders, Is.Empty);
            Assert.That(read.Metadata.HasRefreshedFolders, Is.False);
        });
    }

    private static DiskItem Root() => new("scan", "/scan", isDirectory: true);

    private static async Task<string> WriteAsync(ScanExportRequest request)
    {
        using var stream = new MemoryStream();
        await ScanResultJsonWriter.WriteAsync(request, stream);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
