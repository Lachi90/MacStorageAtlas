using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MacStorageAtlas.App.ViewModels;

public partial class MainWindowViewModel
{
    private bool _settingsPersistedForUpdateRestart;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsApplicationInteractive))]
    private bool _isUpdateRestartPending;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdateStatusMessage))]
    [NotifyPropertyChangedFor(nameof(UpdateStatusMessage))]
    private AppUpdateViewModel? _updates;

    public bool HasUpdateStatusMessage => Updates?.HasStatusMessage == true;

    public string? UpdateStatusMessage => Updates?.StatusMessage;

    partial void OnUpdatesChanged(AppUpdateViewModel? oldValue, AppUpdateViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnUpdateStatusChanged;
        }
        if (newValue is not null)
        {
            newValue.PropertyChanged += OnUpdateStatusChanged;
        }
    }

    private void OnUpdateStatusChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(AppUpdateViewModel.StatusMessage))
        {
            OnPropertyChanged(nameof(HasUpdateStatusMessage));
            OnPropertyChanged(nameof(UpdateStatusMessage));
        }
    }

    public bool IsApplicationInteractive => !IsUpdateRestartPending;

    public bool HasScanResults => _scanRoot is not null;

    public bool HasWorkPreventingUpdateRestart =>
        IsScanning || IsAnalyzingDuplicates || IsExporting || IsMovingToTrash
        || IsRunningCleanupBasketOperation || ScanHistory.HasPendingOperations
        || Volatile.Read(ref _pendingScanFinalizations) != 0
        || _scanCancellation is not null || _duplicateAnalysisCancellation is not null
        || _exportCancellation is not null || _scanHistoryCancellation is not null
        || _treePreparationCancellation is not null;

    public bool CanCompleteUpdateRestart =>
        IsUpdateRestartPending && _settingsPersistedForUpdateRestart
        && !HasWorkPreventingUpdateRestart;

    public async Task<bool> PrepareForUpdateRestartAsync()
    {
        if (HasWorkPreventingUpdateRestart)
        {
            return false;
        }

        var settings = CaptureSettings();
        IsUpdateRestartPending = true;
        var saved = false;
        try
        {
            saved = await Task.Run(() => _settingsService.TrySave(settings));
        }
        catch (Exception)
        {
        }
        await _uiDispatcher.InvokeAsync(() =>
        {
            if (!saved || !IsUpdateRestartPending || HasWorkPreventingUpdateRestart)
            {
                CancelUpdateRestart();
            }
            else
            {
                _settingsPersistedForUpdateRestart = true;
            }
        });
        return CanCompleteUpdateRestart;
    }

    public void CancelUpdateRestart()
    {
        _settingsPersistedForUpdateRestart = false;
        IsUpdateRestartPending = false;
    }

    partial void OnIsUpdateRestartPendingChanged(bool value)
    {
        ScanHistory.UpdatesPreventOperations = value;
        NotifyScanCommandsCanExecuteChanged();
        NotifyExportCommandsCanExecuteChanged();
        StartDuplicateAnalysisCommand.NotifyCanExecuteChanged();
        MoveToTrashCommand.NotifyCanExecuteChanged();
        MoveCleanupBasketToTrashCommand.NotifyCanExecuteChanged();
        MoveCleanupBasketToLocationCommand.NotifyCanExecuteChanged();
        CopyCleanupBasketToLocationCommand.NotifyCanExecuteChanged();
    }
}
