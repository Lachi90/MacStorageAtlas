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
using MacStorageAtlas.Core.Access;
using MacStorageAtlas.Core.Cleanup;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.App.ViewModels;

public partial class MainWindowViewModel
{
    internal const string SharedAwareFolderRescanExplanation =
        "This result was measured in shared-aware allocated mode, which attributes hardlinks "
        + "and clones across the whole scan, so one folder cannot be remeasured on its own.";

    internal const string FollowedSymbolicLinkFolderRescanExplanation =
        "This result was scanned with symbolic links followed, so the folders reached through "
        + "links depend on the paths visited before them and one folder cannot be remeasured "
        + "on its own.";

    private Task _scanHistoryCapture = Task.CompletedTask;
    private Task _duplicateAnalysis = Task.CompletedTask;
    private IReadOnlySet<string>? _pendingTreeExpansion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowScanOverlay))]
    private bool _isRescanningFolder;

    [ObservableProperty]
    private string? _folderRescanProgressMessage;

    [ObservableProperty]
    private string? _folderRescanStatusMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRefreshedFolders))]
    [NotifyPropertyChangedFor(nameof(RefreshedFoldersSummary))]
    [NotifyPropertyChangedFor(nameof(RefreshedFoldersDetails))]
    private ScanResultProvenance? _resultProvenance;

    public bool ShowScanOverlay => IsScanning && !IsRescanningFolder;

    public bool IsSelectedItemDirectory => SelectedItem?.IsDirectory == true;

    public string RescanSelectedFolderAccessibleName =>
        SelectedItem is { IsDirectory: true } item
            ? $"Rescan folder {DisplayNameConverter.Display(item.Name)}"
            : "Rescan this folder";

    public bool HasRefreshedFolders => ResultProvenance?.HasRefreshedFolders == true;

    public string? RefreshedFoldersSummary
    {
        get
        {
            if (ResultProvenance is not { HasRefreshedFolders: true } provenance)
            {
                return null;
            }

            var count = provenance.RefreshedFolders.Count;
            var folders = count == 1
                ? "1 folder was rescanned"
                : $"{count.ToString("N0", CultureInfo.CurrentCulture)} folders were rescanned";
            return $"Original scan {FormatProvenanceTime(provenance.ScanCompletedAt)}. "
                + $"{folders} later, so this result mixes measurement times.";
        }
    }

    public string? RefreshedFoldersDetails =>
        ResultProvenance is { HasRefreshedFolders: true } provenance
            ? string.Join(
                Environment.NewLine,
                provenance.RefreshedFolders.Select(folder =>
                    $"{folder.Path} rescanned {FormatProvenanceTime(folder.CompletedAt)}"))
            : null;

    partial void OnIsRescanningFolderChanged(bool value) =>
        RescanFolderCommand.NotifyCanExecuteChanged();

    private bool CanRescanFolder() =>
        !IsUpdateRestartPending
        && !IsScanning
        && !IsExporting
        && !IsMovingToTrash
        && !IsRunningCleanupBasketOperation
        && _scanRoot is not null
        && _resultScanOptions is not null
        && ScanCompletedAt is not null
        && SelectedItem is { IsDirectory: true };

    [RelayCommand(CanExecute = nameof(CanRescanFolder))]
    private async Task RescanFolderAsync()
    {
        if (!CanRescanFolder()
            || _scanRoot is not { } root
            || _resultScanOptions is not { } options
            || SelectedItem is not { } selected)
        {
            return;
        }

        if (ScanResultMerger.FindByPath(root, selected.Path) is not { IsDirectory: true } folder)
        {
            return;
        }

        FolderRescanStatusMessage = null;

        if (ReferenceEquals(folder, root))
        {
            await RunScanAsync(root.Path, options, addRecentLocation: false);
            return;
        }

        if (FolderRescanLimitation(options) is { } explanation)
        {
            if (!await _folderRescanConfirmationService.ConfirmFullRescanAsync(
                    folder,
                    root.Path,
                    explanation)
                || IsUpdateRestartPending)
            {
                FolderRescanStatusMessage =
                    $"{explanation} “{DisplayNameConverter.Display(folder.Name)}” was not rescanned.";
                return;
            }

            await RunScanAsync(root.Path, options, addRecentLocation: false);
            return;
        }

        await RunFolderRescanAsync(root, folder, options);
    }

    private static string? FolderRescanLimitation(ScanOptions options)
    {
        if (options.MeasurementMode == StorageMeasurementMode.SharedAwareAllocated)
        {
            return SharedAwareFolderRescanExplanation;
        }

        return options.FollowSymbolicLinks
            ? FollowedSymbolicLinkFolderRescanExplanation
            : null;
    }

    private async Task RunFolderRescanAsync(DiskItem root, DiskItem folder, ScanOptions options)
    {
        CancelDuplicateAnalysis();
        CancelScanHistoryCapture();

        var cancellation = new CancellationTokenSource();
        _scanCancellation = cancellation;
        var folderName = DisplayNameConverter.Display(folder.Name);
        var folderPath = folder.Path;
        var previousCurrentPath = CurrentPath;

        await _uiDispatcher.InvokeAsync(() =>
        {
            IsRescanningFolder = true;
            IsScanning = true;
            FolderRescanStatusMessage = null;
            FolderRescanProgressMessage = $"Rescanning “{folderName}”…";
            CurrentPath = folderPath;
        });

        ScanProgress? completed = null;
        FullDiskAccessAssessment? accessAssessment = null;
        var merged = false;

        try
        {
            await Task.Run(async () =>
            {
                await foreach (var progress in _diskScanner
                                   .ScanSubtreeAsync(root.Path, folderPath, options, cancellation.Token)
                                   .ConfigureAwait(false))
                {
                    if (progress.IsCompleted)
                    {
                        completed = progress;
                        continue;
                    }

                    await _uiDispatcher.InvokeAsync(
                            () => ApplyFolderRescanProgress(progress, folderName))
                        .ConfigureAwait(false);
                }

                cancellation.Token.ThrowIfCancellationRequested();
                if (completed is not null)
                {
                    accessAssessment = CheckFullDiskAccess(root.Path);
                }
            }).ConfigureAwait(false);

            if (completed is { } result)
            {
                var applied = false;
                while (!applied)
                {
                    await WaitForResultReadersAsync().ConfigureAwait(false);
                    cancellation.Token.ThrowIfCancellationRequested();
                    await _uiDispatcher.InvokeAsync(() =>
                        {
                            if (HasActiveResultReaders())
                            {
                                return;
                            }

                            applied = true;
                            merged = ApplyFolderRescan(
                                root,
                                folderPath,
                                folderName,
                                result,
                                options,
                                accessAssessment ?? FullDiskAccessAssessment.Indeterminate);
                        })
                        .ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException)
        {
            await _uiDispatcher.InvokeAsync(() => FolderRescanStatusMessage =
                    $"The rescan of “{folderName}” was stopped. The result was not changed.")
                .ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            await _uiDispatcher.InvokeAsync(() => FolderRescanStatusMessage =
                    $"The rescan of “{folderName}” stopped unexpectedly and the result was not "
                    + $"changed. {exception.Message}")
                .ConfigureAwait(false);
        }
        finally
        {
            if (merged)
            {
                Interlocked.Increment(ref _pendingScanFinalizations);
            }

            _scanCancellation = null;
            cancellation.Dispose();
            await _uiDispatcher.InvokeAsync(() =>
            {
                if (!merged)
                {
                    CurrentPath = previousCurrentPath;
                }

                FolderRescanProgressMessage = null;
                IsScanning = false;
                IsRescanningFolder = false;
            });
        }

        if (!merged)
        {
            return;
        }

        try
        {
            if (ResultProvenance is { } provenance && _scanRoot is { } mergedRoot)
            {
                await StartScanHistoryCaptureAsync(
                        MergedResultProgress(mergedRoot),
                        options,
                        provenance)
                    .ConfigureAwait(false);
            }
        }
        finally
        {
            Interlocked.Decrement(ref _pendingScanFinalizations);
        }
    }

    private void ApplyFolderRescanProgress(ScanProgress progress, string folderName)
    {
        CurrentPath = progress.CurrentPath;
        var files = progress.FilesScanned.ToString("N0", CultureInfo.CurrentCulture);
        var folders = progress.DirectoriesScanned.ToString("N0", CultureInfo.CurrentCulture);
        FolderRescanProgressMessage =
            $"Rescanning “{folderName}”… {files} files, {folders} folders";
    }

    private async Task WaitForResultReadersAsync()
    {
        var pendingPreparation = Task.CompletedTask;
        var pendingInsightPreparation = Task.CompletedTask;
        await _uiDispatcher.InvokeAsync(() =>
        {
            pendingPreparation = TreePreparation;
            pendingInsightPreparation = InsightPreparation;
            CancelTreePreparation();
            CancelInsightPreparation();
        }).ConfigureAwait(false);

        foreach (var reader in new[]
                 {
                     pendingPreparation,
                     pendingInsightPreparation,
                     _scanHistoryCapture,
                     _duplicateAnalysis
                 })
        {
            try
            {
                await reader.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }
    }

    private bool HasActiveResultReaders() =>
        !TreePreparation.IsCompleted
        || Volatile.Read(ref _activeTreePreparations) > 0
        || !InsightPreparation.IsCompleted
        || Volatile.Read(ref _activeInsightPreparations) > 0
        || !_scanHistoryCapture.IsCompleted
        || !_duplicateAnalysis.IsCompleted;

    private bool ApplyFolderRescan(
        DiskItem root,
        string folderPath,
        string folderName,
        ScanProgress completed,
        ScanOptions options,
        FullDiskAccessAssessment accessAssessment)
    {
        if (!ReferenceEquals(_scanRoot, root)
            || ScanResultMerger.FindByPath(root, folderPath) is not { } staleFolder)
        {
            FolderRescanStatusMessage =
                $"“{folderName}” is no longer part of the result and was not rescanned.";
            return false;
        }

        var expandedPaths = CollectExpandedPaths(TreeItems);
        AddAncestorPaths(expandedPaths, root.Path, folderPath);
        var replacement = completed.IsRootPresent ? completed.Root : null;
        var staleCounts = CountEntries(staleFolder);
        var refreshedCounts = replacement is null ? (Files: 0L, Directories: 0L) : CountEntries(replacement);
        var sizeDelta = (replacement?.SizeBytes ?? 0) - staleFolder.SizeBytes;

        ScanResultMerger.ReplaceSubtree(root, folderPath, replacement);

        var errors = ScanResultMerger.MergeErrors(ScanErrors, folderPath, completed.Errors);
        ScanErrors = errors;
        SelectedScanError = null;
        OmittedEquivalentPaths = ScanResultMerger.MergeOmittedEquivalentPaths(
            OmittedEquivalentPaths,
            folderPath,
            completed.OmittedEquivalentPaths);
        FilesScanned = Math.Max(0, FilesScanned - staleCounts.Files + refreshedCounts.Files);
        DirectoriesScanned = Math.Max(
            0,
            DirectoriesScanned - staleCounts.Directories + refreshedCounts.Directories);
        BytesScanned = Math.Max(0, BytesScanned + sizeDelta);
        CurrentPath = root.Path;
        ResultProvenance = (ResultProvenance
                ?? ScanResultProvenance.ForFullScan(ScanCompletedAt ?? _referenceTimeProvider()))
            .WithRefreshedFolder(folderPath, _referenceTimeProvider());

        _cleanupProtectedPathPolicy = new CleanupProtectedPathPolicy(root);
        var removedBasketItems = _cleanupBasketPlanner?.Rebind(
            root,
            folderPath,
            _cleanupProtectedPathPolicy) ?? 0;
        RefreshCleanupBasketState();
        if (removedBasketItems > 0)
        {
            CleanupBasketStatusMessage = removedBasketItems == 1
                ? "Removed 1 cleanup basket item that no longer exists after the folder rescan."
                : $"Removed {removedBasketItems.ToString(CultureInfo.CurrentCulture)} cleanup basket items that no longer exist after the folder rescan.";
        }

        var hadDuplicateResults = DuplicateGroups.Count > 0
            || DuplicateSkippedCandidates.Count > 0
            || DuplicateAnalysisStatusMessage is not null;
        ClearDuplicateAnalysisForResultReplacement();
        if (hadDuplicateResults)
        {
            DuplicateAnalysisStatusMessage =
                "Duplicate results were cleared because a folder was rescanned. "
                + "Run duplicate analysis again to refresh them.";
        }

        AccessGuidance = _accessGuidanceClassifier.Classify(errors, accessAssessment);

        _treemapLayoutCache.Clear();
        SelectedTreeItem = null;
        SelectedTreemapRectangle = null;
        SelectedLargeFile = null;
        _pendingTreeExpansion = expandedPaths;
        ApplySearch();
        _pendingTreeExpansion = null;

        var focus = replacement
            ?? ScanResultMerger.FindByPath(root, Path.GetDirectoryName(folderPath) ?? root.Path)
            ?? root;
        if (FindNode(TreeItems, focus) is { } node)
        {
            SelectedTreeItem = node;
        }
        else
        {
            TreemapRectangles = LayoutChildren(focus);
        }

        FolderRescanStatusMessage = FolderRescanCompletionMessage(
            folderName,
            replacement is not null,
            options);
        return true;
    }

    private string FolderRescanCompletionMessage(
        string folderName,
        bool folderExists,
        ScanOptions resultOptions)
    {
        if (!folderExists)
        {
            return $"“{folderName}” no longer exists and was removed from the result.";
        }

        var message = $"Rescanned “{folderName}” with the scan options of the displayed result.";
        return CurrentToolbarOptions() == resultOptions
            ? message
            : $"{message} The current toolbar options apply to the next full scan.";
    }

    private ScanOptions CurrentToolbarOptions() =>
        new()
        {
            TreatPackagesAsDirectories = ExpandApplicationBundles,
            IncludeHiddenFiles = IncludeHiddenFiles,
            FollowSymbolicLinks = FollowSymbolicLinks,
            MeasurementMode = MeasurementMode
        };

    private ScanProgress MergedResultProgress(DiskItem root) =>
        new(
            root.Path,
            FilesScanned,
            DirectoriesScanned,
            BytesScanned,
            root,
            ScanErrors,
            IsCompleted: true,
            ResultMeasurementMode,
            ResultCloneAccountingCoverage)
        {
            OmittedEquivalentPaths = OmittedEquivalentPaths
        };

    private static (long Files, long Directories) CountEntries(DiskItem item)
    {
        long files = 0;
        long directories = 0;
        var pending = new Stack<DiskItem>();
        pending.Push(item);
        while (pending.Count > 0)
        {
            var current = pending.Pop();
            if (!current.IsDirectory)
            {
                files++;
                continue;
            }

            directories++;
            foreach (var child in current.Children)
            {
                pending.Push(child);
            }
        }

        return (files, directories);
    }

    private static HashSet<string> CollectExpandedPaths(
        IEnumerable<DiskItemTreeNodeViewModel> nodes)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<DiskItemTreeNodeViewModel>(nodes);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (!node.IsExpanded)
            {
                continue;
            }

            paths.Add(node.Item.Path);
            if (!node.HasMaterializedChildren)
            {
                continue;
            }

            foreach (var child in node.Children)
            {
                pending.Push(child);
            }
        }

        return paths;
    }

    private static void AddAncestorPaths(HashSet<string> paths, string rootPath, string folderPath)
    {
        paths.Add(rootPath);
        var ancestor = Path.GetDirectoryName(folderPath);
        while (ancestor is not null && IsUnder(ancestor, rootPath))
        {
            paths.Add(ancestor);
            ancestor = Path.GetDirectoryName(ancestor);
        }
    }

    private static void RestoreTreeExpansion(
        IEnumerable<DiskItemTreeNodeViewModel> nodes,
        IReadOnlySet<string> expandedPaths)
    {
        var pending = new Stack<DiskItemTreeNodeViewModel>(nodes);
        while (pending.Count > 0)
        {
            var node = pending.Pop();
            if (expandedPaths.Contains(node.Item.Path))
            {
                node.IsExpanded = true;
            }

            if (!node.IsExpanded)
            {
                continue;
            }

            foreach (var child in node.Children)
            {
                pending.Push(child);
            }
        }
    }

    private static string FormatProvenanceTime(DateTimeOffset value) =>
        value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
}
