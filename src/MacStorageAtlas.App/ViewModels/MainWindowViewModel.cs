using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacStorageAtlas.App.Converters;
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
    private static readonly TimeSpan DefaultSearchDebounceInterval =
        TimeSpan.FromMilliseconds(200);
    private readonly IFolderPickerService _folderPickerService;
    private readonly IDiskScanner _diskScanner;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly IFileRevealService _fileRevealService;
    private readonly IQuickLookService _quickLookService;
    private readonly ITrashService _trashService;
    private readonly ITrashConfirmationService _trashConfirmationService;
    private readonly ISettingsService _settingsService;
    private readonly IClipboardService _clipboardService;
    private readonly ISaveFilePickerService _saveFilePickerService;
    private readonly IFullDiskAccessService _fullDiskAccessService;
    private readonly ICleanupBasketReviewService _cleanupBasketReviewService;
    private readonly ICleanupFileSystemMetadataReader _cleanupFileSystemMetadataReader;
    private readonly AccessGuidanceClassifier _accessGuidanceClassifier = new();
    private readonly ITreemapLayoutService _treemapLayoutService = new TreemapLayoutService();
    private readonly FileTypeStatisticsService _fileTypeStatisticsService = new();
    private readonly LargeFilesService _largeFilesService = new();
    private readonly DiskItemFilterEvaluator _filterEvaluator = new();
    private readonly Dictionary<DiskItem, IReadOnlyList<TreemapRect>> _treemapLayoutCache =
        new(ReferenceEqualityComparer.Instance);
    private readonly TimeSpan _searchDebounceInterval;
    private readonly Func<DateTimeOffset> _referenceTimeProvider;
    private CleanupBasketPlanner? _cleanupBasketPlanner;
    private DiskItem? _scanRoot;
    private ScanOptions? _resultScanOptions;
    private CancellationTokenSource? _scanCancellation;
    private CancellationTokenSource? _exportCancellation;
    private CancellationTokenSource? _treePreparationCancellation;
    private CancellationTokenSource? _cleanupBasketCancellation;
    private FilterResult? _filterResult;
    private double? _windowWidth;
    private double? _windowHeight;
    private bool _isApplyingSettings;
    private bool _isSyncingSearchText;

    public MainWindowViewModel()
        : this(
            new NullFolderPickerService(),
            new DiskScanner(new MacFileMetadataReader()),
            new AvaloniaUiDispatcher(),
            new MacFileRevealService(),
            new MacTrashService(),
            new NullTrashConfirmationService(),
            quickLookService: new MacQuickLookService())
    {
    }

    public MainWindowViewModel(IFolderPickerService folderPickerService)
        : this(
            folderPickerService,
            new DiskScanner(new MacFileMetadataReader()),
            new AvaloniaUiDispatcher())
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
        IClipboardService? clipboardService = null,
        IQuickLookService? quickLookService = null,
        ISaveFilePickerService? saveFilePickerService = null,
        IFullDiskAccessService? fullDiskAccessService = null,
        ICleanupBasketReviewService? cleanupBasketReviewService = null,
        ICleanupFileSystemMetadataReader? cleanupFileSystemMetadataReader = null,
        TimeSpan? searchDebounceInterval = null,
        Func<DateTimeOffset>? referenceTimeProvider = null)
    {
        _folderPickerService = folderPickerService;
        _diskScanner = diskScanner;
        _uiDispatcher = uiDispatcher;
        _referenceTimeProvider = referenceTimeProvider ?? (() => DateTimeOffset.Now);
        Filter = new ResultFilterViewModel(_referenceTimeProvider);
        _searchDebounceInterval = searchDebounceInterval ?? DefaultSearchDebounceInterval;
        _fileRevealService = fileRevealService ?? new MacFileRevealService();
        _quickLookService = quickLookService ?? new MacQuickLookService();
        _trashService = trashService ?? new MacTrashService();
        _trashConfirmationService =
            trashConfirmationService ?? new NullTrashConfirmationService();
        _settingsService = settingsService ?? new InMemorySettingsService();
        _clipboardService = clipboardService ?? new NullClipboardService();
        _saveFilePickerService = saveFilePickerService ?? new NullSaveFilePickerService();
        _fullDiskAccessService = fullDiskAccessService ?? new NullFullDiskAccessService();
        _cleanupBasketReviewService =
            cleanupBasketReviewService ?? new NullCleanupBasketReviewService();
        _cleanupFileSystemMetadataReader =
            cleanupFileSystemMetadataReader ?? new CleanupFileSystemMetadataReader();

        Filter.CriteriaChanged += OnFilterCriteriaChanged;
        Filter.UserPresetsChanged += OnUserPresetsChanged;

        LoadSettings();
    }

    private void OnUserPresetsChanged(object? sender, EventArgs e) => SaveSettings();

    public ResultFilterViewModel Filter { get; }

    private void OnFilterCriteriaChanged(object? sender, EventArgs e)
    {
        if (!_isSyncingSearchText)
        {
            _isSyncingSearchText = true;
            try
            {
                SearchText = Filter.TextTerm;
            }
            finally
            {
                _isSyncingSearchText = false;
            }
        }

        ScheduleTreePreparation();
    }

    public string ApplicationName { get; } = "MacStorageAtlas";

    public double? InitialWindowWidth => _windowWidth;

    public double? InitialWindowHeight => _windowHeight;

    public IReadOnlyList<StorageMeasurementMode> MeasurementModes { get; } =
    [
        StorageMeasurementMode.SharedAwareAllocated,
        StorageMeasurementMode.Allocated,
        StorageMeasurementMode.Logical
    ];

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
    private StorageMeasurementMode _measurementMode =
        StorageMeasurementMode.SharedAwareAllocated;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MeasurementBasisLabel))]
    private StorageMeasurementMode _resultMeasurementMode =
        StorageMeasurementMode.SharedAwareAllocated;

    public string MeasurementBasisLabel =>
        StorageMeasurementModeLabelConverter.Label(ResultMeasurementMode);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CloneAccountingCoverageLabel))]
    private CloneAccountingCoverage _resultCloneAccountingCoverage =
        CloneAccountingCoverage.Unavailable;

    public string CloneAccountingCoverageLabel =>
        ResultCloneAccountingCoverage switch
        {
            CloneAccountingCoverage.Available =>
                "Verified full-clone accounting available",
            CloneAccountingCoverage.Unavailable =>
                "Verified full-clone accounting unavailable",
            CloneAccountingCoverage.Partial =>
                "Verified full-clone accounting partial",
            _ => throw new ArgumentOutOfRangeException(
                nameof(ResultCloneAccountingCoverage),
                ResultCloneAccountingCoverage,
                null)
        };

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
    private DateTimeOffset? _scanCompletedAt;

    partial void OnScanCompletedAtChanged(DateTimeOffset? value) =>
        NotifyExportCommandsCanExecuteChanged();

    [ObservableProperty]
    private bool _isExporting;

    partial void OnIsExportingChanged(bool value)
    {
        NotifyExportCommandsCanExecuteChanged();
        CancelExportCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    private string? _exportStatusMessage;

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
    private string? _quickLookStatusMessage;

    [ObservableProperty]
    private string? _trashStatusMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCleanupBasketItems))]
    private IReadOnlyList<CleanupBasketItem> _cleanupBasketItems = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CleanupBasketItemCount))]
    [NotifyPropertyChangedFor(nameof(CleanupBasketTotalLogicalSize))]
    [NotifyPropertyChangedFor(nameof(CleanupBasketExpectedReclaimableSize))]
    [NotifyPropertyChangedFor(nameof(FormattedCleanupBasketTotalLogicalSize))]
    [NotifyPropertyChangedFor(nameof(FormattedCleanupBasketExpectedReclaimableSize))]
    private CleanupBasketSummary _cleanupBasketSummary = CleanupBasketSummary.Empty;

    [ObservableProperty]
    private string? _cleanupBasketStatusMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExecutableCleanupBasketItemCount))]
    [NotifyPropertyChangedFor(nameof(HasBlockedCleanupBasketItems))]
    private IReadOnlyList<CleanupPreflightResult> _cleanupBasketPreflightResults = [];

    [ObservableProperty]
    private bool _isMovingCleanupBasketToTrash;

    partial void OnIsMovingCleanupBasketToTrashChanged(bool value)
    {
        MoveCleanupBasketToTrashCommand.NotifyCanExecuteChanged();
        CancelCleanupBasketMoveCommand.NotifyCanExecuteChanged();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CleanupBasketSucceededCount))]
    [NotifyPropertyChangedFor(nameof(CleanupBasketFailedCount))]
    [NotifyPropertyChangedFor(nameof(CleanupBasketUnattemptedCount))]
    private IReadOnlyList<CleanupOperationItemResult> _cleanupBasketOperationResults = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAccessGuidanceVisible))]
    [NotifyPropertyChangedFor(nameof(AccessGuidanceStatus))]
    [NotifyPropertyChangedFor(nameof(InaccessiblePathCount))]
    [NotifyPropertyChangedFor(nameof(AccessGuidanceTitle))]
    [NotifyPropertyChangedFor(nameof(AccessGuidanceMessage))]
    [NotifyPropertyChangedFor(nameof(FullDiskAccessManualFallback))]
    [NotifyPropertyChangedFor(nameof(ShowFullDiskAccessManualFallback))]
    private AccessGuidance _accessGuidance = AccessGuidance.None;

    [ObservableProperty]
    private int _selectedResultsTabIndex;

    [ObservableProperty]
    private bool _isMovingToTrash;

    [ObservableProperty]
    private IReadOnlyList<TreemapRect> _treemapRectangles = [];

    [ObservableProperty]
    private IReadOnlyList<DiskItem> _highlightedTreemapItems = [];

    partial void OnTreemapRectanglesChanged(IReadOnlyList<TreemapRect> value) =>
        RefreshTreemapHighlight();

    private void RefreshTreemapHighlight() =>
        HighlightedTreemapItems = _filterResult is { IsFilterActive: true } result
            ? TreemapRectangles
                .Select(rectangle => rectangle.Item.Item)
                .Where(result.IsVisible)
                .ToArray()
            : [];

    [ObservableProperty]
    private IReadOnlyList<FileTypeSummary> _fileTypeSummaries = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedTreemapItem))]
    private TreemapRect? _selectedTreemapRectangle;

    public DiskItem? SelectedTreemapItem => SelectedTreemapRectangle?.Item.Item;

    public DiskItem? SelectedItem =>
        SelectedTreeItem?.Item ?? SelectedTreemapItem ?? SelectedLargeFile;

    public string SelectedItemMeasuredSize => SelectedItem is null
        ? string.Empty
        : FileSizeFormatter.Format(SelectedItem.MeasuredSizeBytes);

    public string SelectedItemCountedSize => SelectedItem is null
        ? string.Empty
        : FileSizeFormatter.Format(SelectedItem.SizeBytes);

    public string SelectedItemSharedSize => SelectedItem is null
        ? string.Empty
        : FileSizeFormatter.Format(SelectedItem.SharedSizeBytes);

    public string SelectedItemKind => SelectedItem is null
        ? string.Empty
        : KindLabel(SelectedItem.Metadata.Kind);

    public string SelectedItemCreatedTime => SelectedItem is null
        ? string.Empty
        : FormatMetadataTime(SelectedItem.Metadata.CreatedTimeUtc);

    public string SelectedItemModifiedTime => SelectedItem is null
        ? string.Empty
        : FormatMetadataTime(SelectedItem.Metadata.ModifiedTimeUtc);

    public string SelectedItemLastAccessTime => SelectedItem is null
        ? string.Empty
        : FormatMetadataTime(SelectedItem.Metadata.LastAccessTimeUtc);

    public bool HasSelectedItem => SelectedItem is not null;

    public bool SelectedItemIsCountedElsewhere =>
        SelectedItem?.IsSizeCountedElsewhere == true;

    public bool HasCleanupBasketItems => CleanupBasketItems.Count > 0;

    public int CleanupBasketItemCount => CleanupBasketSummary.ItemCount;

    public long CleanupBasketTotalLogicalSize =>
        CleanupBasketSummary.TotalLogicalSizeBytes;

    public long CleanupBasketExpectedReclaimableSize =>
        CleanupBasketSummary.ExpectedReclaimableSizeBytes;

    public string FormattedCleanupBasketTotalLogicalSize =>
        FileSizeFormatter.Format(CleanupBasketSummary.TotalLogicalSizeBytes);

    public string FormattedCleanupBasketExpectedReclaimableSize =>
        FileSizeFormatter.Format(CleanupBasketSummary.ExpectedReclaimableSizeBytes);

    public int ExecutableCleanupBasketItemCount =>
        CleanupBasketPreflightResults.Count(item => item.CanExecute);

    public bool HasBlockedCleanupBasketItems =>
        CleanupBasketPreflightResults.Any(item => !item.CanExecute);

    public int CleanupBasketSucceededCount =>
        CleanupBasketOperationResults.Count(
            result => result.Status == CleanupOperationItemStatus.Succeeded);

    public int CleanupBasketFailedCount =>
        CleanupBasketOperationResults.Count(
            result => result.Status == CleanupOperationItemStatus.Failed);

    public int CleanupBasketUnattemptedCount =>
        CleanupBasketOperationResults.Count(
            result => result.Status is CleanupOperationItemStatus.Cancelled
                or CleanupOperationItemStatus.Unattempted);

    public bool IsAccessGuidanceVisible =>
        AccessGuidance.Status != AccessGuidanceStatus.None;

    public AccessGuidanceStatus AccessGuidanceStatus => AccessGuidance.Status;

    public int InaccessiblePathCount => AccessGuidance.InaccessiblePathCount;

    public string AccessGuidanceTitle =>
        AccessGuidance.Status switch
        {
            AccessGuidanceStatus.LikelyMissingFullDiskAccess =>
                "Full Disk Access may be needed",
            AccessGuidanceStatus.IncompleteScan =>
                "Some paths could not be scanned",
            AccessGuidanceStatus.Indeterminate =>
                "Access status is unclear",
            AccessGuidanceStatus.SettingsOpenFailure =>
                "Open Full Disk Access manually",
            AccessGuidanceStatus.None => string.Empty,
            _ => throw new ArgumentOutOfRangeException(
                nameof(AccessGuidance.Status),
                AccessGuidance.Status,
                null)
        };

    public string AccessGuidanceMessage =>
        AccessGuidance.Status switch
        {
            AccessGuidanceStatus.LikelyMissingFullDiskAccess =>
                InaccessiblePathMessage(
                    "macOS blocked access to protected locations. Grant Full Disk Access to MacStorageAtlas, restart the app if macOS asks, then rescan this location."),
            AccessGuidanceStatus.IncompleteScan =>
                InaccessiblePathMessage(
                    "The scan result may be incomplete. Some failures can be normal file permissions, removed files, removable media, or protected macOS locations."),
            AccessGuidanceStatus.Indeterminate =>
                "MacStorageAtlas cannot confirm whether macOS access is sufficient for this scan. If protected folders are missing, grant Full Disk Access, restart if needed, and rescan.",
            AccessGuidanceStatus.SettingsOpenFailure =>
                "System Settings could not be opened automatically. Use the manual path below to grant access, then restart MacStorageAtlas if macOS asks and rescan.",
            AccessGuidanceStatus.None => string.Empty,
            _ => throw new ArgumentOutOfRangeException(
                nameof(AccessGuidance.Status),
                AccessGuidance.Status,
                null)
        };

    public string FullDiskAccessManualFallback =>
        "System Settings > Privacy & Security > Full Disk Access";

    public bool ShowFullDiskAccessManualFallback =>
        AccessGuidance.Status == AccessGuidanceStatus.SettingsOpenFailure
        || AccessGuidance.ShowsManualSettingsFallback;

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
        OpenFullDiskAccessSettingsCommand.NotifyCanExecuteChanged();
        RescanAfterFullDiskAccessCommand.NotifyCanExecuteChanged();
    }

    partial void OnIncludeHiddenFilesChanged(bool value) => SaveSettings();

    partial void OnFollowSymbolicLinksChanged(bool value) => SaveSettings();

    partial void OnMeasurementModeChanged(StorageMeasurementMode value) => SaveSettings();

    partial void OnExpandApplicationBundlesChanged(bool value) => SaveSettings();

    public void SaveWindowSize(double width, double height)
    {
        if (!IsUsableWindowSize(width, height))
        {
            return;
        }

        _windowWidth = Math.Max(width, AppSettings.MinimumWindowWidth);
        _windowHeight = Math.Max(height, AppSettings.MinimumWindowHeight);
        SaveSettings();
    }

    private bool CanRevealInFinder() => SelectedItem is not null;

    private bool CanQuickLook() => SelectedItem is not null;

    private bool CanMoveToTrash() => SelectedItem is not null && !IsMovingToTrash;

    private bool CanAddSelectedItemToCleanupBasket() =>
        SelectedItem is not null && _cleanupBasketPlanner is not null;

    private bool CanRemoveSelectedItemFromCleanupBasket() =>
        SelectedItem is { } item
        && CleanupBasketItems.Any(basketItem => ReferenceEquals(basketItem.Item, item));

    private bool CanClearCleanupBasket() => HasCleanupBasketItems;

    private bool CanMoveCleanupBasketToTrash() =>
        HasCleanupBasketItems && !IsMovingCleanupBasketToTrash;

    private bool CanCancelCleanupBasketMove() => IsMovingCleanupBasketToTrash;

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

    partial void OnAccessGuidanceChanged(AccessGuidance value)
    {
        OpenFullDiskAccessSettingsCommand.NotifyCanExecuteChanged();
        RescanAfterFullDiskAccessCommand.NotifyCanExecuteChanged();
    }

    private bool CanOpenFullDiskAccessSettings() =>
        IsAccessGuidanceVisible && !IsScanning;

    [RelayCommand(CanExecute = nameof(CanOpenFullDiskAccessSettings))]
    private void OpenFullDiskAccessSettings()
    {
        if (!IsAccessGuidanceVisible || IsScanning)
        {
            return;
        }

        AccessGuidance = _fullDiskAccessService.OpenSettings() switch
        {
            FullDiskAccessSettingsResult.OpenedDirectly =>
                AccessGuidance with { ShowsManualSettingsFallback = false },
            FullDiskAccessSettingsResult.OpenedFallback =>
                AccessGuidance with { ShowsManualSettingsFallback = true },
            FullDiskAccessSettingsResult.Failed =>
                AccessGuidance with
                {
                    Status = AccessGuidanceStatus.SettingsOpenFailure,
                    ShowsManualSettingsFallback = true
                },
            _ => throw new ArgumentOutOfRangeException()
        };
    }

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

    [RelayCommand(CanExecute = nameof(CanQuickLook))]
    private void QuickLook()
    {
        var item = SelectedItem;
        if (item is null)
        {
            return;
        }

        try
        {
            QuickLookStatusMessage = _quickLookService.Preview(item.Path)
                ? null
                : "The selected item no longer exists or could not be previewed.";
        }
        catch (System.Exception)
        {
            QuickLookStatusMessage =
                "The selected item no longer exists or could not be previewed.";
        }
    }

    [RelayCommand(CanExecute = nameof(CanQuickLook))]
    private void ShowSelectedItemDetails()
    {
        if (SelectedItem is not null)
        {
            SelectedResultsTabIndex = 0;
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
            if (ReferenceEquals(_scanRoot, item))
            {
                RemoveTrashedItem(item);
            }
            else if (ResultMeasurementMode
                     == StorageMeasurementMode.SharedAwareAllocated
                     && _resultScanOptions is { } resultOptions
                     && _scanRoot is { } root)
            {
                await RunScanAsync(root.Path, resultOptions, addRecentLocation: false);
            }
            else
            {
                RemoveTrashedItem(item);
            }
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

    [RelayCommand(CanExecute = nameof(CanAddSelectedItemToCleanupBasket))]
    private void AddSelectedItemToCleanupBasket()
    {
        if (SelectedItem is not { } item || _cleanupBasketPlanner is null)
        {
            return;
        }

        var result = _cleanupBasketPlanner.Add(item);
        CleanupBasketStatusMessage = result.Message;
        RefreshCleanupBasketState();
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedItemFromCleanupBasket))]
    private void RemoveSelectedItemFromCleanupBasket()
    {
        if (SelectedItem is not { } item || _cleanupBasketPlanner is null)
        {
            return;
        }

        if (_cleanupBasketPlanner.Remove(item))
        {
            CleanupBasketStatusMessage = "Removed from the cleanup basket.";
        }

        RefreshCleanupBasketState();
    }

    [RelayCommand(CanExecute = nameof(CanClearCleanupBasket))]
    private void ClearCleanupBasket()
    {
        _cleanupBasketPlanner?.Clear();
        CleanupBasketStatusMessage = null;
        RefreshCleanupBasketState();
    }

    [RelayCommand(CanExecute = nameof(CanMoveCleanupBasketToTrash))]
    private async Task MoveCleanupBasketToTrashAsync()
    {
        if (_scanRoot is null || !HasCleanupBasketItems)
        {
            return;
        }

        IsMovingCleanupBasketToTrash = true;
        CleanupBasketStatusMessage = null;

        try
        {
            var validator = new CleanupPreflightValidator(
                new CleanupProtectedPathPolicy(_scanRoot),
                _cleanupFileSystemMetadataReader);
            var results = validator.Validate(CleanupBasketItems);
            CleanupBasketPreflightResults = results;

            if (!results.Any(result => result.CanExecute))
            {
                CleanupBasketStatusMessage =
                    "No cleanup basket items are ready to move to Trash.";
                return;
            }

            var review = new CleanupBasketReview(CleanupBasketSummary, results);
            if (!await _cleanupBasketReviewService.ConfirmCleanupAsync(review))
            {
                CleanupBasketStatusMessage = "Cleanup cancelled.";
                return;
            }

            await ExecuteCleanupBasketTrashAsync(results);
        }
        finally
        {
            IsMovingCleanupBasketToTrash = false;
            _cleanupBasketCancellation?.Dispose();
            _cleanupBasketCancellation = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancelCleanupBasketMove))]
    private void CancelCleanupBasketMove()
    {
        _cleanupBasketCancellation?.Cancel();
    }

    private async Task ExecuteCleanupBasketTrashAsync(
        IReadOnlyList<CleanupPreflightResult> preflightResults)
    {
        var operationResults = new List<CleanupOperationItemResult>();
        var executableResults = preflightResults
            .Where(result => result.CanExecute)
            .ToArray();
        var successfulItems = new List<CleanupBasketItem>();
        _cleanupBasketCancellation = new CancellationTokenSource();
        var cancellationToken = _cleanupBasketCancellation.Token;

        foreach (var result in preflightResults.Where(result => !result.CanExecute))
        {
            operationResults.Add(new CleanupOperationItemResult(
                result.Item,
                CleanupOperationItemStatus.Unattempted,
                result.Status.Message));
        }

        for (var index = 0; index < executableResults.Length; index++)
        {
            var result = executableResults[index];
            if (cancellationToken.IsCancellationRequested)
            {
                AddUnattemptedResults(operationResults, executableResults[index..]);
                break;
            }

            try
            {
                await _trashService.MoveToTrashAsync(
                    result.Item.Snapshot.Path,
                    cancellationToken);
                operationResults.Add(new CleanupOperationItemResult(
                    result.Item,
                    CleanupOperationItemStatus.Succeeded));
                successfulItems.Add(result.Item);
            }
            catch (OperationCanceledException)
            {
                operationResults.Add(new CleanupOperationItemResult(
                    result.Item,
                    CleanupOperationItemStatus.Cancelled,
                    "Cleanup was cancelled."));
                AddUnattemptedResults(
                    operationResults,
                    executableResults[(index + 1)..]);
                break;
            }
            catch (System.Exception exception)
            {
                operationResults.Add(new CleanupOperationItemResult(
                    result.Item,
                    CleanupOperationItemStatus.Failed,
                    exception.Message));
            }
        }

        CleanupBasketOperationResults = operationResults;
        await ReconcileCleanupBasketSuccessesAsync(successfulItems);
        CleanupBasketStatusMessage = FormatCleanupBasketOperationMessage(operationResults);
    }

    private static void AddUnattemptedResults(
        List<CleanupOperationItemResult> operationResults,
        IReadOnlyList<CleanupPreflightResult> unattemptedResults)
    {
        foreach (var result in unattemptedResults)
        {
            operationResults.Add(new CleanupOperationItemResult(
                result.Item,
                CleanupOperationItemStatus.Unattempted,
                "Not attempted."));
        }
    }

    private async Task ReconcileCleanupBasketSuccessesAsync(
        IReadOnlyList<CleanupBasketItem> successfulItems)
    {
        if (successfulItems.Count == 0 || _cleanupBasketPlanner is null)
        {
            return;
        }

        foreach (var item in successfulItems)
        {
            _cleanupBasketPlanner.Remove(item.Item);
        }

        if (ResultMeasurementMode == StorageMeasurementMode.SharedAwareAllocated
            && _resultScanOptions is { } resultOptions
            && _scanRoot is { } root)
        {
            await RunScanAsync(root.Path, resultOptions, addRecentLocation: false);
            return;
        }

        foreach (var item in successfulItems)
        {
            RemoveTrashedItem(item.Item);
        }

        RefreshCleanupBasketState();
    }

    private static string FormatCleanupBasketOperationMessage(
        IReadOnlyList<CleanupOperationItemResult> results)
    {
        var succeeded = results.Count(
            result => result.Status == CleanupOperationItemStatus.Succeeded);
        var failed = results.Count(
            result => result.Status == CleanupOperationItemStatus.Failed);
        var unattempted = results.Count(
            result => result.Status is CleanupOperationItemStatus.Cancelled
                or CleanupOperationItemStatus.Unattempted);

        if (failed == 0 && unattempted == 0)
        {
            return $"Moved {succeeded} item(s) to Trash.";
        }

        return $"Moved {succeeded} item(s) to Trash. Failed: {failed}. Not attempted: {unattempted}.";
    }

    [RelayCommand(CanExecute = nameof(CanScanFolder))]
    private Task RescanAsync() => ScanFolderAsync();

    private bool CanRescanAfterFullDiskAccess() =>
        IsAccessGuidanceVisible
        && !IsScanning
        && _scanRoot is not null
        && _resultScanOptions is not null;

    [RelayCommand(CanExecute = nameof(CanRescanAfterFullDiskAccess))]
    private Task RescanAfterFullDiskAccessAsync()
    {
        if (_scanRoot is not { } root || _resultScanOptions is not { } options)
        {
            return Task.CompletedTask;
        }

        return RunScanAsync(root.Path, options, addRecentLocation: false);
    }

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

        var options = new ScanOptions
        {
            TreatPackagesAsDirectories = ExpandApplicationBundles,
            IncludeHiddenFiles = IncludeHiddenFiles,
            FollowSymbolicLinks = FollowSymbolicLinks,
            MeasurementMode = MeasurementMode
        };

        await RunScanAsync(rootPath, options, addRecentLocation: true);
    }

    private async Task RunScanAsync(
        string rootPath,
        ScanOptions options,
        bool addRecentLocation)
    {
        var cancellation = new CancellationTokenSource();
        _scanCancellation = cancellation;

        if (addRecentLocation)
        {
            AddRecentLocation(rootPath);
        }

        await _uiDispatcher.InvokeAsync(() =>
        {
            IsScanning = true;
            CurrentPath = rootPath;
            FilesScanned = 0;
            DirectoriesScanned = 0;
            BytesScanned = 0;
            ResultMeasurementMode = options.MeasurementMode;
            ResultCloneAccountingCoverage = CloneAccountingCoverage.Unavailable;
            ScanErrors = [];
            ScanCompletedAt = null;
            SelectedScanError = null;
            _scanRoot = null;
            _resultScanOptions = null;
            _treemapLayoutCache.Clear();
            TreeItems = [];
            SelectedTreeItem = null;
            LargeFiles = [];
            SelectedLargeFile = null;
            TreemapRectangles = [];
            FileTypeSummaries = [];
            SelectedTreemapRectangle = null;
            TrashStatusMessage = null;
            QuickLookStatusMessage = null;
            RecentLocationStatusMessage = null;
            AccessGuidance = AccessGuidance.None;
            ClearCleanupBasketForResultReplacement();
        });

        try
        {
            await Task.Run(async () =>
            {
                await foreach (var progress in _diskScanner
                                   .ScanAsync(rootPath, options, cancellation.Token)
                                   .ConfigureAwait(false))
                {
                    var accessAssessment = progress.IsCompleted
                        ? CheckFullDiskAccess(rootPath)
                        : null;
                    await _uiDispatcher.InvokeAsync(
                            () => ApplyProgress(progress, options, accessAssessment))
                        .ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _scanCancellation = null;
            cancellation.Dispose();
            await _uiDispatcher.InvokeAsync(() => IsScanning = false);
        }
    }

    private void ApplyProgress(
        ScanProgress progress,
        ScanOptions options,
        FullDiskAccessAssessment? accessAssessment)
    {
        CurrentPath = progress.CurrentPath;
        FilesScanned = progress.FilesScanned;
        DirectoriesScanned = progress.DirectoriesScanned;
        BytesScanned = progress.BytesScanned;
        ResultMeasurementMode = progress.MeasurementMode;
        ResultCloneAccountingCoverage = progress.CloneAccountingCoverage;
        ScanErrors = progress.Errors;

        if (progress.IsCompleted)
        {
            _scanRoot = progress.Root;
            _resultScanOptions = options;
            _cleanupBasketPlanner = new CleanupBasketPlanner(
                progress.Root,
                progress.MeasurementMode,
                new CleanupProtectedPathPolicy(progress.Root));
            RefreshCleanupBasketState();
            ScanCompletedAt = _referenceTimeProvider();
            SelectedTreeItem = null;
            SelectedTreemapRectangle = null;
            SelectedLargeFile = null;
            TreemapRectangles = LayoutChildren(progress.Root);
            AccessGuidance = _accessGuidanceClassifier.Classify(
                progress.Errors,
                accessAssessment ?? FullDiskAccessAssessment.Indeterminate);
            ApplySearch();
        }
    }

    private FullDiskAccessAssessment CheckFullDiskAccess(string rootPath)
    {
        try
        {
            return _fullDiskAccessService.CheckAccess(rootPath);
        }
        catch (System.Exception)
        {
            return FullDiskAccessAssessment.Indeterminate;
        }
    }

    partial void OnSelectedTreeItemChanged(DiskItemTreeNodeViewModel? value)
    {
        NotifySelectedItemPropertiesChanged();
        RevealInFinderCommand.NotifyCanExecuteChanged();
        QuickLookCommand.NotifyCanExecuteChanged();
        ShowSelectedItemDetailsCommand.NotifyCanExecuteChanged();
        MoveToTrashCommand.NotifyCanExecuteChanged();
        NotifyCleanupBasketCommandsCanExecuteChanged();
        RevealStatusMessage = null;
        QuickLookStatusMessage = null;
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
        NotifySelectedItemPropertiesChanged();
        RevealInFinderCommand.NotifyCanExecuteChanged();
        QuickLookCommand.NotifyCanExecuteChanged();
        ShowSelectedItemDetailsCommand.NotifyCanExecuteChanged();
        MoveToTrashCommand.NotifyCanExecuteChanged();
        NotifyCleanupBasketCommandsCanExecuteChanged();
        RevealStatusMessage = null;
        QuickLookStatusMessage = null;
        TrashStatusMessage = null;

        if (value is not null)
        {
            SelectedTreeItem = null;
            SelectedLargeFile = null;
        }
    }

    partial void OnSelectedLargeFileChanged(DiskItem? value)
    {
        NotifySelectedItemPropertiesChanged();
        RevealInFinderCommand.NotifyCanExecuteChanged();
        QuickLookCommand.NotifyCanExecuteChanged();
        ShowSelectedItemDetailsCommand.NotifyCanExecuteChanged();
        MoveToTrashCommand.NotifyCanExecuteChanged();
        NotifyCleanupBasketCommandsCanExecuteChanged();
        RevealStatusMessage = null;
        QuickLookStatusMessage = null;
        TrashStatusMessage = null;

        if (value is not null)
        {
            SelectedTreeItem = null;
            SelectedTreemapRectangle = null;
        }
    }

    partial void OnSearchTextChanged(string value)
    {
        if (_isSyncingSearchText)
        {
            return;
        }

        _isSyncingSearchText = true;
        try
        {
            Filter.TextTerm = value;
        }
        finally
        {
            _isSyncingSearchText = false;
        }
    }

    private void NotifySelectedItemPropertiesChanged()
    {
        OnPropertyChanged(nameof(SelectedItem));
        OnPropertyChanged(nameof(SelectedItemMeasuredSize));
        OnPropertyChanged(nameof(SelectedItemCountedSize));
        OnPropertyChanged(nameof(SelectedItemSharedSize));
        OnPropertyChanged(nameof(SelectedItemKind));
        OnPropertyChanged(nameof(SelectedItemCreatedTime));
        OnPropertyChanged(nameof(SelectedItemModifiedTime));
        OnPropertyChanged(nameof(SelectedItemLastAccessTime));
        OnPropertyChanged(nameof(HasSelectedItem));
        OnPropertyChanged(nameof(SelectedItemIsCountedElsewhere));
    }

    private void ClearCleanupBasketForResultReplacement()
    {
        _cleanupBasketPlanner = null;
        CleanupBasketStatusMessage = null;
        RefreshCleanupBasketState();
    }

    private void RefreshCleanupBasketState()
    {
        CleanupBasketItems = _cleanupBasketPlanner?.Items.ToArray() ?? [];
        CleanupBasketSummary =
            _cleanupBasketPlanner?.Summary ?? CleanupBasketSummary.Empty;
        CleanupBasketPreflightResults = [];
        NotifyCleanupBasketCommandsCanExecuteChanged();
    }

    private void NotifyCleanupBasketCommandsCanExecuteChanged()
    {
        AddSelectedItemToCleanupBasketCommand.NotifyCanExecuteChanged();
        RemoveSelectedItemFromCleanupBasketCommand.NotifyCanExecuteChanged();
        ClearCleanupBasketCommand.NotifyCanExecuteChanged();
        MoveCleanupBasketToTrashCommand.NotifyCanExecuteChanged();
        CancelCleanupBasketMoveCommand.NotifyCanExecuteChanged();
    }

    private static string KindLabel(DiskItemKind kind) =>
        kind switch
        {
            DiskItemKind.File => "File",
            DiskItemKind.Directory => "Folder",
            DiskItemKind.ApplicationBundle => "Application bundle",
            DiskItemKind.SymbolicLink => "Symbolic link",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static string FormatMetadataTime(DateTimeOffset? value) =>
        value is { } time
            ? time.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
            : "Unknown";

    internal Task TreePreparation { get; private set; } = Task.CompletedTask;

    private void ApplySearch()
    {
        CancelTreePreparation();

        var request = CreateFilterRequest();
        if (_scanRoot is null
            || !request.Filter.Validate(request.ReferenceTime).IsValid)
        {
            ApplyEmptyPreparation();
            return;
        }

        ApplyPreparation(Prepare(_scanRoot, request, CancellationToken.None));
    }

    private void CancelTreePreparation()
    {
        _treePreparationCancellation?.Cancel();
        _treePreparationCancellation = null;
        TreePreparation = Task.CompletedTask;
    }

    private void ScheduleTreePreparation()
    {
        var previous = _treePreparationCancellation;
        var cancellation = new CancellationTokenSource();
        _treePreparationCancellation = cancellation;
        previous?.Cancel();

        TreePreparation = PrepareTreeAsync(_scanRoot, CreateFilterRequest(), cancellation);
    }

    private FilterRequest CreateFilterRequest() =>
        new(Filter.CurrentFilter, _referenceTimeProvider());

    private async Task PrepareTreeAsync(
        DiskItem? root,
        FilterRequest request,
        CancellationTokenSource cancellation)
    {
        var token = cancellation.Token;

        try
        {
            if (_searchDebounceInterval > TimeSpan.Zero)
            {
                await Task.Delay(_searchDebounceInterval, token).ConfigureAwait(false);
            }

            if (root is null || !request.Filter.Validate(request.ReferenceTime).IsValid)
            {
                await _uiDispatcher.InvokeAsync(() =>
                {
                    if (ReferenceEquals(_treePreparationCancellation, cancellation))
                    {
                        ApplyEmptyPreparation();
                    }
                }).ConfigureAwait(false);
                return;
            }

            var prepared = await Task.Run(
                    () => Prepare(root, request, token),
                    token)
                .ConfigureAwait(false);

            token.ThrowIfCancellationRequested();

            await _uiDispatcher.InvokeAsync(() =>
            {
                if (!ReferenceEquals(_treePreparationCancellation, cancellation))
                {
                    return;
                }

                ApplyPreparation(prepared);
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(_treePreparationCancellation, cancellation))
            {
                _treePreparationCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private PreparedResult Prepare(
        DiskItem root,
        FilterRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.Filter.IsActive)
        {
            return new PreparedResult(
                null,
                DiskItemTreeFilter.Filter(root, searchText: null),
                _largeFilesService.GetLargestFiles(root),
                _fileTypeStatisticsService.Calculate(root));
        }

        var result = _filterEvaluator.Evaluate(
            root,
            request.Filter,
            request.ReferenceTime,
            cancellationToken);
        return new PreparedResult(
            result,
            DiskItemTreeFilter.Filter(root, result),
            _largeFilesService.GetLargestFiles(result.MatchedFiles),
            _fileTypeStatisticsService.Calculate(result.MatchedFiles));
    }

    private void ApplyPreparation(PreparedResult prepared)
    {
        _filterResult = prepared.Result;
        TreeItems = prepared.TreeItems;
        LargeFiles = prepared.LargeFiles;
        FileTypeSummaries = prepared.FileTypeSummaries;

        if (prepared.Result is { } result)
        {
            Filter.ApplyMatchSummary(result);
        }

        OnPropertyChanged(nameof(TreeSizeColumnHeader));
        OnPropertyChanged(nameof(IsFilterActive));
        RefreshTreemapHighlight();
        ReconcileSelections();
    }

    private void ApplyEmptyPreparation()
    {
        _filterResult = null;
        TreeItems = [];
        LargeFiles = [];
        FileTypeSummaries = [];
        OnPropertyChanged(nameof(TreeSizeColumnHeader));
        OnPropertyChanged(nameof(IsFilterActive));
        RefreshTreemapHighlight();
        ReconcileSelections();
    }

    private void ReconcileSelections()
    {
        if (SelectedTreeItem is { } treeItem)
        {
            if (!IsVisibleInTree(treeItem.Item))
            {
                SelectedTreeItem = null;
            }
            else if (FindNode(TreeItems, treeItem.Item) is { } replacement
                     && !ReferenceEquals(replacement, treeItem))
            {
                SelectedTreeItem = replacement;
            }
        }

        if (SelectedLargeFile is { } largeFile
            && !LargeFiles.Any(file => ReferenceEquals(file, largeFile)))
        {
            SelectedLargeFile = null;
        }
    }

    private bool IsVisibleInTree(DiskItem item) =>
        _filterResult is not { } result || result.IsVisible(item);

    private static DiskItemTreeNodeViewModel? FindNode(
        IReadOnlyList<DiskItemTreeNodeViewModel> nodes,
        DiskItem target)
    {
        foreach (var node in nodes)
        {
            if (ReferenceEquals(node.Item, target))
            {
                return node;
            }

            if (node.Item.IsDirectory
                && IsUnder(target.Path, node.Item.Path)
                && FindNode(node.Children, target) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    private static bool IsUnder(string path, string directoryPath) =>
        path.Length > directoryPath.Length
        && path.StartsWith(directoryPath, StringComparison.Ordinal)
        && (directoryPath.EndsWith(Path.DirectorySeparatorChar)
            || path[directoryPath.Length] == Path.DirectorySeparatorChar);

    public bool IsFilterActive => _filterResult?.IsFilterActive == true;

    public bool HasScanResult => _scanRoot is not null;

    public bool ShowEmptyFilterResult => HasScanResult && TreeItems.Count == 0;

    public string TreeSizeColumnHeader => IsFilterActive ? "Matched size" : "Size";

    partial void OnTreeItemsChanged(IReadOnlyList<DiskItemTreeNodeViewModel> value)
    {
        OnPropertyChanged(nameof(HasScanResult));
        OnPropertyChanged(nameof(ShowEmptyFilterResult));
    }

    public bool IsMatched(DiskItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return _filterResult is not { } result || !result.IsFilterActive || result.Matches(item);
    }

    private sealed record PreparedResult(
        FilterResult? Result,
        IReadOnlyList<DiskItemTreeNodeViewModel> TreeItems,
        IReadOnlyList<DiskItem> LargeFiles,
        IReadOnlyList<FileTypeSummary> FileTypeSummaries);

    private sealed record FilterRequest(
        DiskItemFilter Filter,
        DateTimeOffset ReferenceTime);

    private IReadOnlyList<TreemapRect> LayoutChildren(DiskItem parent)
    {
        if (_treemapLayoutCache.TryGetValue(parent, out var rectangles))
        {
            return rectangles;
        }

        rectangles = _treemapLayoutService.Layout(
            parent.Children.Select(child => new TreemapItem(child)).ToArray(),
            new TreemapBounds(0, 0, TreemapWidth, TreemapHeight));
        _treemapLayoutCache[parent] = rectangles;
        return rectangles;
    }

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
            _resultScanOptions = null;
            _treemapLayoutCache.Clear();
            TreeItems = [];
            TreemapRectangles = [];
            FileTypeSummaries = [];
            LargeFiles = [];
        }
        else
        {
            var parent = FindParent(_scanRoot, item);
            _scanRoot.RemoveDescendant(item);
            _treemapLayoutCache.Clear();
            TreemapRectangles = LayoutChildren(parent ?? _scanRoot);
            ApplySearch();
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
        RescanAfterFullDiskAccessCommand.NotifyCanExecuteChanged();
        NotifyExportCommandsCanExecuteChanged();
    }

    private string InaccessiblePathMessage(string suffix)
    {
        var pathText = InaccessiblePathCount == 1
            ? "1 path was inaccessible."
            : $"{InaccessiblePathCount.ToString(CultureInfo.CurrentCulture)} paths were inaccessible.";

        return $"{pathText} {suffix}";
    }

    private void NotifyExportCommandsCanExecuteChanged()
    {
        ExportCsvCommand.NotifyCanExecuteChanged();
        ExportJsonCommand.NotifyCanExecuteChanged();
    }

    private bool CanExport() =>
        !IsScanning
        && !IsExporting
        && _scanRoot is not null
        && _resultScanOptions is not null
        && ScanCompletedAt is not null;

    private bool CanCancelExport() => IsExporting;

    [RelayCommand(CanExecute = nameof(CanCancelExport))]
    private void CancelExport() => _exportCancellation?.Cancel();

    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportCsvAsync() => ExportAsync(ScanExportFormat.Csv);

    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportJsonAsync() => ExportAsync(ScanExportFormat.Json);

    private async Task ExportAsync(ScanExportFormat format)
    {
        if (_scanRoot is not { } root
            || _resultScanOptions is not { } options
            || ScanCompletedAt is not { } completedAt)
        {
            return;
        }

        var destination = await _saveFilePickerService.SelectSaveFileAsync(
            format,
            SuggestedExportFileName(root, format, completedAt));

        if (destination is null)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _exportCancellation = cancellation;
        var measurementMode = ResultMeasurementMode;
        var coverage = ResultCloneAccountingCoverage;
        var filterResult = _filterResult;
        var errors = ScanErrors;

        await _uiDispatcher.InvokeAsync(() =>
        {
            IsExporting = true;
            ExportStatusMessage = null;
        });

        try
        {
            var itemCount = await Task.Run(
                    async () =>
                    {
                        var request = ScanExportRequestFactory.Create(
                            root,
                            options,
                            measurementMode,
                            coverage,
                            completedAt,
                            filterResult,
                            errors,
                            cancellation.Token);

                        await WriteExportAsync(
                                request,
                                destination,
                                format,
                                cancellation.Token)
                            .ConfigureAwait(false);

                        return request.Metadata.ItemCount;
                    },
                    cancellation.Token)
                .ConfigureAwait(false);

            await _uiDispatcher.InvokeAsync(() =>
                ExportStatusMessage = ExportCompletionMessage(
                    itemCount,
                    errors.Count,
                    destination));
        }
        catch (OperationCanceledException)
        {
            await _uiDispatcher.InvokeAsync(() =>
                ExportStatusMessage =
                    "The export was cancelled. No file was written to the chosen location.");
        }
        catch (System.Exception exception)
        {
            await _uiDispatcher.InvokeAsync(() =>
                ExportStatusMessage =
                    $"The export failed and no file was written: {exception.Message}");
        }
        finally
        {
            _exportCancellation = null;
            cancellation.Dispose();
            await _uiDispatcher.InvokeAsync(() => IsExporting = false);
        }
    }

    private static async Task WriteExportAsync(
        ScanExportRequest request,
        string destination,
        ScanExportFormat format,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(destination);
        var temporaryPath = Path.Combine(
            string.IsNullOrEmpty(directory) ? "." : directory,
            $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");

        try
        {
            if (format == ScanExportFormat.Csv)
            {
                await using var stream = CreateTemporaryStream(temporaryPath);
                await using var writer = new StreamWriter(
                    stream,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
                await ScanResultCsvWriter.WriteAsync(request, writer, cancellationToken)
                    .ConfigureAwait(false);
                await writer.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await using var stream = CreateTemporaryStream(temporaryPath);
                await ScanResultJsonWriter.WriteAsync(request, stream, cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(temporaryPath, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static FileStream CreateTemporaryStream(string temporaryPath) =>
        new(
            temporaryPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            useAsync: true);

    private static string SuggestedExportFileName(
        DiskItem root,
        ScanExportFormat format,
        DateTimeOffset completedAt)
    {
        var folderName = Path.GetFileName(root.Path.TrimEnd(Path.DirectorySeparatorChar));
        var label = string.IsNullOrWhiteSpace(folderName) ? "volume" : folderName;
        var stamp = completedAt.ToLocalTime()
            .ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var extension = format == ScanExportFormat.Csv ? "csv" : "json";

        return $"MacStorageAtlas-{label}-{stamp}.{extension}";
    }

    private static string ExportCompletionMessage(
        long itemCount,
        int errorCount,
        string destination)
    {
        var items = itemCount.ToString("N0", CultureInfo.CurrentCulture);
        var fileName = Path.GetFileName(destination);
        var message = itemCount == 1
            ? $"Exported 1 item to {fileName}."
            : $"Exported {items} items to {fileName}.";

        if (errorCount == 0)
        {
            return message;
        }

        var errors = errorCount.ToString("N0", CultureInfo.CurrentCulture);
        var unreadable = errorCount == 1
            ? "1 path could not be read during the scan"
            : $"{errors} paths could not be read during the scan";

        return $"{message} {unreadable}, so the export does not describe them.";
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
            MeasurementMode = settings.EffectiveMeasurementMode;
            _windowWidth = ValidWindowDimension(
                settings.WindowWidth,
                AppSettings.MinimumWindowWidth);
            _windowHeight = ValidWindowDimension(
                settings.WindowHeight,
                AppSettings.MinimumWindowHeight);
            RecentLocations = settings.RecentLocations
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Take(AppSettings.MaxRecentLocations)
                .ToArray();
            Filter.LoadUserPresets(settings.FilterPresets
                .Select(preset => preset.TryCreatePreset())
                .Where(preset => preset is not null)
                .Cast<FilterPreset>());
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
            MeasurementMode = MeasurementMode,
            RecentLocations = RecentLocations.ToList(),
            FilterPresets = Filter.UserPresets
                .Select(FilterPresetSettings.FromPreset)
                .ToList(),
            WindowWidth = _windowWidth,
            WindowHeight = _windowHeight
        });
    }

    private static bool IsUsableWindowSize(double width, double height) =>
        double.IsFinite(width)
        && double.IsFinite(height)
        && width >= AppSettings.MinimumWindowWidth
        && height >= AppSettings.MinimumWindowHeight;

    private static double? ValidWindowDimension(double? value, double minimum) =>
        value is { } dimension
        && double.IsFinite(dimension)
        && dimension >= minimum
        && dimension <= 10_000
            ? dimension
            : null;

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
