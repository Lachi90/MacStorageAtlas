using System.Threading.Tasks;

namespace MacStorageAtlas.App.Services;

public interface IClipboardService
{
    Task SetTextAsync(string text);
}
