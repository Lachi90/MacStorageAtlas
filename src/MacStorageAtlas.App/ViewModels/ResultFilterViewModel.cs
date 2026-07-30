using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.ViewModels;

public partial class ResultFilterViewModel : ViewModelBase
{
    private readonly Func<DateTimeOffset> _referenceTimeProvider;
    private readonly List<FilterPreset> _userPresets = [];
    private bool _isApplyingPreset;
    private string? _appliedPresetName;

    public ResultFilterViewModel()
        : this(() => DateTimeOffset.Now)
    {
    }

    public ResultFilterViewModel(Func<DateTimeOffset> referenceTimeProvider)
    {
        ArgumentNullException.ThrowIfNull(referenceTimeProvider);

        _referenceTimeProvider = referenceTimeProvider;
        CategoryOptions = Enum.GetValues<FileCategory>()
            .Select(category => new FileCategoryOption(category, RaiseCriteriaChanged))
            .ToArray();
        CreatedAfter = CreateBound("Created after", "After");
        CreatedBefore = CreateBound("Created before", "Before");
        ModifiedAfter = CreateBound("Modified after", "After");
        ModifiedBefore = CreateBound("Modified before", "Before");
        LastAccessedAfter = CreateBound("Last accessed after", "After");
        LastAccessedBefore = CreateBound("Last accessed before", "Before");
        RefreshPresets();
    }

    public event EventHandler? CriteriaChanged;

    public event EventHandler? UserPresetsChanged;

    public IReadOnlyList<FileCategoryOption> CategoryOptions { get; }

    public DateBoundViewModel CreatedAfter { get; }

    public DateBoundViewModel CreatedBefore { get; }

    public DateBoundViewModel ModifiedAfter { get; }

    public DateBoundViewModel ModifiedBefore { get; }

    public DateBoundViewModel LastAccessedAfter { get; }

    public DateBoundViewModel LastAccessedBefore { get; }

    public IReadOnlyList<FileCategory> SelectedCategories =>
        CategoryOptions
            .Where(option => option.IsSelected)
            .Select(option => option.Category)
            .ToArray();

    public double? MinimumSizeMegabytes
    {
        get => ToMegabytes(MinimumSizeBytes);
        set => MinimumSizeBytes = ToBytes(value);
    }

    public double? MaximumSizeMegabytes
    {
        get => ToMegabytes(MaximumSizeBytes);
        set => MaximumSizeBytes = ToBytes(value);
    }

    private static double? ToMegabytes(long? bytes) =>
        bytes is { } value ? value / (double)(1024 * 1024) : null;

    private static long? ToBytes(double? megabytes) =>
        megabytes is { } value && value >= 0
            ? (long)Math.Round(value * 1024 * 1024)
            : null;

    [ObservableProperty]
    private string _textTerm = string.Empty;

    [ObservableProperty]
    private long? _minimumSizeBytes;

    [ObservableProperty]
    private long? _maximumSizeBytes;

    [ObservableProperty]
    private string _extensionsText = string.Empty;

    [ObservableProperty]
    private bool _sharedStorageOnly;

    [ObservableProperty]
    private IReadOnlyList<FilterPreset> _presets = [];

