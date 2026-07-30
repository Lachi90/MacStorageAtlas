using System.Threading.Tasks;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.Services;

internal sealed class NullSaveFilePickerService : ISaveFilePickerService
{
    public Task<string?> SelectSaveFileAsync(
        ScanExportFormat format,
        string suggestedFileName) => Task.FromResult<string?>(null);
}
