using System.Text.Json;

namespace MacStorageAtlas.Core;

internal static class ScanDocumentJson
{
    public static void WriteOptions(Utf8JsonWriter writer, ScanOptions options)
    {
        writer.WriteStartObject("options");
        writer.WriteBoolean("includeHiddenFiles", options.IncludeHiddenFiles);
        writer.WriteBoolean("followSymbolicLinks", options.FollowSymbolicLinks);
        writer.WriteBoolean(
            "treatPackagesAsDirectories",
            options.TreatPackagesAsDirectories);
        writer.WriteEndObject();
    }

    public static ScanOptions ReadOptions(
        JsonElement scan,
        StorageMeasurementMode measurementMode)
    {
        var options = scan.GetProperty("options");

        return new ScanOptions
        {
            IncludeHiddenFiles = options.GetProperty("includeHiddenFiles").GetBoolean(),
            FollowSymbolicLinks = options.GetProperty("followSymbolicLinks").GetBoolean(),
            TreatPackagesAsDirectories =
                options.GetProperty("treatPackagesAsDirectories").GetBoolean(),
            MeasurementMode = measurementMode
        };
    }

    public static void WriteErrors(
        Utf8JsonWriter writer,
        IReadOnlyList<ScanError> errors)
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

    public static ScanError[] ReadErrors(JsonElement root) =>
        root.GetProperty("errors")
            .EnumerateArray()
            .Select(error => new ScanError(
                error.GetProperty("path").GetString()!,
                error.GetProperty("message").GetString()!,
                error.GetProperty("exceptionType").GetString()!))
            .ToArray();

    public static void WriteRow(Utf8JsonWriter writer, ScanExportRow row)
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

    public static ScanExportRow ReadRow(JsonElement item) =>
        new(
            item.GetProperty("path").GetString()!,
            item.GetProperty("name").GetString()!,
            ReadEnum<DiskItemKind>(item, "kind"),
            item.GetProperty("depth").GetInt32(),
            ReadEnum<StorageMeasurementMode>(item, "measurementMode"),
            item.GetProperty("measuredSizeBytes").GetInt64(),
            item.GetProperty("countedSizeBytes").GetInt64(),
            item.GetProperty("sharedSizeBytes").GetInt64(),
            item.GetProperty("isSharedStorage").GetBoolean(),
            item.GetProperty("extension").GetString()!,
            ScanExportValues.ParseCategory(item.GetProperty("category").GetString()),
            ScanExportValues.ParseTimestamp(item.GetProperty("createdUtc").GetString()),
            ScanExportValues.ParseTimestamp(item.GetProperty("modifiedUtc").GetString()),
            ScanExportValues.ParseTimestamp(item.GetProperty("lastAccessedUtc").GetString()));

    public static void WriteNullableTimestamp(
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

    public static TEnum ReadEnum<TEnum>(JsonElement element, string propertyName)
        where TEnum : struct, Enum =>
        Enum.Parse<TEnum>(element.GetProperty(propertyName).GetString()!);
}
