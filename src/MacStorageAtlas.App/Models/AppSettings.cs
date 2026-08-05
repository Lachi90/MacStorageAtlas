using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.Models;

public sealed class AppSettings
{
    public const int MaxRecentLocations = 10;
    public const double MinimumWindowWidth = 1060;
    public const double MinimumWindowHeight = 680;

    public bool IncludeHiddenFiles { get; set; }

    public bool FollowSymbolicLinks { get; set; }

    public bool TreatPackagesAsDirectories { get; set; } = true;

    public StorageMeasurementMode? MeasurementMode { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? MeasureAllocatedSize { get; set; }

    public List<string> RecentLocations { get; set; } = [];

    public List<FilterPresetSettings> FilterPresets { get; set; } = [];

    public double? WindowWidth { get; set; }

    public double? WindowHeight { get; set; }

    public bool ScanHistoryEnabled { get; set; }

    public int? MaxScanHistorySnapshotsPerRoot { get; set; }

    public long? MaxScanHistoryStoreSizeBytes { get; set; }

    [JsonIgnore]
    public ScanHistoryLimits EffectiveScanHistoryLimits =>
        new(
            MaxScanHistorySnapshotsPerRoot is { } snapshots && snapshots >= 1
                ? snapshots
                : ScanHistoryLimits.DefaultMaxSnapshotsPerRoot,
            MaxScanHistoryStoreSizeBytes is { } storeSize && storeSize >= 1
                ? storeSize
                : ScanHistoryLimits.DefaultMaxTotalSizeBytes);

    public StorageMeasurementMode EffectiveMeasurementMode =>
        MeasurementMode is { } measurementMode && Enum.IsDefined(measurementMode)
            ? measurementMode
            : MeasureAllocatedSize switch
            {
                true => StorageMeasurementMode.SharedAwareAllocated,
                false => StorageMeasurementMode.Logical,
                null => StorageMeasurementMode.SharedAwareAllocated
            };
}
