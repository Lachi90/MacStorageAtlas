using System.Text.Json;

namespace MacStorageAtlas.Core;

public static class ScanResultJsonReader
{
    public static ScanExportDocument Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        var scan = root.GetProperty("scan");
        var measurementMode = ReadEnum<StorageMeasurementMode>(scan, "measurementMode");

        var options = new ScanOptions
        {
            IncludeHiddenFiles = scan.GetProperty("options")
                .GetProperty("includeHiddenFiles").GetBoolean(),
            FollowSymbolicLinks = scan.GetProperty("options")
                .GetProperty("followSymbolicLinks").GetBoolean(),
            TreatPackagesAsDirectories = scan.GetProperty("options")
                .GetProperty("treatPackagesAsDirectories").GetBoolean(),
            MeasurementMode = measurementMode
        };

        var metadata = new ScanExportMetadata(
            scan.GetProperty("rootPath").GetString()!,
            ScanExportValues.ParseTimestamp(scan.GetProperty("completedAt").GetString())!.Value,
            options,
            measurementMode,
            ReadEnum<CloneAccountingCoverage>(scan, "cloneAccountingCoverage"),
            ReadEnum<ScanExportScope>(scan, "scope"),
            ReadFilter(scan.GetProperty("filter")),
            scan.GetProperty("itemCount").GetInt64(),
            scan.GetProperty("totalCountedSizeBytes").GetInt64())
        {
            SchemaVersion = root.GetProperty("schemaVersion").GetInt32()
        };

        var errors = root.GetProperty("errors")
            .EnumerateArray()
            .Select(error => new ScanError(
                error.GetProperty("path").GetString()!,
                error.GetProperty("message").GetString()!,
                error.GetProperty("exceptionType").GetString()!))
            .ToArray();

        var items = root.GetProperty("items")
            .EnumerateArray()
            .Select(ReadRow)
            .ToArray();

        return new ScanExportDocument(metadata, items, errors);
    }

    private static ScanExportRow ReadRow(JsonElement item) =>
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

    private static DiskItemFilter? ReadFilter(JsonElement filter)
    {
        if (filter.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return new DiskItemFilter
        {
            TextTerm = filter.GetProperty("textTerm").GetString(),
            MinimumSizeBytes = ReadNullableNumber(filter, "minimumSizeBytes"),
            MaximumSizeBytes = ReadNullableNumber(filter, "maximumSizeBytes"),
            CreatedAfter = ReadCriterion(filter, "createdAfter"),
            CreatedBefore = ReadCriterion(filter, "createdBefore"),
            ModifiedAfter = ReadCriterion(filter, "modifiedAfter"),
            ModifiedBefore = ReadCriterion(filter, "modifiedBefore"),
            LastAccessedAfter = ReadCriterion(filter, "lastAccessedAfter"),
            LastAccessedBefore = ReadCriterion(filter, "lastAccessedBefore"),
            Extensions = filter.GetProperty("extensions")
                .EnumerateArray()
                .Select(extension => extension.GetString()!)
                .ToArray(),
            Categories = filter.GetProperty("categories")
                .EnumerateArray()
                .Select(category => Enum.Parse<FileCategory>(category.GetString()!))
                .ToArray(),
            SharedStorageOnly = filter.GetProperty("sharedStorageOnly").GetBoolean()
        };
    }

    private static DateCriterion? ReadCriterion(JsonElement filter, string propertyName)
    {
        var criterion = filter.GetProperty(propertyName);
        if (criterion.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return criterion.GetProperty("kind").GetString() switch
        {
            "Absolute" => new AbsoluteDateCriterion(
                ScanExportValues.ParseTimestamp(
                    criterion.GetProperty("instant").GetString())!.Value),
            "Relative" => new RelativeDateCriterion(
                criterion.GetProperty("count").GetInt32(),
                Enum.Parse<RelativeDateUnit>(criterion.GetProperty("unit").GetString()!)),
            var kind => throw new NotSupportedException(
                $"Unsupported date criterion kind '{kind}'.")
        };
    }

    private static long? ReadNullableNumber(JsonElement element, string propertyName)
    {
        var value = element.GetProperty(propertyName);
        return value.ValueKind == JsonValueKind.Null ? null : value.GetInt64();
    }

    private static TEnum ReadEnum<TEnum>(JsonElement element, string propertyName)
        where TEnum : struct, Enum =>
        Enum.Parse<TEnum>(element.GetProperty(propertyName).GetString()!);
}
