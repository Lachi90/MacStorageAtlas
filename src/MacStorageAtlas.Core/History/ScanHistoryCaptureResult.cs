namespace MacStorageAtlas.Core.History;

public enum ScanHistoryCaptureStatus
{
    Captured = 0,
    Refused = 1,
    Failed = 2
}

public sealed record ScanHistoryCaptureResult
{
    private ScanHistoryCaptureResult(
        ScanHistoryCaptureStatus status,
        ScanSnapshotDescriptor? descriptor,
        IReadOnlyList<ScanSnapshotDescriptor> prunedSnapshots,
        string? message)
    {
        Status = status;
        Descriptor = descriptor;
        PrunedSnapshots = prunedSnapshots;
        Message = message;
    }

    public ScanHistoryCaptureStatus Status { get; }

    public ScanSnapshotDescriptor? Descriptor { get; }

    public IReadOnlyList<ScanSnapshotDescriptor> PrunedSnapshots { get; }

    public string? Message { get; }

    public bool IsCaptured => Status == ScanHistoryCaptureStatus.Captured;

    public static ScanHistoryCaptureResult Captured(
        ScanSnapshotDescriptor descriptor,
        IReadOnlyList<ScanSnapshotDescriptor> prunedSnapshots)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(prunedSnapshots);

        return new ScanHistoryCaptureResult(
            ScanHistoryCaptureStatus.Captured,
            descriptor,
            prunedSnapshots,
            message: null);
    }

    public static ScanHistoryCaptureResult Refused(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new ScanHistoryCaptureResult(
            ScanHistoryCaptureStatus.Refused,
            descriptor: null,
            [],
            message);
    }

    public static ScanHistoryCaptureResult Failed(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        return new ScanHistoryCaptureResult(
            ScanHistoryCaptureStatus.Failed,
            descriptor: null,
            [],
            message);
    }
}