    [ObservableProperty]
    private string _newPresetName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRenamingPreset))]
    private FilterPreset? _renamingPreset;

    [ObservableProperty]
    private string _renamePresetName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMatchSummary))]
    private int _matchCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FormattedMatchedBytes))]
    private long _matchedBytes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnknownDateExclusions))]
    [NotifyPropertyChangedFor(nameof(UnknownDateExclusionMessage))]
    private long _unknownDateExclusionCount;

    [ObservableProperty]
    private DateTimeOffset? _lastEvaluatedReferenceTime;

    public DateTimeOffset EffectiveReferenceTime =>
        LastEvaluatedReferenceTime ?? _referenceTimeProvider();

    public string FormattedMatchedBytes => FileSizeFormatter.Format(MatchedBytes);

    public DiskItemFilter CurrentFilter => new()
    {
        TextTerm = string.IsNullOrWhiteSpace(TextTerm) ? null : TextTerm,
        MinimumSizeBytes = MinimumSizeBytes,
        MaximumSizeBytes = MaximumSizeBytes,
        CreatedAfter = CreatedAfter.Criterion,
        CreatedBefore = CreatedBefore.Criterion,
        ModifiedAfter = ModifiedAfter.Criterion,
        ModifiedBefore = ModifiedBefore.Criterion,
        LastAccessedAfter = LastAccessedAfter.Criterion,
        LastAccessedBefore = LastAccessedBefore.Criterion,
        Extensions = ParseExtensions(ExtensionsText),
        Categories = [.. SelectedCategories],
        SharedStorageOnly = SharedStorageOnly
    };

    public DiskItemFilterValidation Validation =>
        CurrentFilter.Validate(EffectiveReferenceTime);

    public bool IsFilterActive => CurrentFilter.IsActive;

    public bool IsFilterValid => Validation.IsValid;

    public string? ValidationMessage => Validation.Message;

    public bool HasValidationError => !IsFilterValid;

    public bool HasMatchSummary => IsFilterActive && IsFilterValid;

    public bool HasNoMatches => HasMatchSummary && MatchCount == 0;

    public bool HasUnknownDateExclusions => UnknownDateExclusionCount > 0;

    public string UnknownDateExclusionMessage =>
        UnknownDateExclusionCount == 1
            ? "1 file excluded: required date unknown."
            : $"{UnknownDateExclusionCount} files excluded: required date unknown.";

    public string MatchSummary =>
        $"{MatchCount:N0} files · {FormattedMatchedBytes} matched";

    public IReadOnlyList<FilterPreset> UserPresets => _userPresets;

    public FilterPreset? AppliedPreset
    {
        get
        {
            var filter = CurrentFilter;
            return Presets.FirstOrDefault(preset => preset.Filter == filter);
        }
    }

    public string? AppliedPresetName => AppliedPreset?.Name;

    public bool HasAppliedPreset => AppliedPreset is not null;

    public FilterPreset? EditedFromPreset =>
        _appliedPresetName is null
            ? null
            : Presets.FirstOrDefault(
                preset => string.Equals(
                    preset.Name,
                    _appliedPresetName,
                    StringComparison.OrdinalIgnoreCase));

    public bool HasEditedCriteria =>
        AppliedPreset is null && EditedFromPreset is not null;

    public string? EditedFromPresetName => HasEditedCriteria ? EditedFromPreset?.Name : null;

    public string? PresetStateSummary =>
        AppliedPreset is { } applied
            ? $"Criteria match preset “{applied.Name}”."
            : EditedFromPresetName is { } edited
                ? $"Criteria edited from preset “{edited}”."
                : null;

    public bool HasPresetState => PresetStateSummary is not null;

    public bool IsRenamingPreset => RenamingPreset is not null;

    internal void ApplyMatchSummary(FilterResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        MatchCount = result.MatchCount;
        MatchedBytes = result.MatchedBytes;
        UnknownDateExclusionCount = result.UnknownDateExclusionCount;
        LastEvaluatedReferenceTime = result.ReferenceTime;
        OnPropertyChanged(nameof(MatchSummary));
        OnPropertyChanged(nameof(HasNoMatches));
        RefreshResolvedDescriptions();
    }

    [RelayCommand]
    public void ApplyPreset(FilterPreset? preset)
    {
        if (preset is null)
        {
            return;
        }

        ApplyFilterCriteria(preset.Filter);
        _appliedPresetName = preset.Name.Length == 0 ? null : preset.Name;
        RaiseCriteriaChanged();
    }

    [RelayCommand]
    public void ClearFilter()
    {
        ApplyFilterCriteria(DiskItemFilter.Empty);
        _appliedPresetName = null;
        CancelRenamePreset();
        RaiseCriteriaChanged();
    }

    [RelayCommand]
    public void SavePreset()
    {
        var name = NewPresetName.Trim();
        if (name.Length == 0 || !IsFilterValid || !IsFilterActive)
        {
            return;
        }

        var preset = new FilterPreset(name, CurrentFilter);
        var existingIndex = _userPresets.FindIndex(
            existing => string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase));

        if (existingIndex >= 0)
        {
            _userPresets[existingIndex] = preset;
        }
        else
        {
            _userPresets.Add(preset);
        }

        NewPresetName = string.Empty;
        _appliedPresetName = preset.Name;
        RefreshPresets();
        UserPresetsChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand(CanExecute = nameof(CanUpdatePreset))]
    public void UpdatePreset()
    {
        if (EditedFromPreset is not { IsBuiltIn: false } target)
        {
            return;
        }

        var index = _userPresets.FindIndex(
            existing => string.Equals(
                existing.Name,
                target.Name,
                StringComparison.OrdinalIgnoreCase));

        if (index < 0)
        {
            return;
        }

        _userPresets[index] = _userPresets[index] with { Filter = CurrentFilter };
        RefreshPresets();
        UserPresetsChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool CanUpdatePreset =>
        HasEditedCriteria
        && EditedFromPreset is { IsBuiltIn: false }
        && IsFilterValid
        && IsFilterActive;

    [RelayCommand]
    public void DeletePreset(FilterPreset? preset)
    {
        if (preset is null || preset.IsBuiltIn)
        {
            return;
        }

        _userPresets.RemoveAll(
            existing => string.Equals(
                existing.Name,
                preset.Name,
                StringComparison.OrdinalIgnoreCase));

        if (string.Equals(_appliedPresetName, preset.Name, StringComparison.OrdinalIgnoreCase))
        {
            _appliedPresetName = null;
        }

        if (RenamingPreset is { } renaming
            && string.Equals(renaming.Name, preset.Name, StringComparison.OrdinalIgnoreCase))
        {
            CancelRenamePreset();
        }

        RefreshPresets();
        UserPresetsChanged?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    public void BeginRenamePreset(FilterPreset? preset)
    {
        if (preset is null || preset.IsBuiltIn)
        {
            return;
        }

        RenamingPreset = preset;
        RenamePresetName = preset.Name;
    }

    [RelayCommand]
    public void CommitRenamePreset()
    {
        if (RenamingPreset is not { } preset)
        {
            return;
        }

        RenamePreset(preset, RenamePresetName);
        CancelRenamePreset();
    }

    [RelayCommand]
    public void CancelRenamePreset()
    {
        RenamingPreset = null;
        RenamePresetName = string.Empty;
    }

    public void RenamePreset(FilterPreset preset, string newName)
    {
        ArgumentNullException.ThrowIfNull(preset);

        var trimmed = newName?.Trim();
        if (string.IsNullOrEmpty(trimmed) || preset.IsBuiltIn)
        {
            return;
        }

        var index = _userPresets.FindIndex(
            existing => string.Equals(
                existing.Name,
                preset.Name,
                StringComparison.OrdinalIgnoreCase));

        if (index >= 0)
        {
            if (string.Equals(
                _appliedPresetName,
                _userPresets[index].Name,
                StringComparison.OrdinalIgnoreCase))
            {
                _appliedPresetName = trimmed;
            }

            _userPresets[index] = _userPresets[index] with { Name = trimmed };
            RefreshPresets();
            UserPresetsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    internal void LoadUserPresets(IEnumerable<FilterPreset> presets)
    {
        ArgumentNullException.ThrowIfNull(presets);

        _userPresets.Clear();
        _userPresets.AddRange(presets);
        RefreshPresets();
    }

    private DateBoundViewModel CreateBound(string displayName, string boundLabel) =>
        new(displayName, boundLabel, () => EffectiveReferenceTime, RaiseCriteriaChanged);

    private void RefreshResolvedDescriptions()
    {
        foreach (var bound in DateBounds)
        {
            bound.RefreshResolvedDescription();
        }
    }

    private IReadOnlyList<DateBoundViewModel> DateBounds =>
    [
        CreatedAfter,
        CreatedBefore,
        ModifiedAfter,
        ModifiedBefore,
        LastAccessedAfter,
        LastAccessedBefore
    ];

    private void ApplyFilterCriteria(DiskItemFilter filter)
    {
        _isApplyingPreset = true;
        try
        {
            TextTerm = filter.TextTerm ?? string.Empty;
            MinimumSizeBytes = filter.MinimumSizeBytes;
            MaximumSizeBytes = filter.MaximumSizeBytes;
            CreatedAfter.SetCriterionSilently(filter.CreatedAfter);
            CreatedBefore.SetCriterionSilently(filter.CreatedBefore);
            ModifiedAfter.SetCriterionSilently(filter.ModifiedAfter);
            ModifiedBefore.SetCriterionSilently(filter.ModifiedBefore);
            LastAccessedAfter.SetCriterionSilently(filter.LastAccessedAfter);
            LastAccessedBefore.SetCriterionSilently(filter.LastAccessedBefore);
            ExtensionsText = string.Join(", ", filter.Extensions);
            foreach (var option in CategoryOptions)
            {
                option.SetSelectedSilently(filter.Categories.Contains(option.Category));
            }

            SharedStorageOnly = filter.SharedStorageOnly;
        }
        finally
        {
            _isApplyingPreset = false;
        }
    }

    private void RefreshPresets()
    {
        Presets = [.. BuiltInFilterPresets.Create(), .. _userPresets];
        RaisePresetStateChanged();
    }

    private static IReadOnlyList<string> ParseExtensions(string text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text
                .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries
                    | StringSplitOptions.TrimEntries)
                .Select(FileCategoryMap.NormalizeExtension)
                .Where(extension => extension is not null)
                .Cast<string>()
                .Distinct(StringComparer.Ordinal)
                .ToArray();

    partial void OnTextTermChanged(string value) => RaiseCriteriaChanged();

    partial void OnMinimumSizeBytesChanged(long? value)
    {
        OnPropertyChanged(nameof(MinimumSizeMegabytes));
        RaiseCriteriaChanged();
    }

    partial void OnMaximumSizeBytesChanged(long? value)
    {
        OnPropertyChanged(nameof(MaximumSizeMegabytes));
        RaiseCriteriaChanged();
    }

    partial void OnExtensionsTextChanged(string value) => RaiseCriteriaChanged();

    partial void OnSharedStorageOnlyChanged(bool value) => RaiseCriteriaChanged();

    private void RaiseCriteriaChanged()
    {
        OnPropertyChanged(nameof(CurrentFilter));
        OnPropertyChanged(nameof(Validation));
        OnPropertyChanged(nameof(IsFilterActive));
        OnPropertyChanged(nameof(IsFilterValid));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(HasValidationError));
        OnPropertyChanged(nameof(HasMatchSummary));
        OnPropertyChanged(nameof(HasNoMatches));
        RaisePresetStateChanged();

        if (!_isApplyingPreset)
        {
            CriteriaChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RaisePresetStateChanged()
    {
        OnPropertyChanged(nameof(AppliedPreset));
        OnPropertyChanged(nameof(AppliedPresetName));
        OnPropertyChanged(nameof(HasAppliedPreset));
        OnPropertyChanged(nameof(EditedFromPreset));
        OnPropertyChanged(nameof(EditedFromPresetName));
        OnPropertyChanged(nameof(HasEditedCriteria));
        OnPropertyChanged(nameof(PresetStateSummary));
        OnPropertyChanged(nameof(HasPresetState));
        OnPropertyChanged(nameof(CanUpdatePreset));
        UpdatePresetCommand.NotifyCanExecuteChanged();
    }
}
