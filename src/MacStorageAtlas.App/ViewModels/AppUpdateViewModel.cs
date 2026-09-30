using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacStorageAtlas.App.Services;

namespace MacStorageAtlas.App.ViewModels;

public partial class AppUpdateViewModel : ViewModelBase, IDisposable
{
    private readonly IAppUpdateService _service;
    private bool _isRefreshing;

    public AppUpdateViewModel(IAppUpdateService service)
    {
        ArgumentNullException.ThrowIfNull(service);
        _service = service;
        _service.StateChanged += Refresh;
        Refresh();
    }

    [ObservableProperty]
    private bool _automaticChecksEnabled;

    [ObservableProperty]
    private bool _isAvailable;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckForUpdatesCommand))]
    private bool _canCheck;

    [ObservableProperty]
    private string _checkMenuLabel = "Check for Updates…";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    private string? _statusMessage;

    public bool HasStatusMessage => !string.IsNullOrEmpty(StatusMessage);

    partial void OnAutomaticChecksEnabledChanged(bool value)
    {
        if (!_isRefreshing && _service.IsAvailable)
        {
            _service.AutomaticChecksEnabled = value;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCheck))]
    private void CheckForUpdates()
    {
        if (!_service.IsAvailable)
        {
            StatusMessage = _service.ErrorMessage ?? "Software updates are unavailable.";
            return;
        }

        StatusMessage = null;
        _service.CheckForUpdates();
        Refresh();
    }

    private void Refresh()
    {
        _isRefreshing = true;
        try
        {
            IsAvailable = _service.IsAvailable;
            CanCheck = !IsAvailable || _service.CanCheck;
            AutomaticChecksEnabled = _service.AutomaticChecksEnabled;
            CheckMenuLabel = _service.IsRestartDeferred
                ? "Finish Installing Update…"
                : "Check for Updates…";
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    public void Dispose() => _service.StateChanged -= Refresh;
}
