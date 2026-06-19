using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace MacStorageAtlas.App.Services;

public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public async Task InvokeAsync(Action action)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(action);
    }
}
