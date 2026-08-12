namespace MacStorageAtlas.Core.Filtering;

public sealed record RelativeDateCriterion(int Count, RelativeDateUnit Unit) : DateCriterion
{
    public override DateTimeOffset Resolve(DateTimeOffset referenceTime) =>
        Unit switch
        {
            RelativeDateUnit.Days => SubtractDays(referenceTime, Count),
            RelativeDateUnit.Weeks => SubtractDays(referenceTime, (long)Count * 7),
            RelativeDateUnit.Months => SubtractMonths(referenceTime, Count),
            RelativeDateUnit.Years => SubtractYears(referenceTime, Count),
            _ => throw new InvalidOperationException(
                $"Unsupported relative date unit '{Unit}'.")
        };

    public override DiskItemFilterValidation Validate()
    {
        if (Count <= 0)
        {
            return DiskItemFilterValidation.Invalid(
                "A relative date must use a count greater than zero.");
        }

        if (!Enum.IsDefined(Unit))
        {
            return DiskItemFilterValidation.Invalid(
                "A relative date must use a known unit of time.");
        }

        return DiskItemFilterValidation.Valid;
    }

    private static DateTimeOffset SubtractDays(DateTimeOffset referenceTime, long days)
    {
        var available = (referenceTime - DateTimeOffset.MinValue).TotalDays;
        return days >= available
            ? DateTimeOffset.MinValue
            : referenceTime.AddDays(-days);
    }

    private static DateTimeOffset SubtractMonths(DateTimeOffset referenceTime, int months)
    {
        var available = ((referenceTime.Year - 1) * 12) + referenceTime.Month - 1;
        return months > available
            ? DateTimeOffset.MinValue
            : referenceTime.AddMonths(-months);
    }

    private static DateTimeOffset SubtractYears(DateTimeOffset referenceTime, int years)
    {
        var available = referenceTime.Year - 1;
        return years > available
            ? DateTimeOffset.MinValue
            : referenceTime.AddYears(-years);
    }
}
