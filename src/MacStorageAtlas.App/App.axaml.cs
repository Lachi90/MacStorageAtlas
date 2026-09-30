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
using MacStorageAtlas.Platform.Mac;
using MacStorageAtlas.Core.History;
using MacStorageAtlas.Core.Scanning;
#if DIRECT_UPDATES
using Avalonia.Controls;
#endif

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
                new DiskScanner(new MacFileMetadataReader()),
                new AvaloniaUiDispatcher(),
                new MacFileRevealService(),
                new MacTrashService(),
                new AvaloniaTrashConfirmationService(mainWindow),
                new JsonSettingsService(),
                new AvaloniaClipboardService(mainWindow),
                new MacQuickLookService(),
                new AvaloniaSaveFilePickerService(mainWindow.StorageProvider),
                new MacFullDiskAccessService(),
                new AvaloniaCleanupBasketReviewService(mainWindow),
                scanHistoryStore: new FileSystemScanHistoryStore(
                    ScanHistoryStoreLocation.Default()),
                scanHistoryClearConfirmationService:
                    new AvaloniaScanHistoryClearConfirmationService(mainWindow));

            desktop.MainWindow = mainWindow;

#if DIRECT_UPDATES
            var viewModel = (MainWindowViewModel)mainWindow.DataContext;
            Dispatcher.UIThread.Post(() => InitializeUpdates(viewModel, desktop));
#endif

            ApplyDockIcon();
        }

        base.OnFrameworkInitializationCompleted();
    }

#if DIRECT_UPDATES
    private void InitializeUpdates(
        MainWindowViewModel mainViewModel,
        IClassicDesktopStyleApplicationLifetime desktop)
    {
        DirectAppUpdateService? service = null;
        async System.Threading.Tasks.Task PersistAndResumeAsync()
        {
            var ready = await mainViewModel.PrepareForUpdateRestartAsync();
            Dispatcher.UIThread.Post(() =>
            {
                if (ready)
                {
                    service?.CheckForUpdates();
                }
                else if (mainViewModel.Updates is { } updates)
                {
                    updates.StatusMessage = "Update restart postponed. Finish current work and ensure settings can be saved, then choose Finish Installing Update.";
                }
            });
        }
        int PrepareRestart()
        {
            if (mainViewModel.IsUpdateRestartPending)
            {
                return mainViewModel.CanCompleteUpdateRestart ? 1 : 2;
            }
            if (mainViewModel.HasWorkPreventingUpdateRestart)
            {
                return 0;
            }
            _ = PersistAndResumeAsync();
            return 2;
        }
        service = new DirectAppUpdateService(PrepareRestart, () => mainViewModel.HasScanResults);
        var updates = new AppUpdateViewModel(service);
        mainViewModel.Updates = updates;
        desktop.ShutdownRequested += (_, args) =>
        {
            if ((mainViewModel.IsUpdateRestartPending || service.IsRestartRequested)
                && !mainViewModel.CanCompleteUpdateRestart)
            {
                args.Cancel = true;
                mainViewModel.CancelUpdateRestart();
            }
        };
        var menu = NativeMenu.GetMenu(this) ?? new NativeMenu();
        var check = new NativeMenuItem(updates.CheckMenuLabel)
        {
            Command = updates.CheckForUpdatesCommand
        };
        var automatic = new NativeMenuItem("Automatically Check for Updates")
        {
            ToggleType = MenuItemToggleType.CheckBox
        };
        automatic.Click += (_, _) =>
            updates.AutomaticChecksEnabled = !updates.AutomaticChecksEnabled;
        void RefreshMenu()
        {
            check.Header = updates.CheckMenuLabel;
            automatic.IsEnabled = updates.IsAvailable;
            automatic.IsChecked = updates.AutomaticChecksEnabled;
        }
        updates.PropertyChanged += (_, _) => RefreshMenu();
        service.StateChanged += () =>
        {
            if (service.CanCheck && !service.IsRestartDeferred && !service.IsRestartRequested)
            {
                mainViewModel.CancelUpdateRestart();
            }
        };
        RefreshMenu();
        menu.Items.Insert(0, check);
        menu.Items.Insert(1, automatic);
        menu.Items.Insert(2, new NativeMenuItemSeparator());
        NativeMenu.SetMenu(this, menu);
    }
#endif

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

            Dispatcher.UIThread.Post(() => MacDockIcon.TrySet(bytes));
        }
        catch
        {
        }
    }
}
