using MacStorageAtlas.App.Models;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core.Cleanup;
using MacStorageAtlas.Core.Export;
using MacStorageAtlas.Core.History;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;
using NSubstitute;

namespace MacStorageAtlas.App.Tests;

public class AppUpdateTests
{
    [Test]
    public void WindowWithoutUpdaterHasNoUpdateStatusAndShowsServiceErrorsWhenAttached()
    {
        var mainViewModel = CreateMainWindow(CreateSettings());
        Assert.That(mainViewModel.HasUpdateStatusMessage, Is.False);
        var service = Substitute.For<IAppUpdateService>();
        service.ErrorMessage.Returns("Unavailable updater.");
        using var updates = new AppUpdateViewModel(service);
        mainViewModel.Updates = updates;

        updates.CheckForUpdatesCommand.Execute(null);

        Assert.That(mainViewModel.UpdateStatusMessage, Is.EqualTo("Unavailable updater."));
        Assert.That(mainViewModel.HasUpdateStatusMessage, Is.True);
    }
    [Test]
    public void UnavailableUpdaterKeepsManualErrorActionAndDisablesConsent()
    {
        var service = Substitute.For<IAppUpdateService>();
        service.ErrorMessage.Returns("The updater is missing.");
        using var viewModel = new AppUpdateViewModel(service);

        viewModel.CheckForUpdatesCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CanCheck, Is.True);
            Assert.That(viewModel.IsAvailable, Is.False);
            Assert.That(viewModel.StatusMessage, Is.EqualTo("The updater is missing."));
        });
        service.DidNotReceive().CheckForUpdates();
    }

    [Test]
    public void CheckingAndDeferredInstallationFollowUpdaterState()
    {
        var service = Substitute.For<IAppUpdateService>();
        service.IsAvailable.Returns(true);
        service.CanCheck.Returns(true);
        using var viewModel = new AppUpdateViewModel(service);

        viewModel.CheckForUpdatesCommand.Execute(null);
        service.CanCheck.Returns(false);
        service.StateChanged += Raise.Event<Action>();

        Assert.That(viewModel.CheckForUpdatesCommand.CanExecute(null), Is.False);
        service.Received(1).CheckForUpdates();

        service.CanCheck.Returns(true);
        service.IsRestartDeferred.Returns(true);
        service.StateChanged += Raise.Event<Action>();

        Assert.That(viewModel.CheckMenuLabel, Is.EqualTo("Finish Installing Update…"));
    }

    [Test]
    public void ConsentUsesUpdaterPreferencesWithoutWritingDuringRefresh()
    {
        var service = Substitute.For<IAppUpdateService>();
        service.IsAvailable.Returns(true);
        service.AutomaticChecksEnabled.Returns(true);
        using var viewModel = new AppUpdateViewModel(service);

        service.DidNotReceive().AutomaticChecksEnabled = Arg.Any<bool>();
        Assert.That(viewModel.AutomaticChecksEnabled, Is.True);

        viewModel.AutomaticChecksEnabled = false;

        service.Received(1).AutomaticChecksEnabled = false;
    }

    [TestCase(nameof(MainWindowViewModel.IsScanning))]
    [TestCase(nameof(MainWindowViewModel.IsAnalyzingDuplicates))]
    [TestCase(nameof(MainWindowViewModel.IsExporting))]
    [TestCase(nameof(MainWindowViewModel.IsMovingToTrash))]
    [TestCase(nameof(MainWindowViewModel.IsRunningCleanupBasketOperation))]
    public async Task ActiveWorkDefersRestartWithoutSavingOrReservingShutdown(string property)
    {
        var settings = CreateSettings();
        var viewModel = CreateMainWindow(settings);
        typeof(MainWindowViewModel).GetProperty(property)!.SetValue(viewModel, true);

        Assert.That(await viewModel.PrepareForUpdateRestartAsync(), Is.False);
        Assert.That(viewModel.IsUpdateRestartPending, Is.False);
        settings.DidNotReceive().TrySave(Arg.Any<AppSettings>());
    }

    [Test]
    public async Task HistoryActivityDefersRestart()
    {
        var viewModel = CreateMainWindow(CreateSettings());
        viewModel.ScanHistory.IsBusy = true;

        Assert.That(await viewModel.PrepareForUpdateRestartAsync(), Is.False);
    }

    [Test]
    public async Task PendingHistoryClearDefersRestartBeforeRefreshShowsBusy()
    {
        var store = Substitute.For<IScanHistoryStore>();
        var listed = new TaskCompletionSource<IReadOnlyList<ScanHistoryEntry>>();
        store.ListAsync(Arg.Any<CancellationToken>()).Returns(listed.Task);
        var viewModel = CreateMainWindow(CreateSettings(), store);

        var clear = viewModel.ScanHistory.ClearCommand.ExecuteAsync(null);
        Assert.That(viewModel.ScanHistory.IsBusy, Is.False);
        Assert.That(await viewModel.PrepareForUpdateRestartAsync(), Is.False);
        listed.SetResult([]);
        await clear;

        Assert.That(await viewModel.PrepareForUpdateRestartAsync(), Is.True);
    }

    [Test]
    public async Task IdleRestartPersistsSettingsAndPreventsNewWorkUntilCancelled()
    {
        var settings = CreateSettings();
        var viewModel = CreateMainWindow(settings);
        viewModel.SelectedFolderPath = "/isolated-fixture";

        Assert.That(await viewModel.PrepareForUpdateRestartAsync(), Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsApplicationInteractive, Is.False);
            Assert.That(viewModel.ScanHistory.UpdatesPreventOperations, Is.True);
            Assert.That(viewModel.ScanFolderCommand.CanExecute(null), Is.False);
        });
        settings.Received(1).TrySave(Arg.Any<AppSettings>());

        viewModel.CancelUpdateRestart();

        Assert.That(viewModel.ScanFolderCommand.CanExecute(null), Is.True);
        Assert.That(viewModel.ScanHistory.UpdatesPreventOperations, Is.False);
    }

    [Test]
    public async Task WorkAppearingAtFinalRevalidationVetoesRestart()
    {
        var viewModel = CreateMainWindow(CreateSettings());
        Assert.That(await viewModel.PrepareForUpdateRestartAsync(), Is.True);
        viewModel.IsExporting = true;

        Assert.That(viewModel.CanCompleteUpdateRestart, Is.False);
    }

    [Test]
    public async Task ExportPickerReturningAfterRestartReservationDoesNotWriteAFile()
    {
        var picker = Substitute.For<ISaveFilePickerService>();
        var selection = new TaskCompletionSource<string?>();
        picker.SelectSaveFileAsync(Arg.Any<ScanExportFormat>(), Arg.Any<string>())
            .Returns(selection.Task);
        var viewModel = CreateScannedWindow(saveFilePicker: picker);
        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        var export = viewModel.ExportJsonCommand.ExecuteAsync(null);
        Assert.That(await viewModel.PrepareForUpdateRestartAsync(), Is.True);
        var destination = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
        selection.SetResult(destination);
        await export;

        Assert.That(viewModel.IsExporting, Is.False);
        Assert.That(viewModel.ExportStatusMessage, Is.Null);
        Assert.That(File.Exists(destination), Is.False);
    }

    [Test]
    public async Task TrashConfirmationReturningAfterRestartReservationDoesNotMoveFiles()
    {
        var confirmation = Substitute.For<ITrashConfirmationService>();
        var decision = new TaskCompletionSource<bool>();
        confirmation.ConfirmMoveToTrashAsync(Arg.Any<DiskItem>()).Returns(decision.Task);
        var trash = Substitute.For<ITrashService>();
        var viewModel = CreateScannedWindow(trash: trash, confirmation: confirmation);
        await viewModel.ScanFolderCommand.ExecuteAsync(null);
        viewModel.SelectedLargeFile = viewModel.LargeFiles[0];

        var move = viewModel.MoveToTrashCommand.ExecuteAsync(null);
        Assert.That(await viewModel.PrepareForUpdateRestartAsync(), Is.True);
        decision.SetResult(true);
        await move;

        await trash.DidNotReceive().MoveToTrashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task PersistenceFailureLeavesApplicationInteractive()
    {
        var settings = CreateSettings();
        settings.TrySave(Arg.Any<AppSettings>()).Returns(false);
        var viewModel = CreateMainWindow(settings);

        Assert.That(await viewModel.PrepareForUpdateRestartAsync(), Is.False);
        Assert.That(viewModel.IsApplicationInteractive, Is.True);

        settings.TrySave(Arg.Any<AppSettings>()).Returns(_ => throw new IOException());

        Assert.That(await viewModel.PrepareForUpdateRestartAsync(), Is.False);
    }

    [Test]
    public void DistributionSelectsNativeUpdaterAtCompileTime()
    {
        var appAssembly = typeof(MainWindowViewModel).Assembly;
        var platformAssembly = typeof(MacStorageAtlas.Platform.Mac.MacTrashService).Assembly;
#if DIRECT_UPDATES
        Assert.That(appAssembly.GetType("MacStorageAtlas.App.Services.DirectAppUpdateService"), Is.Not.Null);
        Assert.That(platformAssembly.GetType("MacStorageAtlas.Platform.Mac.MacAppUpdater"), Is.Not.Null);
        var updater = new MacStorageAtlas.Platform.Mac.MacAppUpdater(() => 1, () => false);
        Assert.Multiple(() =>
        {
            Assert.That(updater.IsAvailable, Is.False);
            Assert.That(updater.CanCheck, Is.False);
            Assert.That(updater.AutomaticChecksEnabled, Is.False);
            Assert.That(updater.ErrorMessage, Is.Not.Empty);
        });
        Assert.DoesNotThrow(() => updater.CheckForUpdates());
        Assert.DoesNotThrow(() => updater.AutomaticChecksEnabled = true);
#else
        Assert.That(appAssembly.GetType("MacStorageAtlas.App.Services.DirectAppUpdateService"), Is.Null);
        Assert.That(platformAssembly.GetType("MacStorageAtlas.Platform.Mac.MacAppUpdater"), Is.Null);
#endif
    }

    [Test]
    public void SettingsSaveFailureIsObservableWithoutChangingOrdinarySaveBehavior()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var service = new JsonSettingsService(directory);

            Assert.That(service.TrySave(new AppSettings()), Is.False);
            Assert.DoesNotThrow(() => service.Save(new AppSettings()));
        }
        finally
        {
            Directory.Delete(directory);
        }
    }

    [Test]
    public void AtomicSaveFailurePreservesExistingSettings()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Ignore("This case requires macOS directory permission enforcement.");
            return;
        }
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var destination = Path.Combine(directory, "settings.json");
        const string previous = "{\"IncludeHiddenFiles\":true}";
        File.WriteAllText(destination, previous);
        var permissions = File.GetUnixFileMode(directory);
        try
        {
            File.SetUnixFileMode(directory, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            var service = new JsonSettingsService(destination);

            Assert.That(service.TrySave(new AppSettings()), Is.False);
            Assert.That(File.ReadAllText(destination), Is.EqualTo(previous));
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(1));
        }
        finally
        {
            File.SetUnixFileMode(directory, permissions);
            Directory.Delete(directory, recursive: true);
        }
    }

    private static ISettingsService CreateSettings()
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings());
        settings.TrySave(Arg.Any<AppSettings>()).Returns(true);
        return settings;
    }

    private static MainWindowViewModel CreateMainWindow(
        ISettingsService settings,
        IScanHistoryStore? historyStore = null) =>
        new(Substitute.For<IFolderPickerService>(),
            Substitute.For<IDiskScanner>(),
            new ImmediateDispatcher(),
            settingsService: settings, scanHistoryStore: historyStore);

    private static MainWindowViewModel CreateScannedWindow(
        ISaveFilePickerService? saveFilePicker = null,
        ITrashService? trash = null,
        ITrashConfirmationService? confirmation = null)
    {
        var scanner = Substitute.For<IDiskScanner>();
        scanner.ScanAsync(Arg.Any<string>(), Arg.Any<ScanOptions>(), Arg.Any<CancellationToken>())
            .Returns(CompletedScan());
        return new MainWindowViewModel(
            Substitute.For<IFolderPickerService>(), scanner, new ImmediateDispatcher(),
            trashService: trash, trashConfirmationService: confirmation,
            settingsService: CreateSettings(), saveFilePickerService: saveFilePicker)
        {
            SelectedFolderPath = "/isolated-fixture"
        };
    }

    private static async IAsyncEnumerable<ScanProgress> CompletedScan()
    {
        var root = new DiskItem("fixture", "/isolated-fixture", isDirectory: true);
        root.AddChild(new DiskItem("example.txt", "/isolated-fixture/example.txt", false));
        await Task.Yield();
        yield return new ScanProgress(root.Path, 1, 1, 0, root, [], true);
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
    }
}
