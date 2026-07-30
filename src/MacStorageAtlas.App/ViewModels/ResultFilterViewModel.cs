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
        RefreshPresets();
    }

    public event EventHandler? CriteriaChanged;

    public event EventHandler? UserPresetsChanged;

    public IReadOnlyList<FileCategoryOption> CategoryOptions { get; }

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
    private DateTimeOffset? _createdAfter;

    [ObservableProperty]
    private DateTimeOffset? _createdBefore;

    [ObservableProperty]
    private DateTimeOffset? _modifiedAfter;

    [ObservableProperty]
    private DateTimeOffset? _modifiedBefore;

    [ObservableProperty]
    private DateTimeOffset? _lastAccessedAfter;

    [ObservableProperty]
    private DateTimeOffset? _lastAccessedBefore;

    [ObservableProperty]
    private string _extensionsText = string.Empty;

    [ObservableProperty]
    private bool _sharedStorageOnly;

    [ObservableProperty]
    private IReadOnlyList<FilterPreset> _presets = [];

    [ObservableProperty]
    private string _newPresetName = string.Empty;

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

    public string FormattedMatchedBytes => FileSizeFormatter.Format(MatchedBytes);

    public DiskItemFilter CurrentFilter => new()
    {
        TextTerm = string.IsNullOrWhiteSpace(TextTerm) ? null : TextTerm,
        MinimumSizeBytes = MinimumSizeBytes,
        MaximumSizeBytes = MaximumSizeBytes,
        CreatedAfter = CreatedAfter,
        CreatedBefore = CreatedBefore,
        ModifiedAfter = ModifiedAfter,
        ModifiedBefore = ModifiedBefore,
        LastAccessedAfter = LastAccessedAfter,
        LastAccessedBefore = LastAccessedBefore,
        Extensions = ParseExtensions(ExtensionsText),
        Categories = [.. SelectedCategories],
        SharedStorageOnly = SharedStorageOnly
    };

    public DiskItemFilterValidation Validation => CurrentFilter.Validate();

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

    internal void ApplyMatchSummary(FilterResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        MatchCount = result.MatchCount;
        MatchedBytes = result.MatchedBytes;
        UnknownDateExclusionCount = result.UnknownDateExclusionCount;
        OnPropertyChanged(nameof(MatchSummary));
        OnPropertyChanged(nameof(HasNoMatches));
    }

    [RelayCommand]
    public void ApplyPreset(FilterPreset? preset)
    {
        if (preset is null)
        {
            return;
        }

        _isApplyingPreset = true;
        try
        {
            var filter = preset.Filter;
            TextTerm = filter.TextTerm ?? string.Empty;
            MinimumSizeBytes = filter.MinimumSizeBytes;
            MaximumSizeBytes = filter.MaximumSizeBytes;
            CreatedAfter = filter.CreatedAfter;
            CreatedBefore = filter.CreatedBefore;
            ModifiedAfter = filter.ModifiedAfter;
            ModifiedBefore = filter.ModifiedBefore;
            LastAccessedAfter = filter.LastAccessedAfter;
            LastAccessedBefore = filter.LastAccessedBefore;
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

        RaiseCriteriaChanged();
    }

    [RelayCommand]
    public void ClearFilter()
    {
        ApplyPreset(new FilterPreset(string.Empty, DiskItemFilter.Empty));
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
        RefreshPresets();
        UserPresetsChanged?.Invoke(this, EventArgs.Empty);
    }

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
        RefreshPresets();
        UserPresetsChanged?.Invoke(this, EventArgs.Empty);
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

    private void RefreshPresets() =>
        Presets =
        [
            .. BuiltInFilterPresets.Create(_referenceTimeProvider()),
            .. _userPresets
        ];

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

    partial void OnCreatedAfterChanged(DateTimeOffset? value) => RaiseCriteriaChanged();

    partial void OnCreatedBeforeChanged(DateTimeOffset? value) => RaiseCriteriaChanged();

    partial void OnModifiedAfterChanged(DateTimeOffset? value) => RaiseCriteriaChanged();

    partial void OnModifiedBeforeChanged(DateTimeOffset? value) => RaiseCriteriaChanged();

    partial void OnLastAccessedAfterChanged(DateTimeOffset? value) => RaiseCriteriaChanged();

    partial void OnLastAccessedBeforeChanged(DateTimeOffset? value) => RaiseCriteriaChanged();

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

        if (!_isApplyingPreset)
        {
            CriteriaChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
