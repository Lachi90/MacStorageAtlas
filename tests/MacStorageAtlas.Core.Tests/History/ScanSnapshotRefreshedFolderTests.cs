using System.IO;
using System.IO.Compression;
using System.Text;
using MacStorageAtlas.Core.Export;
using MacStorageAtlas.Core.History;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Tests.History;

public class ScanSnapshotRefreshedFolderTests
{
    private static readonly DateTimeOffset Completed =
        new(2026, 11, 2, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Refreshed =
        new(2026, 11, 2, 8, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Captured =
        new(2026, 11, 2, 8, 31, 0, TimeSpan.Zero);

    private const string VersionOneSnapshot =
        """
        {"schemaVersion":1,
        "scan":{"snapshotId":"20261102T080000Z-0123abcd",
        "capturedAt":"2026-11-02T08:01:00.0000000Z",
        "rootPath":"/scan",
        "completedAt":"2026-11-02T08:00:00.0000000Z",
        "options":{"includeHiddenFiles":false,"followSymbolicLinks":false,"treatPackagesAsDirectories":true},
        "measurementMode":"Logical",
        "cloneAccountingCoverage":"Unavailable",
        "completeness":"Complete",
        "itemCount":1,
        "totalCountedSizeBytes":0,
        "errorCount":0},
        "errors":[],
        "items":[{"path":"/scan","name":"scan","kind":"Directory","depth":0,
        "measurementMode":"Logical","measuredSizeBytes":0,"countedSizeBytes":0,
        "sharedSizeBytes":0,"isSharedStorage":false,"extension":"","category":null,
        "createdUtc":null,"modifiedUtc":null,"lastAccessedUtc":null}]}
        """;

    [Test]
    public async Task ASnapshotOfARefreshedResultReadsBackItsRefreshedFolders()
    {
        var written = Metadata([new RefreshedFolder("/scan/project", Refreshed)]);

        var result = ScanSnapshotJsonReader.Read(await WriteAsync(written));

        Assert.That(result.IsOk, Is.True);
        var read = result.Payload!.Metadata;
        Assert.Multiple(() =>
        {
            Assert.That(read.SchemaVersion, Is.EqualTo(2));
            Assert.That(read.ScanCompletedAt, Is.EqualTo(Completed));
            Assert.That(read.CapturedAt, Is.EqualTo(Captured));
            Assert.That(read.HasRefreshedFolders, Is.True);
            Assert.That(
                read.RefreshedFolders,
                Is.EqualTo(new[] { new RefreshedFolder("/scan/project", Refreshed) }));
        });
    }

    [Test]
    public async Task ADescriptorOfARefreshedResultStatesItsRefreshedFolders()
    {
        var stream = await WriteAsync(Metadata([
            new RefreshedFolder("/scan/a", Refreshed),
            new RefreshedFolder("/scan/b", Refreshed.AddMinutes(1))
        ]));

        var result = ScanSnapshotJsonReader.ReadDescriptor(stream, stream.Length);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsOk, Is.True);
            Assert.That(
                result.Payload!.Metadata.RefreshedFolders.Select(folder => folder.Path),
                Is.EqualTo(new[] { "/scan/a", "/scan/b" }));
        });
    }

    [Test]
    public async Task ASnapshotOfAFullScanRecordsAnEmptyRefreshedFolderList()
    {
        var stream = await WriteAsync(Metadata([]));

        var result = ScanSnapshotJsonReader.Read(stream);

        Assert.Multiple(() =>
        {
            Assert.That(result.Payload!.Metadata.RefreshedFolders, Is.Empty);
            Assert.That(result.Payload!.Metadata.HasRefreshedFolders, Is.False);
            Assert.That(DecompressedJson(stream), Does.Contain("\"refreshedFolders\":[]"));
        });
    }

    [Test]
    public void AVersionOneSnapshotReadsBackWithoutRefreshedFolders()
    {
        var result = ScanSnapshotJsonReader.Read(Compress(VersionOneSnapshot));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsOk, Is.True);
            Assert.That(result.SchemaVersion, Is.EqualTo(1));
            Assert.That(result.Payload!.Metadata.SchemaVersion, Is.EqualTo(1));
            Assert.That(result.Payload!.Metadata.RefreshedFolders, Is.Empty);
            Assert.That(result.Payload!.Items, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void AVersionOneDescriptorReadsBackWithoutRefreshedFolders()
    {
        var stream = Compress(VersionOneSnapshot);

        var result = ScanSnapshotJsonReader.ReadDescriptor(stream, stream.Length);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsOk, Is.True);
            Assert.That(result.Payload!.Metadata.RefreshedFolders, Is.Empty);
            Assert.That(result.Payload!.RootPath, Is.EqualTo("/scan"));
        });
    }

    [Test]
    public void AVersionAfterTheCurrentOneIsStillReportedAsUnsupported()
    {
        var stream = Compress(
            """
            {"schemaVersion":3,"scan":{"snapshotId":"a"},"errors":[],"items":[]}
            """);

        var result = ScanSnapshotJsonReader.Read(stream);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Status,
                Is.EqualTo(ScanSnapshotReadStatus.UnsupportedSchemaVersion));
            Assert.That(result.SchemaVersion, Is.EqualTo(3));
            Assert.That(result.Message, Does.Contain("versions 1 to 2"));
        });
    }

    private static ScanSnapshotMetadata Metadata(IReadOnlyList<RefreshedFolder> refreshedFolders) =>
        new(
            ScanSnapshotIdentity.Create(Completed),
            Captured,
            "/scan",
            Completed,
            ScanOptions.Default,
            StorageMeasurementMode.Logical,
            CloneAccountingCoverage.Unavailable,
            itemCount: 1,
            totalCountedSizeBytes: 0,
            errorCount: 0,
            ScanCompleteness.Complete)
        {
            RefreshedFolders = refreshedFolders
        };

    private static async Task<MemoryStream> WriteAsync(ScanSnapshotMetadata metadata)
    {
        var destination = new MemoryStream();
        await ScanSnapshotJsonWriter.WriteAsync(
            new ScanSnapshotRequest(metadata, [Row()]),
            destination);
        destination.Position = 0;
        return destination;
    }

    private static ScanExportRow Row() =>
        new(
            "/scan",
            "scan",
            DiskItemKind.Directory,
            0,
            StorageMeasurementMode.Logical,
            0,
            0,
            0,
            false,
            string.Empty,
            null,
            null,
            null,
            null);

    private static string DecompressedJson(MemoryStream stream)
    {
        stream.Position = 0;
        using var decompressor = new GZipStream(stream, CompressionMode.Decompress, leaveOpen: true);
        using var reader = new StreamReader(decompressor, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static MemoryStream Compress(string json)
    {
        var destination = new MemoryStream();

        using (var compressor = new GZipStream(
                   destination,
                   CompressionLevel.Optimal,
                   leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            compressor.Write(bytes, 0, bytes.Length);
        }

        destination.Position = 0;
        return destination;
    }
}
