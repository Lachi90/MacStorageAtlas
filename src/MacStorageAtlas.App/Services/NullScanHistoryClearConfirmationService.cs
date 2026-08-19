using System.Threading.Tasks;

namespace MacStorageAtlas.App.Services;

public sealed class NullScanHistoryClearConfirmationService
    : IScanHistoryClearConfirmationService
{
    public Task<bool> ConfirmClearHistoryAsync(int snapshotCount, long totalSizeBytes) =>
        Task.FromResult(false);
}
