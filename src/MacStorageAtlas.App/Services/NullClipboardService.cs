using System.Threading.Tasks;

namespace MacStorageAtlas.App.Services;

public sealed class NullClipboardService : IClipboardService
{
    public Task SetTextAsync(string text) => Task.CompletedTask;
}
