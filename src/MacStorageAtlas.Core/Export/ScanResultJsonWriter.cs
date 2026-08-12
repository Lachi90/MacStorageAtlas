using System.Text.Json;
using MacStorageAtlas.Core.Filtering;
using MacStorageAtlas.Core.Scanning;
using MacStorageAtlas.Core.Serialization;

namespace MacStorageAtlas.Core.Export;

public static class ScanResultJsonWriter
{
    public static async Task WriteAsync(
        ScanExportRequest request,
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(stream);

        await using var writer = new Utf8JsonWriter(
            stream,
            new JsonWriterOptions { Indented = true });

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

    private static void WriteScan(Utf8JsonWriter writer, ScanExportMetadata metadata)
    {
        writer.WriteStartObject("scan");
        writer.WriteString("rootPath", metadata.RootPath);
        writer.WriteString("completedAt", ScanExportValues.Timestamp(metadata.ScanCompletedAt));

        ScanDocumentJson.WriteOptions(writer, metadata.Options);

        writer.WriteString("measurementMode", metadata.MeasurementMode.ToString());
        writer.WriteString(
            "cloneAccountingCoverage",
            metadata.CloneAccountingCoverage.ToString());
        writer.WriteString("scope", metadata.Scope.ToString());

        WriteFilter(writer, metadata.Filter);

        writer.WriteNumber("itemCount", metadata.ItemCount);
        writer.WriteNumber("totalCountedSizeBytes", metadata.TotalCountedSizeBytes);
        writer.WriteEndObject();
    }

    private static void WriteFilter(Utf8JsonWriter writer, DiskItemFilter? filter)
    {
        if (filter is null)
        {
            writer.WriteNull("filter");
            return;
        }

        writer.WriteStartObject("filter");

        if (filter.TextTerm is { } textTerm)
        {
            writer.WriteString("textTerm", textTerm);
        }
        else
        {
            writer.WriteNull("textTerm");
        }

        WriteNullableNumber(writer, "minimumSizeBytes", filter.MinimumSizeBytes);
        WriteNullableNumber(writer, "maximumSizeBytes", filter.MaximumSizeBytes);
        WriteCriterion(writer, "createdAfter", filter.CreatedAfter);
        WriteCriterion(writer, "createdBefore", filter.CreatedBefore);
        WriteCriterion(writer, "modifiedAfter", filter.ModifiedAfter);
        WriteCriterion(writer, "modifiedBefore", filter.ModifiedBefore);
        WriteCriterion(writer, "lastAccessedAfter", filter.LastAccessedAfter);
        WriteCriterion(writer, "lastAccessedBefore", filter.LastAccessedBefore);

        writer.WriteStartArray("extensions");
        foreach (var extension in filter.Extensions)
        {
            writer.WriteStringValue(extension);
        }

        writer.WriteEndArray();

        writer.WriteStartArray("categories");
        foreach (var category in filter.Categories)
        {
            writer.WriteStringValue(category.ToString());
        }

        writer.WriteEndArray();

        writer.WriteBoolean("sharedStorageOnly", filter.SharedStorageOnly);
        writer.WriteEndObject();
    }

    private static void WriteCriterion(
        Utf8JsonWriter writer,
        string propertyName,
        DateCriterion? criterion)
    {
        switch (criterion)
        {
            case null:
                writer.WriteNull(propertyName);
                return;
            case AbsoluteDateCriterion absolute:
                writer.WriteStartObject(propertyName);
                writer.WriteString("kind", "Absolute");
                writer.WriteString("instant", ScanExportValues.Timestamp(absolute.Instant));
                writer.WriteEndObject();
                return;
            case RelativeDateCriterion relative:
                writer.WriteStartObject(propertyName);
                writer.WriteString("kind", "Relative");
                writer.WriteNumber("count", relative.Count);
                writer.WriteString("unit", relative.Unit.ToString());
                writer.WriteEndObject();
                return;
            default:
                throw new NotSupportedException(
                    $"Unsupported date criterion '{criterion.GetType().Name}'.");
        }
    }

    private static void WriteNullableNumber(
        Utf8JsonWriter writer,
        string propertyName,
        long? value)
    {
        if (value is { } number)
        {
            writer.WriteNumber(propertyName, number);
        }
        else
        {
            writer.WriteNull(propertyName);
        }
    }
}
