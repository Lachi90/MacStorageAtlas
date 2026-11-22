using System.Threading.Tasks;
using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.App.Services;

public interface IFolderRescanConfirmationService
{
    Task<bool> ConfirmFullRescanAsync(DiskItem folder, string scanRootPath, string explanation);
}
