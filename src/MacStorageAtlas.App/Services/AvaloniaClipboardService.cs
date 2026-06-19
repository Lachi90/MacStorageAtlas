using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace MacStorageAtlas.App.Services;

public sealed class AvaloniaClipboardService(TopLevel topLevel) : IClipboardService
{
    public Task SetTextAsync(string text)
    {
        var clipboard = topLevel.Clipboard;
        return clipboard is null ? Task.CompletedTask : clipboard.SetTextAsync(text);
    }
}
