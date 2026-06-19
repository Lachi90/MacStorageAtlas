using System.Threading.Tasks;

namespace MacStorageAtlas.App.Services;

public interface IFolderPickerService
{
    Task<string?> SelectFolderAsync();
}
