using System.Threading.Tasks;
using MacStorageAtlas.Core.Export;

namespace MacStorageAtlas.App.Services;

public interface ISaveFilePickerService
{
    Task<string?> SelectSaveFileAsync(ScanExportFormat format, string suggestedFileName);
}
