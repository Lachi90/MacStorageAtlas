using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacStorageAtlas.App.Models;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.Core;
using MacStorageAtlas.Platform.Mac;
using MacStorageAtlas.Rendering;

namespace MacStorageAtlas.App.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private const double TreemapWidth = 700;
    private const double TreemapHeight = 320;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IDiskScanner _diskScanner;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly IFileRevealService _fileRevealService;
    private readonly ITrashService _trashService;
    private readonly ITrashConfirmationService _trashConfirmationService;
    private readonly ISettingsService _settingsService;
    private readonly IClipboardService _clipboardService;
    private readonly ITreemapLayoutService _treemapLayoutService = new TreemapLayoutService();
    private readonly FileTypeStatisticsService _fileTypeStatisticsService = new();
    private readonly LargeFilesService _largeFilesService = new();
    private DiskItem? _scanRoot;
    private CancellationTokenSource? _scanCancellation;
    private bool _isApplyingSettings;

    public MainWindowViewModel()
        : this(
            new NullFolderPickerService(),
            new DiskScanner(),
            new AvaloniaUiDispatcher(),
            new MacFileRevealService(),
            new MacTrashService(),
            new NullTrashConfirmationService())
    {
    }

    public MainWindowViewModel(IFolderPickerService folderPickerService)
        : this(folderPickerService, new DiskScanner(), new AvaloniaUiDispatcher())
    {
    }

    public MainWindowViewModel(
        IFolderPickerService folderPickerService,
        IDiskScanner diskScanner)
        : this(folderPickerService, diskScanner, new AvaloniaUiDispatcher())
    {
    }

    public MainWindowViewModel(
        IFolderPickerService folderPickerService,
        IDiskScanner diskScanner,
        IUiDispatcher uiDispatcher,
        IFileRevealService? fileRevealService = null,
        ITrashService? trashService = null,
        ITrashConfirmationService? trashConfirmationService = null,
        ISettingsService? settingsService = null,
        IClipboardService? clipboardService = null)
    {
        _folderPickerService = folderPickerService;
        _diskScanner = diskScanner;
        _uiDispatcher = uiDispatcher;
        _fileRevealService = fileRevealService ?? new MacFileRevealService();
        _trashService = trashService ?? new MacTrashService();
        _trashConfirmationService =
            trashConfirmationService ?? new NullTrashConfirmationService();
        _settingsService = settingsService ?? new InMemorySettingsService();
        _clipboardService = clipboardService ?? new NullClipboardService();

        LoadSettings();
    }

    public string ApplicationName { get; } = "MacStorageAtlas";

    [ObservableProperty]
    private string? _selectedFolderPath;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private bool _expandApplicationBundles = ScanOptions.Default.TreatPackagesAsDirectories;

    [ObservableProperty]
    private bool _includeHiddenFiles = ScanOptions.Default.IncludeHiddenFiles;

    [ObservableProperty]
    private bool _followSymbolicLinks = ScanOptions.Default.FollowSymbolicLinks;

    [ObservableProperty]
    private string? _currentPath;

    [ObservableProperty]
    private long _filesScanned;

    [ObservableProperty]
    private long _directoriesScanned;

    [ObservableProperty]
    private long _bytesScanned;

    [ObservableProperty]
    private IReadOnlyList<ScanError> _scanErrors = [];

    [ObservableProperty]
    private ScanError? _selectedScanError;

    [ObservableProperty]
    private IReadOnlyList<DiskItemTreeNodeViewModel> _treeItems = [];

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private DiskItemTreeNodeViewModel? _selectedTreeItem;

    [ObservableProperty]
    private IReadOnlyList<DiskItem> _largeFiles = [];

    [ObservableProperty]
    private DiskItem? _selectedLargeFile;

    [ObservableProperty]
    private IReadOnlyList<string> _recentLocations = [];

    [ObservableProperty]
    private string? _recentLocationStatusMessage;

    [ObservableProperty]
    private string? _revealStatusMessage;

    [ObservableProperty]
    private string? _trashStatusMessage;

    [ObservableProperty]
    private bool _isMovingToTrash;

    [ObservableProperty]
    private IReadOnlyList<TreemapRect> _treemapRectangles = [];

    [ObservableProperty]
    private IReadOnlyList<FileTypeSummary> _fileTypeSummaries = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedTreemapItem))]
    [NotifyPropertyChangedFor(nameof(SelectedTreemapItemFormattedSize))]
    [NotifyPropertyChangedFor(nameof(HasSelectedTreemapItem))]
    private TreemapRect? _selectedTreemapRectangle;

    public DiskItem? SelectedTreemapItem => SelectedTreemapRectangle?.Item.Item;

    public DiskItem? SelectedItem =>
        SelectedTreeItem?.Item ?? SelectedTreemapItem ?? SelectedLargeFile;

    public string SelectedTreemapItemFormattedSize => SelectedTreemapItem is null
        ? string.Empty
        : FileSizeFormatter.Format(SelectedTreemapItem.SizeBytes);

    public bool HasSelectedTreemapItem => SelectedTreemapItem is not null;

    [RelayCommand]
    private async Task SelectFolderAsync()
    {
        var selectedPath = await _folderPickerService.SelectFolderAsync();

        if (selectedPath is not null)
        {
            SelectedFolderPath = selectedPath;
            NotifyScanCommandsCanExecuteChanged();
        }
    }

    private bool CanScanFolder() =>
        !IsScanning && !string.IsNullOrWhiteSpace(SelectedFolderPath);

    private bool CanStopScan() => IsScanning;

    [RelayCommand(CanExecute = nameof(CanStopScan))]
    private void StopScan() => _scanCancellation?.Cancel();

    partial void OnIsScanningChanged(bool value)
    {
        NotifyScanCommandsCanExecuteChanged();
        StopScanCommand.NotifyCanExecuteChanged();
    }

    partial void OnIncludeHiddenFilesChanged(bool value) => SaveSettings();

    partial void OnFollowSymbolicLinksChanged(bool value) => SaveSettings();

    partial void OnExpandApplicationBundlesChanged(bool value) => SaveSettings();

    private bool CanRevealInFinder() => SelectedItem is not null;

    private bool CanMoveToTrash() => SelectedItem is not null && !IsMovingToTrash;

    private bool CanCopyErrorPath() => SelectedScanError is not null;

    [RelayCommand(CanExecute = nameof(CanCopyErrorPath))]
    private async Task CopyErrorPathAsync()
    {
        if (SelectedScanError is { } error)
        {
            await _clipboardService.SetTextAsync(error.Path);
        }
    }

    partial void OnSelectedScanErrorChanged(ScanError? value) =>
        CopyErrorPathCommand.NotifyCanExecuteChanged();

    [RelayCommand(CanExecute = nameof(CanRevealInFinder))]
    private void RevealInFinder()
    {
        var item = SelectedItem;
        if (item is null)
        {
            return;
        }

        try
        {
            RevealStatusMessage = _fileRevealService.Reveal(item.Path)
                ? null
                : "The selected item no longer exists or could not be revealed in Finder.";
        }
        catch (System.Exception)
        {
            RevealStatusMessage =
                "The selected item no longer exists or could not be revealed in Finder.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanMoveToTrash))]
    private async Task MoveToTrashAsync()
    {
        var item = SelectedItem;
        if (item is null || IsMovingToTrash)
        {
            return;
        }

        TrashStatusMessage = null;
        if (!await _trashConfirmationService.ConfirmMoveToTrashAsync(item))
        {
            return;
        }

        IsMovingToTrash = true;
        MoveToTrashCommand.NotifyCanExecuteChanged();

        try
        {
            await _trashService.MoveToTrashAsync(item.Path);
            RemoveTrashedItem(item);
        }
        catch (System.Exception exception)
        {
            TrashStatusMessage =
                $"Could not move “{item.Name}” to Trash. {exception.Message}";
        }
        finally
        {
            IsMovingToTrash = false;
            MoveToTrashCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanScanFolder))]
    private Task RescanAsync() => ScanFolderAsync();

    [RelayCommand]
    private async Task ScanRecentLocationAsync(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || IsScanning)
        {
            return;
        }

        if (!Directory.Exists(path))
        {
            RemoveRecentLocation(path);
            RecentLocationStatusMessage =
                $"“{path}” no longer exists and was removed from recent locations.";
            return;
        }

        RecentLocationStatusMessage = null;
        SelectedFolderPath = path;
        NotifyScanCommandsCanExecuteChanged();
        await ScanFolderAsync();
    }

    [RelayCommand(CanExecute = nameof(CanScanFolder))]
    private async Task ScanFolderAsync()
    {
        if (SelectedFolderPath is not { } rootPath)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _scanCancellation = cancellation;

        AddRecentLocation(rootPath);

        await _uiDispatcher.InvokeAsync(() =>
        {
            IsScanning = true;
            CurrentPath = rootPath;
            FilesScanned = 0;
            DirectoriesScanned = 0;
            BytesScanned = 0;
            ScanErrors = [];
            SelectedScanError = null;
            _scanRoot = null;
            TreeItems = [];
            SelectedTreeItem = null;
            LargeFiles = [];
            SelectedLargeFile = null;
            TreemapRectangles = [];
            FileTypeSummaries = [];
            SelectedTreemapRectangle = null;
            TrashStatusMessage = null;
            RecentLocationStatusMessage = null;
        });

        var options = new ScanOptions
        {
            TreatPackagesAsDirectories = ExpandApplicationBundles,
            IncludeHiddenFiles = IncludeHiddenFiles,
            FollowSymbolicLinks = FollowSymbolicLinks
        };

        try
        {
            // Run the scan on a background thread. The scanner's internal
            // `await Task.Yield()` would otherwise capture the UI synchronization
            // context and run the entire (CPU/IO-bound) scan on the UI thread,
            // freezing the window. Inside Task.Run there is no UI context, so the
            // scan stays off the UI thread. Awaiting each progress update gives
            // natural backpressure that keeps the UI responsive and animating.
            await Task.Run(async () =>
            {
                await foreach (var progress in _diskScanner
                                   .ScanAsync(rootPath, options, cancellation.Token)
                                   .ConfigureAwait(false))
                {
                    await _uiDispatcher.InvokeAsync(() => ApplyProgress(progress))
                        .ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The user stopped the scan. Keep whatever partial results were
            // gathered so far; cancellation is not a scan error.
        }
        finally
        {
            _scanCancellation = null;
            cancellation.Dispose();
            await _uiDispatcher.InvokeAsync(() => IsScanning = false);
        }
    }

    private void ApplyProgress(ScanProgress progress)
    {
        CurrentPath = progress.CurrentPath;
        FilesScanned = progress.FilesScanned;
        DirectoriesScanned = progress.DirectoriesScanned;
        BytesScanned = progress.BytesScanned;
        ScanErrors = progress.Errors;

        if (progress.IsCompleted)
        {
            _scanRoot = progress.Root;
            ApplySearch();
            SelectedTreeItem = null;
            SelectedTreemapRectangle = null;
            SelectedLargeFile = null;
            TreemapRectangles = LayoutChildren(progress.Root);
            FileTypeSummaries = _fileTypeStatisticsService.Calculate(progress.Root);
            LargeFiles = _largeFilesService.GetLargestFiles(progress.Root);
        }
    }

    partial void OnSelectedTreeItemChanged(DiskItemTreeNodeViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedItem));
        RevealInFinderCommand.NotifyCanExecuteChanged();
        MoveToTrashCommand.NotifyCanExecuteChanged();
        RevealStatusMessage = null;
        TrashStatusMessage = null;

        if (value is not null)
        {
            SelectedTreemapRectangle = null;
            SelectedLargeFile = null;
            TreemapRectangles = LayoutChildren(value.Item);
        }
    }

    partial void OnSelectedTreemapRectangleChanged(TreemapRect? value)
    {
        OnPropertyChanged(nameof(SelectedItem));
        RevealInFinderCommand.NotifyCanExecuteChanged();
        MoveToTrashCommand.NotifyCanExecuteChanged();
        RevealStatusMessage = null;
        TrashStatusMessage = null;

        if (value is not null)
        {
            SelectedTreeItem = null;
            SelectedLargeFile = null;
        }
    }

    partial void OnSelectedLargeFileChanged(DiskItem? value)
    {
        OnPropertyChanged(nameof(SelectedItem));
        RevealInFinderCommand.NotifyCanExecuteChanged();
        MoveToTrashCommand.NotifyCanExecuteChanged();
        RevealStatusMessage = null;
        TrashStatusMessage = null;

        if (value is not null)
        {
            SelectedTreeItem = null;
            SelectedTreemapRectangle = null;
        }
    }

    partial void OnSearchTextChanged(string value) => ApplySearch();

    private void ApplySearch()
    {
        TreeItems = _scanRoot is null
            ? []
            : DiskItemTreeFilter.Filter(_scanRoot, SearchText);
        SelectedTreeItem = null;
    }

    private IReadOnlyList<TreemapRect> LayoutChildren(DiskItem parent) =>
        _treemapLayoutService.Layout(
            parent.Children.Select(child => new TreemapItem(child)).ToArray(),
            new TreemapBounds(0, 0, TreemapWidth, TreemapHeight));

    private void RemoveTrashedItem(DiskItem item)
    {
        if (_scanRoot is null)
        {
            SelectedTreeItem = null;
            SelectedTreemapRectangle = null;
            SelectedLargeFile = null;
            return;
        }

        if (ReferenceEquals(_scanRoot, item))
        {
            _scanRoot = null;
            TreeItems = [];
            TreemapRectangles = [];
            FileTypeSummaries = [];
            LargeFiles = [];
        }
        else
        {
            var parent = FindParent(_scanRoot, item);
            _scanRoot.RemoveDescendant(item);
            ApplySearch();
            TreemapRectangles = LayoutChildren(parent ?? _scanRoot);
            FileTypeSummaries = _fileTypeStatisticsService.Calculate(_scanRoot);
            LargeFiles = _largeFilesService.GetLargestFiles(_scanRoot);
        }

        SelectedTreeItem = null;
        SelectedTreemapRectangle = null;
        SelectedLargeFile = null;
    }

    private static DiskItem? FindParent(DiskItem parent, DiskItem item)
    {
        if (parent.Children.Any(child => ReferenceEquals(child, item)))
        {
            return parent;
        }

        foreach (var child in parent.Children)
        {
            var result = FindParent(child, item);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }

    private void NotifyScanCommandsCanExecuteChanged()
    {
        ScanFolderCommand.NotifyCanExecuteChanged();
        RescanCommand.NotifyCanExecuteChanged();
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Load();

        _isApplyingSettings = true;
        try
        {
            IncludeHiddenFiles = settings.IncludeHiddenFiles;
            FollowSymbolicLinks = settings.FollowSymbolicLinks;
            ExpandApplicationBundles = settings.TreatPackagesAsDirectories;
            RecentLocations = settings.RecentLocations
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Take(AppSettings.MaxRecentLocations)
                .ToArray();
        }
        finally
        {
            _isApplyingSettings = false;
        }
    }

    private void SaveSettings()
    {
        if (_isApplyingSettings)
        {
            return;
        }

        _settingsService.Save(new AppSettings
        {
            IncludeHiddenFiles = IncludeHiddenFiles,
            FollowSymbolicLinks = FollowSymbolicLinks,
            TreatPackagesAsDirectories = ExpandApplicationBundles,
            RecentLocations = RecentLocations.ToList()
        });
    }

    private void AddRecentLocation(string path)
    {
        var updated = new List<string>(AppSettings.MaxRecentLocations) { path };
        updated.AddRange(RecentLocations.Where(existing =>
            !string.Equals(existing, path, StringComparison.OrdinalIgnoreCase)));

        if (updated.Count > AppSettings.MaxRecentLocations)
        {
            updated.RemoveRange(
                AppSettings.MaxRecentLocations,
                updated.Count - AppSettings.MaxRecentLocations);
        }

        RecentLocations = updated;
        SaveSettings();
    }

    private void RemoveRecentLocation(string path)
    {
        RecentLocations = RecentLocations
            .Where(existing =>
                !string.Equals(existing, path, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        SaveSettings();
    }
}
