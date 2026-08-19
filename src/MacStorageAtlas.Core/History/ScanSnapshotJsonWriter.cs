using System.IO.Compression;
using System.Text.Json;
using MacStorageAtlas.Core.Export;
using MacStorageAtlas.Core.Scanning;
using MacStorageAtlas.Core.Serialization;

namespace MacStorageAtlas.Core.History;

public static class ScanSnapshotJsonWriter
{
    public static async Task WriteAsync(
        ScanSnapshotRequest request,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(stream);

        var compressor = new GZipStream(stream, CompressionLevel.Optimal, leaveOpen: true);

        await using (compressor.ConfigureAwait(false))
        {
            var writer = new Utf8JsonWriter(
                compressor,
                new JsonWriterOptions { Indented = false });

            await using (writer.ConfigureAwait(false))
            {
                writer.WriteStartObject();
                writer.WriteNumber("schemaVersion", request.Metadata.SchemaVersion);

                WriteScan(writer, request.Metadata);
                ScanDocumentJson.WriteErrors(writer, request.Errors);

                writer.WriteStartArray("items");

                foreach (var row in request.Rows)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    ScanDocumentJson.WriteRow(writer, row);

                    if (writer.BytesPending > 16 * 1024)
                    {
                        await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
                    }
                }

                writer.WriteEndArray();
                writer.WriteEndObject();

                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static void WriteScan(Utf8JsonWriter writer, ScanSnapshotMetadata metadata)
    {
        writer.WriteStartObject("scan");
        writer.WriteString("snapshotId", metadata.SnapshotId);
        writer.WriteString("capturedAt", ScanExportValues.Timestamp(metadata.CapturedAt));
        writer.WriteString("rootPath", metadata.RootPath);
        writer.WriteString(
            "completedAt",
            ScanExportValues.Timestamp(metadata.ScanCompletedAt));

        ScanDocumentJson.WriteOptions(writer, metadata.Options);

        writer.WriteString("measurementMode", metadata.MeasurementMode.ToString());
        writer.WriteString(
            "cloneAccountingCoverage",
            metadata.CloneAccountingCoverage.ToString());
        writer.WriteString("completeness", metadata.Completeness.ToString());
        writer.WriteNumber("itemCount", metadata.ItemCount);
        writer.WriteNumber("totalCountedSizeBytes", metadata.TotalCountedSizeBytes);
        writer.WriteNumber("errorCount", metadata.ErrorCount);
        writer.WriteEndObject();
    }
}
