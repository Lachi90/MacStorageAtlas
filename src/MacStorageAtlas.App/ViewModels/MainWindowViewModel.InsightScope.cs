using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacStorageAtlas.App.Converters;
using MacStorageAtlas.Core.Filtering;
using MacStorageAtlas.Core.Insights;
using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.App.ViewModels;

public partial class MainWindowViewModel
{
    private CancellationTokenSource? _insightPreparationCancellation;
    private int _activeInsightPreparations;
    private bool _areInsightsPrepared;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInsightScope))]
    [NotifyPropertyChangedFor(nameof(IsInsightScopeNarrowed))]
    [NotifyPropertyChangedFor(nameof(InsightScopeCaption))]
    [NotifyPropertyChangedFor(nameof(InsightScopePath))]
    [NotifyPropertyChangedFor(nameof(InsightScopeEmptyMessage))]
    [NotifyCanExecuteChangedFor(nameof(ResetInsightScopeCommand))]
    private DiskItem? _insightScope;

    internal Task InsightPreparation { get; private set; } = Task.CompletedTask;

    public bool HasInsightScope => InsightScope is not null;

    public bool IsInsightScopeNarrowed =>
        InsightScope is { } scope
        && _scanRoot is { } root
        && !ReferenceEquals(scope, root);

    public string InsightScopeCaption =>
        IsInsightScopeNarrowed && InsightScope is { } scope
            ? $"Showing files in “{DisplayNameConverter.Display(scope.Name)}”"
            : "Showing the whole scan";

    public string InsightScopePath => InsightScope?.Path ?? string.Empty;

    public string InsightScopeEmptyMessage
    {
        get
        {
            var files = IsFilterActive ? "No matching files" : "No files";
            return IsInsightScopeNarrowed && InsightScope is { } scope
                ? $"{files} in “{DisplayNameConverter.Display(scope.Name)}”."
                : $"{files} in this scan.";
        }
    }

    public bool ShowLargeFilesEmptyMessage => HasInsightScope && LargeFiles.Count == 0;

    public bool ShowFileTypesEmptyMessage => HasInsightScope && FileTypeSummaries.Count == 0;

    partial void OnLargeFilesChanged(IReadOnlyList<DiskItem> value) =>
        OnPropertyChanged(nameof(ShowLargeFilesEmptyMessage));

    partial void OnFileTypeSummariesChanged(IReadOnlyList<FileTypeSummary> value) =>
        OnPropertyChanged(nameof(ShowFileTypesEmptyMessage));

    partial void OnInsightScopeChanged(DiskItem? value)
    {
        OnPropertyChanged(nameof(ShowLargeFilesEmptyMessage));
        OnPropertyChanged(nameof(ShowFileTypesEmptyMessage));
    }

    [RelayCommand(CanExecute = nameof(IsInsightScopeNarrowed))]
    private void ResetInsightScope()
    {
        if (_scanRoot is { } root)
        {
            ChangeInsightScope(root);
        }
    }

    private void ChangeInsightScope(DiskItem? scope)
    {
        if (scope is null || ReferenceEquals(scope, InsightScope))
        {
            return;
        }

        InsightScope = scope;
        ScheduleInsightPreparation();
    }

    private void ClearInsightScope()
    {
        CancelInsightPreparation();
        _areInsightsPrepared = false;
        InsightScope = null;
    }

    private DiskItem? InsightScopeFor(DiskItem item)
    {
        if (_scanRoot is not { } root)
        {
            return null;
        }

        if (item.IsDirectory)
        {
            return item;
        }

        return FindContainingDirectory(root, item) ?? root;
    }

    private static DiskItem? FindContainingDirectory(DiskItem root, DiskItem item)
    {
        var current = root;
        while (true)
        {
            DiskItem? next = null;
            foreach (var child in current.Children)
            {
                if (ReferenceEquals(child, item))
                {
                    return current;
                }

                if (next is null && child.IsDirectory && IsUnder(item.Path, child.Path))
                {
                    next = child;
                }
            }

            if (next is null)
            {
                return FindParent(root, item);
            }

            current = next;
        }
    }

    private DiskItem? ResolveInsightScope(string? path)
    {
        if (_scanRoot is not { } root)
        {
            return null;
        }

        if (path is null || !IsUnder(path, root.Path))
        {
            return root;
        }

        var current = root;
        while (true)
        {
            DiskItem? next = null;
            foreach (var child in current.Children)
            {
                if (!child.IsDirectory)
                {
                    continue;
                }

                if (string.Equals(child.Path, path, StringComparison.Ordinal))
                {
                    return child;
                }

                if (IsUnder(path, child.Path))
                {
                    next = child;
                    break;
                }
            }

            if (next is null)
            {
                return current;
            }

            current = next;
        }
    }

    private void CancelInsightPreparation()
    {
        _insightPreparationCancellation?.Cancel();
        _insightPreparationCancellation = null;
        InsightPreparation = Task.CompletedTask;
    }

    private void ScheduleInsightPreparation()
    {
        if (!_areInsightsPrepared || InsightScope is not { } scope)
        {
            CancelInsightPreparation();
            return;
        }

        var previous = _insightPreparationCancellation;
        var cancellation = new CancellationTokenSource();
        _insightPreparationCancellation = cancellation;
        previous?.Cancel();

        InsightPreparation = PrepareInsightsAsync(scope, _filterResult, cancellation);
    }

    private async Task PrepareInsightsAsync(
        DiskItem scope,
        FilterResult? filterResult,
        CancellationTokenSource cancellation)
    {
        var token = cancellation.Token;

        try
        {
            var prepared = await Task.Run(
                    () =>
                    {
                        Interlocked.Increment(ref _activeInsightPreparations);
                        try
                        {
                            return PrepareInsights(scope, filterResult, token);
                        }
                        finally
                        {
                            Interlocked.Decrement(ref _activeInsightPreparations);
                        }
                    },
                    token)
                .ConfigureAwait(false);

            token.ThrowIfCancellationRequested();

            await _uiDispatcher.InvokeAsync(() =>
            {
                if (!ReferenceEquals(_insightPreparationCancellation, cancellation)
                    || !ReferenceEquals(InsightScope, prepared.Scope)
                    || !ReferenceEquals(_filterResult, filterResult))
                {
                    return;
                }

                ApplyInsightValues(prepared);
                ReconcileLargeFileSelection();
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            await _uiDispatcher.InvokeAsync(() =>
            {
                if (ReferenceEquals(_insightPreparationCancellation, cancellation))
                {
                    _insightPreparationCancellation = null;
                }

                cancellation.Dispose();
            }).ConfigureAwait(false);
        }
    }

    private PreparedInsights PrepareInsights(
        DiskItem scope,
        FilterResult? filterResult,
        CancellationToken cancellationToken)
    {
        Func<DiskItem, bool>? include = filterResult is { IsFilterActive: true } result
            ? item => item.IsDirectory ? result.IsVisible(item) : result.Matches(item)
            : null;

        return new PreparedInsights(
            scope,
            _largeFilesService.GetLargestFiles(
                scope,
                include,
                LargeFilesService.DefaultLimit,
                cancellationToken),
            _fileTypeStatisticsService.Calculate(scope, include, cancellationToken));
    }

    private void ApplyInsightValues(PreparedInsights prepared)
    {
        LargeFiles = prepared.LargeFiles;
        FileTypeSummaries = prepared.FileTypeSummaries;
    }

    private sealed record PreparedInsights(
        DiskItem Scope,
        IReadOnlyList<DiskItem> LargeFiles,
        IReadOnlyList<FileTypeSummary> FileTypeSummaries);
}
