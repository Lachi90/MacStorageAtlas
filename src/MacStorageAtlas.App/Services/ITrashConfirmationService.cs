using System.Threading.Tasks;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.Services;

public interface ITrashConfirmationService
{
    Task<bool> ConfirmMoveToTrashAsync(DiskItem item);
}
