using System.Runtime.InteropServices;

namespace MacStorageAtlas.Platform.Mac;

public sealed class MacAppUpdater
{
    private readonly RestartGate _restartGate;
    private readonly StateChangedCallback _stateChanged;
    private readonly RestartGate _hasResults;

    public MacAppUpdater(Func<int> prepareRestart, Func<bool> hasResults)
    {
        ArgumentNullException.ThrowIfNull(prepareRestart);
        ArgumentNullException.ThrowIfNull(hasResults);
        _restartGate = () => InvokeGate(() => Math.Clamp(prepareRestart(), 0, 2));
        _hasResults = () => InvokeGate(() => hasResults() ? 1 : 0);
        _stateChanged = NotifyStateChanged;

        if (!OperatingSystem.IsMacOS())
        {
            ErrorMessage = "Software updates are available only in packaged macOS releases.";
            return;
        }

        try
        {
            IsAvailable = Start(_restartGate, _stateChanged, _hasResults) != 0;
            if (!IsAvailable)
            {
                ErrorMessage = Marshal.PtrToStringUTF8(GetError())
                    ?? "Software updates could not be initialized.";
            }
        }
        catch (Exception exception) when (
            exception is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            ErrorMessage = "The updater is unavailable. Reinstall the latest signed release.";
        }
    }

    public event Action? StateChanged;

    public bool IsAvailable { get; }

    public string? ErrorMessage { get; }

    public bool CanCheck => IsAvailable && (GetState() & 10) != 0;

    public bool IsRestartDeferred => IsAvailable && (GetState() & 8) != 0;

    public bool IsRestartRequested => IsAvailable && (GetState() & 16) != 0;

    public bool AutomaticChecksEnabled
    {
        get => IsAvailable && (GetState() & 4) != 0;
        set
        {
            if (IsAvailable)
            {
                SetAutomaticChecks(value ? 1 : 0);
            }
        }
    }

    public void CheckForUpdates()
    {
        if (CanCheck)
        {
            Check();
        }
    }

    private static int InvokeGate(Func<int> callback)
    {
        try
        {
            return callback();
        }
        catch
        {
            return 0;
        }
    }

    private void NotifyStateChanged()
    {
        try
        {
            StateChanged?.Invoke();
        }
        catch
        {
        }
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int RestartGate();

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void StateChangedCallback();

    [DllImport("libMacStorageAtlasUpdater.dylib", EntryPoint = "msa_updater_start")]
    private static extern int Start(RestartGate gate, StateChangedCallback stateChanged, RestartGate hasResults);

    [DllImport("libMacStorageAtlasUpdater.dylib", EntryPoint = "msa_updater_error")]
    private static extern IntPtr GetError();

    [DllImport("libMacStorageAtlasUpdater.dylib", EntryPoint = "msa_updater_state")]
    private static extern int GetState();

    [DllImport("libMacStorageAtlasUpdater.dylib", EntryPoint = "msa_updater_check")]
    private static extern void Check();

    [DllImport("libMacStorageAtlasUpdater.dylib", EntryPoint = "msa_updater_set_automatic_checks")]
    private static extern void SetAutomaticChecks(int enabled);
}
