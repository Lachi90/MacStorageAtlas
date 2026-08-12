using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.Core.History;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Platform;

namespace MacStorageAtlas.App.ViewModels;

public partial class ScanHistoryViewModel : ViewModelBase
{
    private readonly IScanHistoryStore _store;
    private readonly IScanHistoryClearConfirmationService _clearConfirmationService;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly IFileRevealService _fileRevealService;

    public ScanHistoryViewModel(
        IScanHistoryStore store,
        IScanHistoryClearConfirmationService clearConfirmationService,
        IUiDispatcher uiDispatcher,
        IFileRevealService fileRevealService)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(clearConfirmationService);
        ArgumentNullException.ThrowIfNull(uiDispatcher);
        ArgumentNullException.ThrowIfNull(fileRevealService);

        _store = store;
        _clearConfirmationService = clearConfirmationService;
        _uiDispatcher = uiDispatcher;
        _fileRevealService = fileRevealService;
    }

    public string StoreLocation => _store.Location;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(SnapshotCount))]
    [NotifyCanExecuteChangedFor(nameof(RevealStoreCommand))]
    private IReadOnlyList<ScanHistoryRootViewModel> _roots = [];

    [ObservableProperty]
    private string _totalStoreSize = FileSizeFormatter.Format(0);

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _unreadableWarning;

    [ObservableProperty]
    private bool _isBusy;

    public bool IsEmpty => Roots.Count == 0;

    public int SnapshotCount => Roots.Sum(root => root.Snapshots.Count);

    public string EmptyStateMessage =>
        "No scans have been recorded yet.";

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsBusy = true;

        try
        {
            var entries = await _store.ListAsync().ConfigureAwait(false);
            var totalSize = await _store.GetTotalSizeBytesAsync().ConfigureAwait(false);

            await _uiDispatcher.InvokeAsync(() =>
            {
                Roots = Group(entries);
                TotalStoreSize = FileSizeFormatter.Format(totalSize);
                UnreadableWarning = DescribeUnreadable(entries);
            }).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            await _uiDispatcher.InvokeAsync(() =>
            {
                Roots = [];
                UnreadableWarning =
                    $"The scan history store could not be read. {exception.Message}";
            }).ConfigureAwait(false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public bool CanRevealStore =>
        !IsEmpty && !string.IsNullOrWhiteSpace(_store.Location);

    [RelayCommand(CanExecute = nameof(CanRevealStore))]
    private void RevealStore() =>
        StatusMessage = _fileRevealService.Reveal(_store.Location)
            ? null
            : "The scan history store could not be revealed in Finder.";

    [RelayCommand]
    private async Task DeleteSnapshotAsync(ScanHistoryEntryViewModel? snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        try
        {
            var deleted = await _store.DeleteAsync(snapshot.SnapshotId)
                .ConfigureAwait(false);

            await _uiDispatcher.InvokeAsync(() => StatusMessage = deleted
                ? "Removed one recorded scan."
                : "That recorded scan was already gone.").ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            await _uiDispatcher
                .InvokeAsync(() => StatusMessage =
                    $"That recorded scan could not be removed. {exception.Message}")
                .ConfigureAwait(false);
        }

        await RefreshAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task ClearAsync()
    {
        var entries = await _store.ListAsync().ConfigureAwait(false);

        if (entries.Count == 0)
        {
            await _uiDispatcher
                .InvokeAsync(() => StatusMessage = "No scans have been recorded yet.")
                .ConfigureAwait(false);
            return;
        }

        var totalSize = await _store.GetTotalSizeBytesAsync().ConfigureAwait(false);

        var confirmed = await _clearConfirmationService
            .ConfirmClearHistoryAsync(entries.Count, totalSize)
            .ConfigureAwait(false);

        if (!confirmed)
        {
            return;
        }

        var removed = entries.Count.ToString("N0", CultureInfo.CurrentCulture);

        try
        {
            await _store.ClearAsync().ConfigureAwait(false);

            await _uiDispatcher.InvokeAsync(() => StatusMessage = entries.Count == 1
                ? "Removed 1 recorded scan."
                : $"Removed {removed} recorded scans.").ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            await _uiDispatcher
                .InvokeAsync(() => StatusMessage =
                    $"The scan history could not be cleared. {exception.Message}")
                .ConfigureAwait(false);
        }

        await RefreshAsync().ConfigureAwait(false);
    }

    private static IReadOnlyList<ScanHistoryRootViewModel> Group(
        IReadOnlyList<ScanHistoryEntry> entries) =>
        entries
            .Select(entry => new ScanHistoryEntryViewModel(entry))
            .GroupBy(snapshot => snapshot.RootPath, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new ScanHistoryRootViewModel(
                group.Key,
                group
                    .OrderByDescending(snapshot => snapshot.ScanCompletedAt)
                    .ThenBy(snapshot => snapshot.SnapshotId, StringComparer.Ordinal)
                    .ToArray()))
            .ToArray();

    private static string? DescribeUnreadable(IReadOnlyList<ScanHistoryEntry> entries)
    {
        var unreadable = entries.Count(entry => !entry.IsReadable);

        return unreadable switch
        {
            0 => null,
            1 => "One recorded scan could not be read. You can remove it below.",
            _ => $"{unreadable} recorded scans could not be read. "
                + "You can remove them below."
        };
    }
}
