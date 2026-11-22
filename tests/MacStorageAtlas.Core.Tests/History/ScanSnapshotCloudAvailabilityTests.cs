using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using MacStorageAtlas.Core.Export;
using MacStorageAtlas.Core.History;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Tests.History;

public partial class ScanSnapshotCloudAvailabilityTests
{
    private static readonly DateTimeOffset Captured =
        new(2026, 11, 22, 10, 5, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Completed =
        new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task AvailabilityReadsBackUnchanged()
    {
        var rows = Rows();

        var result = ScanSnapshotJsonReader.Read(await WriteAsync(Metadata(), rows));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsOk, Is.True);
            Assert.That(result.Payload!.Items, Is.EqualTo(rows));
            Assert.That(
                result.Payload.Items.Select(row => row.Availability),
                Is.EqualTo(new[]
                {
                    CloudAvailability.PartlyLocal,
                    CloudAvailability.Local,
                    CloudAvailability.CloudOnly,
                    CloudAvailability.Unknown
                }));
        });
    }

    [Test]
    public async Task ASnapshotIsWrittenUnderSchemaVersionThree()
    {
        var json = await DecompressAsync(await WriteAsync(Metadata(), Rows()));

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.StartWith("{\"schemaVersion\":3,"));
            Assert.That(json, Does.Contain("\"availability\":\"CloudOnly\""));
        });
    }

    [TestCase(1)]
    [TestCase(2)]
    public async Task AnOlderSnapshotReadsWithUnknownAvailability(int schemaVersion)
    {
        var stream = await OlderSnapshotAsync(schemaVersion);

        var result = ScanSnapshotJsonReader.Read(stream);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsOk, Is.True);
            Assert.That(result.SchemaVersion, Is.EqualTo(schemaVersion));
            Assert.That(
                result.Payload!.Items.Select(row => row.Availability),
                Is.All.EqualTo(CloudAvailability.Unknown));
        });
    }

    [Test]
    public async Task AnOlderSnapshotIsListedAsReadable()
    {
        var stream = await OlderSnapshotAsync(2);

        var result = ScanSnapshotJsonReader.ReadDescriptor(stream, stream.Length);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsOk, Is.True);
            Assert.That(result.Payload!.Metadata.SchemaVersion, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task ACompletenessWithUnlistedCloudFoldersReadsBack()
    {
        var written = Metadata(ScanCompleteness.IncompleteCloudFoldersNotListed);

        var result = ScanSnapshotJsonReader.Read(await WriteAsync(written, Rows()));

        Assert.That(
            result.Payload!.Metadata.Completeness,
            Is.EqualTo(ScanCompleteness.IncompleteCloudFoldersNotListed));
    }

    private static async Task<MemoryStream> OlderSnapshotAsync(int schemaVersion)
    {
        var json = await DecompressAsync(await WriteAsync(Metadata(), Rows()));
        var older = AvailabilityField().Replace(json, string.Empty)
            .Replace("{\"schemaVersion\":3,", $"{{\"schemaVersion\":{schemaVersion},", StringComparison.Ordinal);
        return Compress(older);
    }

    private static async Task<MemoryStream> WriteAsync(
        ScanSnapshotMetadata metadata,
        IEnumerable<ScanExportRow> rows)
    {
        var destination = new MemoryStream();
        await ScanSnapshotJsonWriter.WriteAsync(new ScanSnapshotRequest(metadata, rows), destination);
        destination.Position = 0;
        return destination;
    }

    private static async Task<string> DecompressAsync(Stream stream)
    {
        await using var decompressor = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(decompressor, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static MemoryStream Compress(string json)
    {
        var destination = new MemoryStream();
        using (var compressor = new GZipStream(destination, CompressionLevel.Optimal, leaveOpen: true))
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            compressor.Write(bytes, 0, bytes.Length);
        }

        destination.Position = 0;
        return destination;
    }

    private static ScanExportRow[] Rows() =>
    [
        Row("/scan", DiskItemKind.Directory, 0, CloudAvailability.PartlyLocal),
        Row("/scan/local.pdf", DiskItemKind.File, 1, CloudAvailability.Local),
        Row("/scan/placeholder.pdf", DiskItemKind.File, 1, CloudAvailability.CloudOnly),
        Row("/scan/unreadable.pdf", DiskItemKind.File, 1, CloudAvailability.Unknown)
    ];

    private static ScanExportRow Row(
        string path,
        DiskItemKind kind,
        int depth,
        CloudAvailability availability) =>
        new(
            path,
            Path.GetFileName(path) is { Length: > 0 } name ? name : path,
            kind,
            depth,
            StorageMeasurementMode.Logical,
            100,
            100,
            0,
            false,
            kind == DiskItemKind.File ? ".pdf" : string.Empty,
            kind == DiskItemKind.File ? FileCategory.Document : null,
            null,
            Completed,
            null,
            availability);

    private static ScanSnapshotMetadata Metadata(
        ScanCompleteness completeness = ScanCompleteness.Complete) =>
        new(
            "20261122T100500Z-cloud",
            Captured,
            "/scan",
            Completed,
            ScanOptions.Default,
            StorageMeasurementMode.Logical,
            CloneAccountingCoverage.Unavailable,
            4,
            300,
            0,
            completeness);

    [GeneratedRegex(",\"availability\":\"[A-Za-z]+\"")]
    private static partial Regex AvailabilityField();
}
