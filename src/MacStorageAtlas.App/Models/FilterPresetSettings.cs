using System;
using System.Collections.Generic;
using System.Linq;
using MacStorageAtlas.Core.Filtering;
using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.App.Models;

public sealed class FilterPresetSettings
{
    public const int CurrentSchemaVersion = 2;

    private const int AbsoluteOnlySchemaVersion = 1;

    public int SchemaVersion { get; set; } = AbsoluteOnlySchemaVersion;

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

    public RelativeDateCriterionSettings? CreatedAfterRelative { get; set; }

    public RelativeDateCriterionSettings? CreatedBeforeRelative { get; set; }

    public RelativeDateCriterionSettings? ModifiedAfterRelative { get; set; }

    public RelativeDateCriterionSettings? ModifiedBeforeRelative { get; set; }

    public RelativeDateCriterionSettings? LastAccessedAfterRelative { get; set; }

    public RelativeDateCriterionSettings? LastAccessedBeforeRelative { get; set; }

    public List<string> Extensions { get; set; } = [];

    public List<FileCategory> Categories { get; set; } = [];

    public bool SharedStorageOnly { get; set; }

    public static FilterPresetSettings FromPreset(FilterPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        var filter = preset.Filter;
        var settings = new FilterPresetSettings
        {
            Name = preset.Name,
            TextTerm = filter.TextTerm,
            MinimumSizeBytes = filter.MinimumSizeBytes,
            MaximumSizeBytes = filter.MaximumSizeBytes,
            CreatedAfter = AbsoluteOf(filter.CreatedAfter),
            CreatedBefore = AbsoluteOf(filter.CreatedBefore),
            ModifiedAfter = AbsoluteOf(filter.ModifiedAfter),
            ModifiedBefore = AbsoluteOf(filter.ModifiedBefore),
            LastAccessedAfter = AbsoluteOf(filter.LastAccessedAfter),
            LastAccessedBefore = AbsoluteOf(filter.LastAccessedBefore),
            CreatedAfterRelative = RelativeOf(filter.CreatedAfter),
            CreatedBeforeRelative = RelativeOf(filter.CreatedBefore),
            ModifiedAfterRelative = RelativeOf(filter.ModifiedAfter),
            ModifiedBeforeRelative = RelativeOf(filter.ModifiedBefore),
            LastAccessedAfterRelative = RelativeOf(filter.LastAccessedAfter),
            LastAccessedBeforeRelative = RelativeOf(filter.LastAccessedBefore),
            Extensions = [.. filter.Extensions],
            Categories = [.. filter.Categories],
            SharedStorageOnly = filter.SharedStorageOnly
        };

        settings.SchemaVersion = settings.HasRelativeCriterion
            ? CurrentSchemaVersion
            : AbsoluteOnlySchemaVersion;

        return settings;
    }

    public FilterPreset? TryCreatePreset()
    {
        if (string.IsNullOrWhiteSpace(Name) || SchemaVersion > CurrentSchemaVersion)
        {
            return null;
        }

        if (!TryReadCriterion(CreatedAfter, CreatedAfterRelative, out var createdAfter)
            || !TryReadCriterion(
                CreatedBefore,
                CreatedBeforeRelative,
                out var createdBefore)
            || !TryReadCriterion(
                ModifiedAfter,
                ModifiedAfterRelative,
                out var modifiedAfter)
            || !TryReadCriterion(
                ModifiedBefore,
                ModifiedBeforeRelative,
                out var modifiedBefore)
            || !TryReadCriterion(
                LastAccessedAfter,
                LastAccessedAfterRelative,
                out var lastAccessedAfter)
            || !TryReadCriterion(
                LastAccessedBefore,
                LastAccessedBeforeRelative,
                out var lastAccessedBefore))
        {
            return null;
        }

        var categories = Categories.Where(Enum.IsDefined).Distinct().ToArray();
        var filter = new DiskItemFilter
        {
            TextTerm = TextTerm,
            MinimumSizeBytes = MinimumSizeBytes,
            MaximumSizeBytes = MaximumSizeBytes,
            CreatedAfter = createdAfter,
            CreatedBefore = createdBefore,
            ModifiedAfter = modifiedAfter,
            ModifiedBefore = modifiedBefore,
            LastAccessedAfter = lastAccessedAfter,
            LastAccessedBefore = lastAccessedBefore,
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

    private bool HasRelativeCriterion =>
        CreatedAfterRelative is not null
        || CreatedBeforeRelative is not null
        || ModifiedAfterRelative is not null
        || ModifiedBeforeRelative is not null
        || LastAccessedAfterRelative is not null
        || LastAccessedBeforeRelative is not null;

    private static DateTimeOffset? AbsoluteOf(DateCriterion? criterion) =>
        criterion is AbsoluteDateCriterion absolute ? absolute.Instant : null;

    private static RelativeDateCriterionSettings? RelativeOf(DateCriterion? criterion) =>
        criterion is RelativeDateCriterion relative
            ? RelativeDateCriterionSettings.FromCriterion(relative)
            : null;

    private static bool TryReadCriterion(
        DateTimeOffset? absolute,
        RelativeDateCriterionSettings? relative,
        out DateCriterion? criterion)
    {
        criterion = null;

        if (absolute is not null && relative is not null)
        {
            return false;
        }

        if (absolute is { } instant)
        {
            criterion = new AbsoluteDateCriterion(instant);
            return true;
        }

        if (relative is null)
        {
            return true;
        }

        if (relative.TryCreateCriterion() is not { } resolved)
        {
            return false;
        }

        criterion = resolved;
        return true;
    }
}
