using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
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
    private readonly ITreemapLayoutService _treemapLayoutService = new TreemapLayoutService();
    private readonly FileTypeStatisticsService _fileTypeStatisticsService = new();
    private readonly LargeFilesService _largeFilesService = new();
    private readonly DiskItemFilterEvaluator _filterEvaluator = new();
    private readonly Dictionary<DiskItem, IReadOnlyList<TreemapRect>> _treemapLayoutCache =
        new(ReferenceEqualityComparer.Instance);
    private readonly TimeSpan _searchDebounceInterval;
    private readonly Func<DateTimeOffset> _referenceTimeProvider;
    private DiskItem? _scanRoot;
    private ScanOptions? _resultScanOptions;
    private CancellationTokenSource? _scanCancellation;
    private CancellationTokenSource? _treePreparationCancellation;
    private FilterResult? _filterResult;
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

    partial void OnMeasurementModeChanged(StorageMeasurementMode value) => SaveSettings();

    partial void OnExpandApplicationBundlesChanged(bool value) => SaveSettings();

    private bool CanRevealInFinder() => SelectedItem is not null;

    private bool CanQuickLook() => SelectedItem is not null;

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
        });

        try
        {
            await Task.Run(async () =>
            {
                await foreach (var progress in _diskScanner
                                   .ScanAsync(rootPath, options, cancellation.Token)
                                   .ConfigureAwait(false))
                {
                    await _uiDispatcher.InvokeAsync(
                            () => ApplyProgress(progress, options))
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

    private void ApplyProgress(ScanProgress progress, ScanOptions options)
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
            SelectedTreeItem = null;
            SelectedTreemapRectangle = null;
            SelectedLargeFile = null;
            TreemapRectangles = LayoutChildren(progress.Root);
            ApplySearch();
        }
    }

    partial void OnSelectedTreeItemChanged(DiskItemTreeNodeViewModel? value)
    {
        NotifySelectedItemPropertiesChanged();
        RevealInFinderCommand.NotifyCanExecuteChanged();
        QuickLookCommand.NotifyCanExecuteChanged();
        ShowSelectedItemDetailsCommand.NotifyCanExecuteChanged();
        MoveToTrashCommand.NotifyCanExecuteChanged();
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
                .ToList()
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
