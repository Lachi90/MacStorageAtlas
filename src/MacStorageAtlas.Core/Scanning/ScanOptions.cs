namespace MacStorageAtlas.Core.Scanning;

public sealed record ScanOptions
{
    public static ScanOptions Default { get; } = new();

    public bool IncludeHiddenFiles { get; init; }

    public bool FollowSymbolicLinks { get; init; }

    public bool TreatPackagesAsDirectories { get; init; } = true;

    public StorageMeasurementMode MeasurementMode { get; init; }
}
