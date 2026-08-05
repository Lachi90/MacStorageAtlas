namespace MacStorageAtlas.Core;

public interface IScanHistoryStore
{
    string Location { get; }

    Task<IReadOnlyList<ScanHistoryEntry>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<long> GetTotalSizeBytesAsync(CancellationToken cancellationToken = default);

    Task<ScanHistoryCaptureResult> CaptureAsync(
        ScanSnapshotRequest request,
        ScanHistoryLimits limits,
        CancellationToken cancellationToken = default);

    Task<ScanSnapshotReadResult<ScanSnapshotDocument>> ReadAsync(
        string snapshotId,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(
        string snapshotId,
        CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScanSnapshotDescriptor>> ApplyLimitsAsync(
        ScanHistoryLimits limits,
        CancellationToken cancellationToken = default);
}
