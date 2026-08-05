namespace MacStorageAtlas.Core;

public sealed record ScanSnapshotMetadata
{
    public ScanSnapshotMetadata(
        string snapshotId,
        DateTimeOffset capturedAt,
        string rootPath,
        DateTimeOffset scanCompletedAt,
        ScanOptions options,
        StorageMeasurementMode measurementMode,
        CloneAccountingCoverage cloneAccountingCoverage,
        long itemCount,
        long totalCountedSizeBytes,
        long errorCount,
        ScanCompleteness completeness)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfNegative(itemCount);
        ArgumentOutOfRangeException.ThrowIfNegative(totalCountedSizeBytes);
        ArgumentOutOfRangeException.ThrowIfNegative(errorCount);

        SnapshotId = snapshotId;
        CapturedAt = capturedAt;
        RootPath = rootPath;
        ScanCompletedAt = scanCompletedAt;
        Options = options;
        MeasurementMode = measurementMode;
        CloneAccountingCoverage = cloneAccountingCoverage;
        ItemCount = itemCount;
        TotalCountedSizeBytes = totalCountedSizeBytes;
        ErrorCount = errorCount;
        Completeness = completeness;
    }

    public string SnapshotId { get; }

    public DateTimeOffset CapturedAt { get; }

    public string RootPath { get; }

    public DateTimeOffset ScanCompletedAt { get; }

    public ScanOptions Options { get; }

    public StorageMeasurementMode MeasurementMode { get; }

    public CloneAccountingCoverage CloneAccountingCoverage { get; }

    public long ItemCount { get; }

    public long TotalCountedSizeBytes { get; }

    public long ErrorCount { get; }

    public ScanCompleteness Completeness { get; }

    public int SchemaVersion { get; init; } = ScanSnapshotSchema.CurrentVersion;
}
