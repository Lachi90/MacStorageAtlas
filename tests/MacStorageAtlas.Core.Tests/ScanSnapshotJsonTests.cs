using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.Core.Tests;

public class ScanSnapshotJsonTests
{
    private static readonly DateTimeOffset Captured =
        new(2026, 8, 5, 14, 2, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Completed =
        new(2026, 8, 5, 14, 1, 30, TimeSpan.Zero);
    private static readonly DateTimeOffset Modified =
        new(2026, 3, 4, 5, 6, 7, TimeSpan.Zero);

    [Test]
    public async Task ASnapshotReadsBackWithEveryMetadataFieldUnchanged()
    {
        var written = Metadata();

        var result = ScanSnapshotJsonReader.Read(
            await WriteAsync(new ScanSnapshotRequest(written, Rows())));

        Assert.That(result.IsOk, Is.True);

        var read = result.Payload!.Metadata;

        Assert.Multiple(() =>
        {
            Assert.That(read.SnapshotId, Is.EqualTo(written.SnapshotId));
            Assert.That(read.CapturedAt, Is.EqualTo(written.CapturedAt));
            Assert.That(read.RootPath, Is.EqualTo(written.RootPath));
            Assert.That(read.ScanCompletedAt, Is.EqualTo(written.ScanCompletedAt));
            Assert.That(read.Options, Is.EqualTo(written.Options));
            Assert.That(read.MeasurementMode, Is.EqualTo(written.MeasurementMode));
            Assert.That(
                read.CloneAccountingCoverage,
                Is.EqualTo(written.CloneAccountingCoverage));
            Assert.That(read.ItemCount, Is.EqualTo(written.ItemCount));
            Assert.That(
                read.TotalCountedSizeBytes,
                Is.EqualTo(written.TotalCountedSizeBytes));
            Assert.That(read.ErrorCount, Is.EqualTo(written.ErrorCount));
            Assert.That(read.Completeness, Is.EqualTo(written.Completeness));
            Assert.That(read.SchemaVersion, Is.EqualTo(ScanSnapshotSchema.CurrentVersion));
        });
    }

    [Test]
    public async Task ASnapshotReadsBackWithEveryItemFieldUnchanged()
    {
        var rows = Rows();

        var result = ScanSnapshotJsonReader.Read(
            await WriteAsync(new ScanSnapshotRequest(Metadata(), rows)));

        Assert.That(result.Payload!.Items, Is.EqualTo(rows));
    }

    [Test]
    public async Task AbsentTimestampsAndCategoriesSurviveTheRoundTrip()
    {
        var directory = new ScanExportRow(
            "/scan",
            "scan",
            DiskItemKind.Directory,
            0,
            StorageMeasurementMode.SharedAwareAllocated,
            8192,
            8192,
            0,
            false,
            string.Empty,
            null,
            null,
            null,
            null);

        var result = ScanSnapshotJsonReader.Read(
            await WriteAsync(new ScanSnapshotRequest(Metadata(), [directory])));

        var read = result.Payload!.Items[0];

        Assert.Multiple(() =>
        {
            Assert.That(read.Category, Is.Null);
            Assert.That(read.CreatedUtc, Is.Null);
            Assert.That(read.ModifiedUtc, Is.Null);
            Assert.That(read.LastAccessedUtc, Is.Null);
            Assert.That(read, Is.EqualTo(directory));
        });
    }

    [Test]
    public async Task RecoverableErrorsSurviveTheRoundTrip()
    {
        ScanError[] errors =
        [
            new("/scan/locked", "Access denied.", "UnauthorizedAccessException")
        ];

        var result = ScanSnapshotJsonReader.Read(
            await WriteAsync(new ScanSnapshotRequest(Metadata(), Rows(), errors)));

        Assert.That(result.Payload!.Errors, Is.EqualTo(errors));
    }

    [Test]
    public async Task ASnapshotIsCompressed()
    {
        var stream = await WriteAsync(new ScanSnapshotRequest(Metadata(), Rows()));
        var header = new byte[2];
        var read = stream.Read(header, 0, 2);
        stream.Position = 0;

        Assert.Multiple(() =>
        {
            Assert.That(read, Is.EqualTo(2));
            Assert.That(header[0], Is.EqualTo(0x1f));
            Assert.That(header[1], Is.EqualTo(0x8b));
        });
    }

    [Test]
    public async Task ADescriptorReadsTheMetadataWithoutReadingItems()
    {
        var stream = await WriteAsync(
            new ScanSnapshotRequest(Metadata(itemCount: 3), ManyRows(5000)));

        var result = ScanSnapshotJsonReader.ReadDescriptor(stream, 4096);

        Assert.That(result.IsOk, Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Payload!.SnapshotId,
                Is.EqualTo("20260805T140200Z-abcd1234"));
            Assert.That(result.Payload!.RootPath, Is.EqualTo("/scan"));
            Assert.That(result.Payload!.ScanCompletedAt, Is.EqualTo(Completed));
            Assert.That(result.Payload!.ItemCount, Is.EqualTo(3));
            Assert.That(result.Payload!.StoredSizeBytes, Is.EqualTo(4096));
            Assert.That(
                result.Payload!.MeasurementMode,
                Is.EqualTo(StorageMeasurementMode.SharedAwareAllocated));
            Assert.That(result.Payload!.IsComplete, Is.False);
        });
    }

    [Test]
    public async Task ADescriptorReportsACompleteScanAsComplete()
    {
        var stream = await WriteAsync(new ScanSnapshotRequest(
            Metadata(completeness: ScanCompleteness.Complete),
            Rows()));

        var result = ScanSnapshotJsonReader.ReadDescriptor(stream, 1024);

        Assert.That(result.Payload!.IsComplete, Is.True);
    }

    [Test]
    public void CancellingAWriteLeavesNoCompleteSnapshot()
    {
        using var cancellation = new CancellationTokenSource();
        using var destination = new MemoryStream();

        cancellation.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await ScanSnapshotJsonWriter.WriteAsync(
                new ScanSnapshotRequest(Metadata(), ManyRows(1000)),
                destination,
                cancellation.Token));

        destination.Position = 0;

        Assert.That(
            ScanSnapshotJsonReader.Read(destination).Status,
            Is.EqualTo(ScanSnapshotReadStatus.Unreadable));
    }

    [Test]
    public async Task ATruncatedSnapshotIsReportedAsUnreadable()
    {
        var stream = await WriteAsync(
            new ScanSnapshotRequest(Metadata(), ManyRows(500)));
        var truncated = new MemoryStream(stream.ToArray()[..(int)(stream.Length / 2)]);

        Assert.Multiple(() =>
        {
            Assert.That(
                ScanSnapshotJsonReader.Read(truncated).Status,
                Is.EqualTo(ScanSnapshotReadStatus.Unreadable));
            Assert.That(
                ScanSnapshotJsonReader.Read(truncated).Message,
                Is.Not.Null.And.Not.Empty);
        });
    }

    [Test]
    public void ACorruptSnapshotIsReportedAsUnreadable()
    {
        using var corrupt = new MemoryStream(Encoding.UTF8.GetBytes("not a snapshot"));

        var result = ScanSnapshotJsonReader.Read(corrupt);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(ScanSnapshotReadStatus.Unreadable));
            Assert.That(result.Payload, Is.Null);
        });
    }

    [Test]
    public void ACorruptSnapshotIsReportedAsUnreadableWhenReadingADescriptor()
    {
        using var corrupt = new MemoryStream(Encoding.UTF8.GetBytes("not a snapshot"));

        Assert.That(
            ScanSnapshotJsonReader.ReadDescriptor(corrupt, 16).Status,
            Is.EqualTo(ScanSnapshotReadStatus.Unreadable));
    }

    [Test]
    public void AnUnreadableSchemaVersionStatesTheVersionItFound()
    {
        var stream = Compress(
            """
            {"schemaVersion":9999,"scan":{"snapshotId":"a","capturedAt":"x"},
            "errors":[],"items":[]}
            """);

        var result = ScanSnapshotJsonReader.Read(stream);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Status,
                Is.EqualTo(ScanSnapshotReadStatus.UnsupportedSchemaVersion));
            Assert.That(result.SchemaVersion, Is.EqualTo(9999));
            Assert.That(result.Payload, Is.Null);
            Assert.That(result.Message, Does.Contain("9999"));
        });
    }

    [Test]
    public void AnUnreadableSchemaVersionIsReportedWhenReadingADescriptor()
    {
        var stream = Compress(
            """
            {"schemaVersion":9999,"scan":{"snapshotId":"a"},"errors":[],"items":[]}
            """);

        var result = ScanSnapshotJsonReader.ReadDescriptor(stream, 16);

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Status,
                Is.EqualTo(ScanSnapshotReadStatus.UnsupportedSchemaVersion));
            Assert.That(result.SchemaVersion, Is.EqualTo(9999));
            Assert.That(result.Payload, Is.Null);
        });
    }

    [Test]
    public async Task TheDocumentOrdersMetadataBeforeItems()
    {
        var stream = await WriteAsync(new ScanSnapshotRequest(Metadata(), Rows()));
        using var decompressor = new GZipStream(stream, CompressionMode.Decompress);
        using var text = new StreamReader(decompressor, Encoding.UTF8);
        var json = await text.ReadToEndAsync();

        Assert.Multiple(() =>
        {
            Assert.That(json.IndexOf("\"scan\"", StringComparison.Ordinal), Is.LessThan(
                json.IndexOf("\"items\"", StringComparison.Ordinal)));
            Assert.That(json.IndexOf("\"errors\"", StringComparison.Ordinal), Is.LessThan(
                json.IndexOf("\"items\"", StringComparison.Ordinal)));
        });
    }

    private static async Task<MemoryStream> WriteAsync(ScanSnapshotRequest request)
    {
        var destination = new MemoryStream();
        await ScanSnapshotJsonWriter.WriteAsync(request, destination);
        destination.Position = 0;
        return destination;
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

    private static ScanExportRow[] Rows() =>
    [
        new(
            "/scan",
            "scan",
            DiskItemKind.Directory,
            0,
            StorageMeasurementMode.SharedAwareAllocated,
            8192,
            8192,
            0,
            false,
            string.Empty,
            null,
            null,
            Modified,
            null),
        new(
            "/scan/report.pdf",
            "report.pdf",
            DiskItemKind.File,
            1,
            StorageMeasurementMode.SharedAwareAllocated,
            4096,
            4096,
            0,
            false,
            ".pdf",
            FileCategory.Document,
            Modified,
            Modified,
            Modified)
    ];

    private static IEnumerable<ScanExportRow> ManyRows(int count)
    {
        for (var index = 0; index < count; index++)
        {
            yield return new ScanExportRow(
                $"/scan/file-{index}.bin",
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
                Modified,
                null);
        }
    }

    private static ScanSnapshotMetadata Metadata(
        long itemCount = 2,
        ScanCompleteness completeness = ScanCompleteness.IncompleteRecoverableErrors) =>
        new(
            "20260805T140200Z-abcd1234",
            Captured,
            "/scan",
            Completed,
            new ScanOptions
            {
                IncludeHiddenFiles = true,
                MeasurementMode = StorageMeasurementMode.SharedAwareAllocated
            },
            StorageMeasurementMode.SharedAwareAllocated,
            CloneAccountingCoverage.Available,
            itemCount,
            12288,
            1,
            completeness);
}
