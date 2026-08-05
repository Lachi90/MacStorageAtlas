namespace MacStorageAtlas.Core;

public sealed record ScanHistoryEntry
{
    private ScanHistoryEntry(
        string snapshotId,
        long storedSizeBytes,
        ScanSnapshotDescriptor? descriptor,
        string? unreadableMessage)
    {
        SnapshotId = snapshotId;
        StoredSizeBytes = storedSizeBytes;
        Descriptor = descriptor;
        UnreadableMessage = unreadableMessage;
    }

    public string SnapshotId { get; }

    public long StoredSizeBytes { get; }

    public ScanSnapshotDescriptor? Descriptor { get; }

    public string? UnreadableMessage { get; }

    public bool IsReadable => Descriptor is not null;

    public static ScanHistoryEntry Readable(
        string snapshotId,
        ScanSnapshotDescriptor descriptor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotId);
        ArgumentNullException.ThrowIfNull(descriptor);

        return new ScanHistoryEntry(
            snapshotId,
            descriptor.StoredSizeBytes,
            descriptor,
            unreadableMessage: null);
    }

    public static ScanHistoryEntry Unreadable(
        string snapshotId,
        long storedSizeBytes,
        string unreadableMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(snapshotId);
        ArgumentOutOfRangeException.ThrowIfNegative(storedSizeBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(unreadableMessage);

        return new ScanHistoryEntry(
            snapshotId,
            storedSizeBytes,
            descriptor: null,
            unreadableMessage);
    }
}
