using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.ViewModels;

public sealed partial class DateBoundViewModel : ObservableObject
{
    private readonly Func<DateTimeOffset> _referenceTimeProvider;
    private readonly Action _criterionChanged;
    private bool _suppressNotification;

    private static readonly IReadOnlyList<RelativeDateUnit> Units =
        Enum.GetValues<RelativeDateUnit>();

    internal DateBoundViewModel(
        string displayName,
        string boundLabel,
        Func<DateTimeOffset> referenceTimeProvider,
        Action criterionChanged)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(boundLabel);
        ArgumentNullException.ThrowIfNull(referenceTimeProvider);
        ArgumentNullException.ThrowIfNull(criterionChanged);

        DisplayName = displayName;
        BoundLabel = boundLabel;
        _referenceTimeProvider = referenceTimeProvider;
        _criterionChanged = criterionChanged;
    }

    public IReadOnlyList<RelativeDateUnit> UnitOptions => Units;

    public string DisplayName { get; }

    public string BoundLabel { get; }

    public string RelativeToggleName => $"Use a relative bound for {DisplayName}";

    public string CountName => $"{DisplayName} relative count";

    public string UnitName => $"{DisplayName} relative unit";

    [ObservableProperty]
    private bool _isRelative;

    [ObservableProperty]
    private DateTimeOffset? _instant;

    [ObservableProperty]
    private int? _count;

    [ObservableProperty]
    private RelativeDateUnit _unit = RelativeDateUnit.Days;

    public bool IsAbsolute => !IsRelative;

    public decimal? CountValue
    {
        get => Count;
        set => Count = value is { } number
            ? (int)Math.Clamp(Math.Round(number), int.MinValue, int.MaxValue)
            : null;
    }

    public DateCriterion? Criterion =>
        IsRelative
            ? Count is { } count ? new RelativeDateCriterion(count, Unit) : null
            : Instant is { } instant ? new AbsoluteDateCriterion(instant) : null;

    public bool HasResolvedDescription => ResolvedDescription is not null;

    public string? ResolvedDescription =>
        Criterion is RelativeDateCriterion relative
        && relative.Validate().IsValid
            ? $"{FormatOffset(relative)} · resolves to "
                + $"{relative.Resolve(_referenceTimeProvider()):yyyy-MM-dd HH:mm}"
            : null;

    internal void RefreshResolvedDescription()
    {
        OnPropertyChanged(nameof(ResolvedDescription));
        OnPropertyChanged(nameof(HasResolvedDescription));
    }

    internal void SetCriterionSilently(DateCriterion? criterion)
    {
        _suppressNotification = true;
        try
        {
            switch (criterion)
            {
                case AbsoluteDateCriterion absolute:
                    IsRelative = false;
                    Instant = absolute.Instant;
                    Count = null;
                    break;
                case RelativeDateCriterion relative:
                    IsRelative = true;
                    Instant = null;
                    Count = relative.Count;
                    Unit = relative.Unit;
                    break;
                default:
                    IsRelative = false;
                    Instant = null;
                    Count = null;
                    break;
            }
        }
        finally
        {
            _suppressNotification = false;
        }

        RaiseCriterionChanged(notifyOwner: false);
    }

    private static string FormatOffset(RelativeDateCriterion criterion)
    {
        var name = criterion.Unit switch
        {
            RelativeDateUnit.Days => "day",
            RelativeDateUnit.Weeks => "week",
            RelativeDateUnit.Months => "month",
            RelativeDateUnit.Years => "year",
            _ => throw new ArgumentOutOfRangeException(
                nameof(criterion),
                criterion.Unit,
                null)
        };

        var unit = criterion.Count == 1 ? name : $"{name}s";
        return $"{criterion.Count} {unit} before now";
    }

    partial void OnIsRelativeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsAbsolute));
        RaiseCriterionChanged(notifyOwner: !_suppressNotification);
    }

    partial void OnInstantChanged(DateTimeOffset? value) =>
        RaiseCriterionChanged(notifyOwner: !_suppressNotification);

    partial void OnCountChanged(int? value)
    {
        OnPropertyChanged(nameof(CountValue));
        RaiseCriterionChanged(notifyOwner: !_suppressNotification);
    }

    partial void OnUnitChanged(RelativeDateUnit value) =>
        RaiseCriterionChanged(notifyOwner: !_suppressNotification);

    private void RaiseCriterionChanged(bool notifyOwner)
    {
        OnPropertyChanged(nameof(Criterion));
        OnPropertyChanged(nameof(ResolvedDescription));
        OnPropertyChanged(nameof(HasResolvedDescription));

        if (notifyOwner)
        {
            _criterionChanged();
        }
    }
}
