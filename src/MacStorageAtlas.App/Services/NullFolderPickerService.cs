using System.Threading.Tasks;

namespace MacStorageAtlas.App.Services;

internal sealed class NullFolderPickerService : IFolderPickerService
{
    public Task<string?> SelectFolderAsync() => Task.FromResult<string?>(null);
}
