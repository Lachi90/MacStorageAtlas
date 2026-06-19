using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace MacStorageAtlas.App.Services;

public sealed class AvaloniaFolderPickerService(IStorageProvider storageProvider) : IFolderPickerService
{
    public async Task<string?> SelectFolderAsync()
    {
        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select a folder to analyze",
            AllowMultiple = false,
        });

        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }
}
