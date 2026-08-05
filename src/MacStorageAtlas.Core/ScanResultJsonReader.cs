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
        var measurementMode =
            ScanDocumentJson.ReadEnum<StorageMeasurementMode>(scan, "measurementMode");

        var options = ScanDocumentJson.ReadOptions(scan, measurementMode);

        var metadata = new ScanExportMetadata(
            scan.GetProperty("rootPath").GetString()!,
            ScanExportValues.ParseTimestamp(scan.GetProperty("completedAt").GetString())!.Value,
            options,
            measurementMode,
            ScanDocumentJson.ReadEnum<CloneAccountingCoverage>(
                scan,
                "cloneAccountingCoverage"),
            ScanDocumentJson.ReadEnum<ScanExportScope>(scan, "scope"),
            ReadFilter(scan.GetProperty("filter")),
            scan.GetProperty("itemCount").GetInt64(),
            scan.GetProperty("totalCountedSizeBytes").GetInt64())
        {
            SchemaVersion = root.GetProperty("schemaVersion").GetInt32()
        };

        var errors = ScanDocumentJson.ReadErrors(root);

        var items = root.GetProperty("items")
            .EnumerateArray()
            .Select(ScanDocumentJson.ReadRow)
            .ToArray();

        return new ScanExportDocument(metadata, items, errors);
    }

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
}
