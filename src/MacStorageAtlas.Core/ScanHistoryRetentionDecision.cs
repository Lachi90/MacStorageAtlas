namespace MacStorageAtlas.Core;

public sealed record ScanHistoryRetentionDecision
{
    private ScanHistoryRetentionDecision(
        bool isAccepted,
        IReadOnlyList<ScanSnapshotDescriptor> snapshotsToPrune,
        string? refusalMessage)
    {
        IsAccepted = isAccepted;
        SnapshotsToPrune = snapshotsToPrune;
        RefusalMessage = refusalMessage;
    }

    public bool IsAccepted { get; }

    public IReadOnlyList<ScanSnapshotDescriptor> SnapshotsToPrune { get; }

    public string? RefusalMessage { get; }

    public static ScanHistoryRetentionDecision Accept(
        IReadOnlyList<ScanSnapshotDescriptor> snapshotsToPrune)
    {
        ArgumentNullException.ThrowIfNull(snapshotsToPrune);

        return new ScanHistoryRetentionDecision(
            isAccepted: true,
            snapshotsToPrune,
            refusalMessage: null);
    }

    public static ScanHistoryRetentionDecision Refuse(string refusalMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refusalMessage);

        return new ScanHistoryRetentionDecision(
            isAccepted: false,
            [],
            refusalMessage);
    }
}
