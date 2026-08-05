namespace MacStorageAtlas.Core;

public sealed record ScanHistoryLimits
{
    public const int DefaultMaxSnapshotsPerRoot = 10;
    public const long DefaultMaxTotalSizeBytes = 500L * 1024 * 1024;

    public ScanHistoryLimits(int maxSnapshotsPerRoot, long maxTotalSizeBytes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxSnapshotsPerRoot, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxTotalSizeBytes, 1);

        MaxSnapshotsPerRoot = maxSnapshotsPerRoot;
        MaxTotalSizeBytes = maxTotalSizeBytes;
    }

    public static ScanHistoryLimits Default { get; } =
        new(DefaultMaxSnapshotsPerRoot, DefaultMaxTotalSizeBytes);

    public int MaxSnapshotsPerRoot { get; }

    public long MaxTotalSizeBytes { get; }
}
