using System;
using MacStorageAtlas.Platform.Mac;

namespace MacStorageAtlas.App.Services;

public sealed class DirectAppUpdateService : IAppUpdateService
{
    private readonly MacAppUpdater _updater;

    public DirectAppUpdateService(Func<int> prepareRestart, Func<bool> hasResults)
    {
        _updater = new MacAppUpdater(prepareRestart, hasResults);
    }

    public event Action? StateChanged
    {
        add => _updater.StateChanged += value;
        remove => _updater.StateChanged -= value;
    }

    public bool IsAvailable => _updater.IsAvailable;

    public bool CanCheck => _updater.CanCheck;

    public bool IsRestartDeferred => _updater.IsRestartDeferred;

    public bool IsRestartRequested => _updater.IsRestartRequested;

    public string? ErrorMessage => _updater.ErrorMessage;

    public bool AutomaticChecksEnabled
    {
        get => _updater.AutomaticChecksEnabled;
        set => _updater.AutomaticChecksEnabled = value;
    }

    public void CheckForUpdates() => _updater.CheckForUpdates();
}
