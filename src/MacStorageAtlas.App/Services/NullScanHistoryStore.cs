using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.Services;

public sealed class NullScanHistoryStore : IScanHistoryStore
{
    public string Location => string.Empty;

    public Task<IReadOnlyList<ScanHistoryEntry>> ListAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ScanHistoryEntry>>([]);

    public Task<long> GetTotalSizeBytesAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult(0L);

    public Task<ScanHistoryCaptureResult> CaptureAsync(
        ScanSnapshotRequest request,
        ScanHistoryLimits limits,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            ScanHistoryCaptureResult.Failed("No scan history store is configured."));

    public Task<ScanSnapshotReadResult<ScanSnapshotDocument>> ReadAsync(
        string snapshotId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            ScanSnapshotReadResult<ScanSnapshotDocument>.Unreadable(
                "No scan history store is configured."));

    public Task<bool> DeleteAsync(
        string snapshotId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(false);

    public Task ClearAsync(CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<ScanSnapshotDescriptor>> ApplyLimitsAsync(
        ScanHistoryLimits limits,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ScanSnapshotDescriptor>>([]);
}
