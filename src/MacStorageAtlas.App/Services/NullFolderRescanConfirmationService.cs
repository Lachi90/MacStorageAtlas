using System.Threading.Tasks;
using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.App.Services;

public sealed class NullFolderRescanConfirmationService : IFolderRescanConfirmationService
{
    public Task<bool> ConfirmFullRescanAsync(
        DiskItem folder,
        string scanRootPath,
        string explanation) =>
        Task.FromResult(false);
}
