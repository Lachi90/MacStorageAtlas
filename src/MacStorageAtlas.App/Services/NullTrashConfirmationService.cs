using System.Threading.Tasks;
using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.App.Services;

public sealed class NullTrashConfirmationService : ITrashConfirmationService
{
    public Task<bool> ConfirmMoveToTrashAsync(DiskItem item) => Task.FromResult(false);
}
