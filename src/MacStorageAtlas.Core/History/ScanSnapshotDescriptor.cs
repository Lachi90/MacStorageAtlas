using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.History;

public sealed record ScanSnapshotDescriptor
{
    public ScanSnapshotDescriptor(
        ScanSnapshotMetadata metadata,
        long storedSizeBytes)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentOutOfRangeException.ThrowIfNegative(storedSizeBytes);

        Metadata = metadata;
        StoredSizeBytes = storedSizeBytes;
    }

    public ScanSnapshotMetadata Metadata { get; }

    public long StoredSizeBytes { get; }

    public string SnapshotId => Metadata.SnapshotId;

    public string RootPath => Metadata.RootPath;

    public DateTimeOffset ScanCompletedAt => Metadata.ScanCompletedAt;

    public DateTimeOffset CapturedAt => Metadata.CapturedAt;

    public long ItemCount => Metadata.ItemCount;

    public StorageMeasurementMode MeasurementMode => Metadata.MeasurementMode;

    public ScanCompleteness Completeness => Metadata.Completeness;

    public bool IsComplete => Metadata.Completeness == ScanCompleteness.Complete;
}
