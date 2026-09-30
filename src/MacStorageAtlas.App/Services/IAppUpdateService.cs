using System;

namespace MacStorageAtlas.App.Services;

public interface IAppUpdateService
{
    event Action? StateChanged;

    bool IsAvailable { get; }

    bool CanCheck { get; }

    bool IsRestartDeferred { get; }

    bool IsRestartRequested { get; }

    string? ErrorMessage { get; }

    bool AutomaticChecksEnabled { get; set; }

    void CheckForUpdates();
}
