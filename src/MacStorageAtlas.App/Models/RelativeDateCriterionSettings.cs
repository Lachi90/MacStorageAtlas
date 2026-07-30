using System;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.Models;

public sealed class RelativeDateCriterionSettings
{
    public int Count { get; set; }

    public RelativeDateUnit Unit { get; set; }

    public static RelativeDateCriterionSettings FromCriterion(
        RelativeDateCriterion criterion)
    {
        ArgumentNullException.ThrowIfNull(criterion);

        return new RelativeDateCriterionSettings
        {
            Count = criterion.Count,
            Unit = criterion.Unit
        };
    }

    public RelativeDateCriterion? TryCreateCriterion() =>
        Count > 0 && Enum.IsDefined(Unit)
            ? new RelativeDateCriterion(Count, Unit)
            : null;
}
