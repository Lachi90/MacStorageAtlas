using System.Text.Json;

namespace MacStorageAtlas.Core;

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
        WriteErrors(writer, request.Errors);

        writer.WriteStartArray("items");

        foreach (var row in request.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();

            WriteRow(writer, row);

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

        writer.WriteStartObject("options");
        writer.WriteBoolean("includeHiddenFiles", metadata.Options.IncludeHiddenFiles);
        writer.WriteBoolean("followSymbolicLinks", metadata.Options.FollowSymbolicLinks);
        writer.WriteBoolean(
            "treatPackagesAsDirectories",
            metadata.Options.TreatPackagesAsDirectories);
        writer.WriteEndObject();

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

    private static void WriteErrors(Utf8JsonWriter writer, IReadOnlyList<ScanError> errors)
    {
        writer.WriteStartArray("errors");

        foreach (var error in errors)
        {
            writer.WriteStartObject();
            writer.WriteString("path", error.Path);
            writer.WriteString("message", error.Message);
            writer.WriteString("exceptionType", error.ExceptionType);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteRow(Utf8JsonWriter writer, ScanExportRow row)
    {
        writer.WriteStartObject();
        writer.WriteString("path", row.Path);
        writer.WriteString("name", row.Name);
        writer.WriteString("kind", row.Kind.ToString());
        writer.WriteNumber("depth", row.Depth);
        writer.WriteString("measurementMode", row.MeasurementMode.ToString());
        writer.WriteNumber("measuredSizeBytes", row.MeasuredSizeBytes);
        writer.WriteNumber("countedSizeBytes", row.CountedSizeBytes);
        writer.WriteNumber("sharedSizeBytes", row.SharedSizeBytes);
        writer.WriteBoolean("isSharedStorage", row.IsSharedStorage);
        writer.WriteString("extension", row.Extension);

        if (row.Category is { } category)
        {
            writer.WriteString("category", category.ToString());
        }
        else
        {
            writer.WriteNull("category");
        }

        WriteNullableTimestamp(writer, "createdUtc", row.CreatedUtc);
        WriteNullableTimestamp(writer, "modifiedUtc", row.ModifiedUtc);
        WriteNullableTimestamp(writer, "lastAccessedUtc", row.LastAccessedUtc);
        writer.WriteEndObject();
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

    private static void WriteNullableTimestamp(
        Utf8JsonWriter writer,
        string propertyName,
        DateTimeOffset? value)
    {
        if (value is not null)
        {
            writer.WriteString(propertyName, ScanExportValues.Timestamp(value));
        }
        else
        {
            writer.WriteNull(propertyName);
        }
    }
}
