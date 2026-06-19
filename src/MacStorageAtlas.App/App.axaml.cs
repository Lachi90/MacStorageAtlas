using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Threading;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.App.Views;
using MacStorageAtlas.Core;
using MacStorageAtlas.Platform.Mac;

namespace MacStorageAtlas.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            mainWindow.DataContext = new MainWindowViewModel(
                new AvaloniaFolderPickerService(mainWindow.StorageProvider),
                new DiskScanner(),
                new AvaloniaUiDispatcher(),
                new MacFileRevealService(),
                new MacTrashService(),
                new AvaloniaTrashConfirmationService(mainWindow),
                new JsonSettingsService(),
                new AvaloniaClipboardService(mainWindow));

            desktop.MainWindow = mainWindow;

            ApplyDockIcon();
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Applies the branding artwork as the macOS Dock icon at runtime. This is
    /// needed when running as a bare executable (e.g. <c>dotnet run</c>); a
    /// packaged <c>.app</c> bundle would use the icon from its Info.plist.
    /// </summary>
    private static void ApplyDockIcon()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        try
        {
            using var stream = AssetLoader.Open(
                new Uri("avares://MacStorageAtlas.App/Assets/icon.png"));
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var bytes = memory.ToArray();

            // Defer until the run loop is active so NSApplication is ready.
            Dispatcher.UIThread.Post(() => MacDockIcon.TrySet(bytes));
        }
        catch
        {
            // Cosmetic only — ignore if the asset can't be loaded.
        }
    }
}
