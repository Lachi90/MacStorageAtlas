using System.Threading.Tasks;

namespace MacStorageAtlas.App.Services;

public interface IScanHistoryClearConfirmationService
{
    Task<bool> ConfirmClearHistoryAsync(int snapshotCount, long totalSizeBytes);
}
