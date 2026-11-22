using System;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace MacStorageAtlas.App.ViewModels;

public partial class MainWindowViewModel
{
    public const string StopLabel = "Stop";

    public const string ScanToolTip = "Scan the selected folder (Command-R)";

    public const string RescanToolTip = "Rescan the selected folder (Command-R)";

    public const string StopToolTip = "Stop the scan and keep partial results (Command-Period)";

    public const string CleanupBasketShortcutSuffix = " (Command-B)";

    internal static readonly TimeSpan PrimaryScanActionTransitionInterval =
        TimeSpan.FromMilliseconds(500);

    private readonly TimeProvider _timeProvider;
    private long? _lastPrimaryScanActivationTimestamp;

    public string PrimaryScanActionLabel => IsScanning ? StopLabel : ScanCommandLabel;

    public string PrimaryScanActionToolTip =>
        IsScanning
            ? StopToolTip
            : IsCompletedResultDisplayedForSelectedFolder() ? RescanToolTip : ScanToolTip;

    public bool IsPrimaryScanActionStop => IsScanning;

    public bool IsPrimaryScanActionRescan =>
        !IsScanning && IsCompletedResultDisplayedForSelectedFolder();

    public bool IsPrimaryScanActionScan =>
        !IsScanning && !IsCompletedResultDisplayedForSelectedFolder();

    public bool CleanupBasketToggleRemovesItem => IsSelectedItemInCleanupBasket();

    public string CleanupBasketToggleToolTip =>
        CleanupBasketCommandLabel + CleanupBasketShortcutSuffix;

    public bool IsExportAvailable => CanExport();

    private ICommand PrimaryScanActionTarget =>
        IsScanning ? StopScanCommand : ScanFolderCommand;

    private void ObservePrimaryScanActionTargets()
    {
        ScanFolderCommand.CanExecuteChanged += OnPrimaryScanActionTargetCanExecuteChanged;
        StopScanCommand.CanExecuteChanged += OnPrimaryScanActionTargetCanExecuteChanged;
    }

    private void OnPrimaryScanActionTargetCanExecuteChanged(object? sender, EventArgs e) =>
        PrimaryScanActionCommand.NotifyCanExecuteChanged();

    private bool CanRunPrimaryScanAction() => PrimaryScanActionTarget.CanExecute(null);

    [RelayCommand(CanExecute = nameof(CanRunPrimaryScanAction))]
    private void PrimaryScanAction()
    {
        if (IsPrimaryScanActionTransitionInProgress())
        {
            return;
        }

        var target = PrimaryScanActionTarget;
        if (!target.CanExecute(null))
        {
            return;
        }

        _lastPrimaryScanActivationTimestamp = _timeProvider.GetTimestamp();
        target.Execute(null);
    }

    private bool IsPrimaryScanActionTransitionInProgress() =>
        _lastPrimaryScanActivationTimestamp is { } lastActivation
        && _timeProvider.GetElapsedTime(lastActivation) < PrimaryScanActionTransitionInterval;

    private void NotifyPrimaryScanActionChanged()
    {
        OnPropertyChanged(nameof(PrimaryScanActionLabel));
        OnPropertyChanged(nameof(PrimaryScanActionToolTip));
        OnPropertyChanged(nameof(IsPrimaryScanActionStop));
        OnPropertyChanged(nameof(IsPrimaryScanActionRescan));
        OnPropertyChanged(nameof(IsPrimaryScanActionScan));
    }

    private void NotifyCleanupBasketToggleChanged()
    {
        OnPropertyChanged(nameof(CleanupBasketToggleRemovesItem));
        OnPropertyChanged(nameof(CleanupBasketToggleToolTip));
    }
}
