using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.Models;

public sealed class AppSettings
{
    public const int MaxRecentLocations = 10;

    public bool IncludeHiddenFiles { get; set; }

    public bool FollowSymbolicLinks { get; set; }

    public bool TreatPackagesAsDirectories { get; set; } = true;

    public StorageMeasurementMode? MeasurementMode { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? MeasureAllocatedSize { get; set; }

    public List<string> RecentLocations { get; set; } = [];

    public StorageMeasurementMode EffectiveMeasurementMode =>
        MeasurementMode is { } measurementMode && Enum.IsDefined(measurementMode)
            ? measurementMode
            : MeasureAllocatedSize switch
            {
                true => StorageMeasurementMode.HardlinkAwareAllocated,
                false => StorageMeasurementMode.Logical,
                null => StorageMeasurementMode.HardlinkAwareAllocated
            };
}
