using System;
using System.Collections.Generic;
using System.Linq;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.Models;

public sealed class FilterPresetSettings
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    public string Name { get; set; } = string.Empty;

    public string? TextTerm { get; set; }

    public long? MinimumSizeBytes { get; set; }

    public long? MaximumSizeBytes { get; set; }

    public DateTimeOffset? CreatedAfter { get; set; }

    public DateTimeOffset? CreatedBefore { get; set; }

    public DateTimeOffset? ModifiedAfter { get; set; }

    public DateTimeOffset? ModifiedBefore { get; set; }

    public DateTimeOffset? LastAccessedAfter { get; set; }

    public DateTimeOffset? LastAccessedBefore { get; set; }

    public List<string> Extensions { get; set; } = [];

    public List<FileCategory> Categories { get; set; } = [];

    public bool SharedStorageOnly { get; set; }

    public static FilterPresetSettings FromPreset(FilterPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        var filter = preset.Filter;
        return new FilterPresetSettings
        {
            SchemaVersion = CurrentSchemaVersion,
            Name = preset.Name,
            TextTerm = filter.TextTerm,
            MinimumSizeBytes = filter.MinimumSizeBytes,
            MaximumSizeBytes = filter.MaximumSizeBytes,
            CreatedAfter = filter.CreatedAfter,
            CreatedBefore = filter.CreatedBefore,
            ModifiedAfter = filter.ModifiedAfter,
            ModifiedBefore = filter.ModifiedBefore,
            LastAccessedAfter = filter.LastAccessedAfter,
            LastAccessedBefore = filter.LastAccessedBefore,
            Extensions = [.. filter.Extensions],
            Categories = [.. filter.Categories],
            SharedStorageOnly = filter.SharedStorageOnly
        };
    }

    public FilterPreset? TryCreatePreset()
    {
        if (string.IsNullOrWhiteSpace(Name) || SchemaVersion > CurrentSchemaVersion)
        {
            return null;
        }

        var categories = Categories.Where(Enum.IsDefined).Distinct().ToArray();
        var filter = new DiskItemFilter
        {
            TextTerm = TextTerm,
            MinimumSizeBytes = MinimumSizeBytes,
            MaximumSizeBytes = MaximumSizeBytes,
            CreatedAfter = CreatedAfter,
            CreatedBefore = CreatedBefore,
            ModifiedAfter = ModifiedAfter,
            ModifiedBefore = ModifiedBefore,
            LastAccessedAfter = LastAccessedAfter,
            LastAccessedBefore = LastAccessedBefore,
            Extensions = Extensions
                .Select(FileCategoryMap.NormalizeExtension)
                .Where(extension => extension is not null)
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            Categories = categories,
            SharedStorageOnly = SharedStorageOnly
        };

        return filter.Validate().IsValid
            ? new FilterPreset(Name.Trim(), filter)
            : null;
    }
}
