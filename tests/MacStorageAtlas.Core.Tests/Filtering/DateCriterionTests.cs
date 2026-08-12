using MacStorageAtlas.Core.Filtering;

namespace MacStorageAtlas.Core.Tests.Filtering;

public class DateCriterionTests
{
    private static readonly DateTimeOffset ReferenceTime =
        new(2026, 7, 30, 14, 25, 36, TimeSpan.Zero);

    [Test]
    public void AnAbsoluteCriterionResolvesToItsInstant()
    {
        var instant = new DateTimeOffset(2020, 3, 14, 1, 59, 26, TimeSpan.Zero);
        var criterion = new AbsoluteDateCriterion(instant);

        Assert.That(criterion.Resolve(ReferenceTime), Is.EqualTo(instant));
        Assert.That(
            criterion.Resolve(ReferenceTime.AddYears(5)),
            Is.EqualTo(instant));
    }

    [Test]
    public void AnAbsoluteCriterionIsAlwaysValid()
    {
        var criterion = new AbsoluteDateCriterion(DateTimeOffset.MinValue);

        Assert.That(criterion.Validate().IsValid, Is.True);
    }

    [Test]
    public void ADayOffsetResolvesToThatManyDaysEarlier()
    {
        var criterion = new RelativeDateCriterion(30, RelativeDateUnit.Days);

        Assert.That(
            criterion.Resolve(ReferenceTime),
            Is.EqualTo(ReferenceTime.AddDays(-30)));
    }

    [Test]
    public void AWeekOffsetResolvesToSevenDaysPerWeekEarlier()
    {
        var criterion = new RelativeDateCriterion(3, RelativeDateUnit.Weeks);

        Assert.That(
            criterion.Resolve(ReferenceTime),
            Is.EqualTo(ReferenceTime.AddDays(-21)));
    }

    [Test]
    public void AMonthOffsetResolvesUsingCalendarMonths()
    {
        var criterion = new RelativeDateCriterion(18, RelativeDateUnit.Months);

        Assert.That(
            criterion.Resolve(ReferenceTime),
            Is.EqualTo(new DateTimeOffset(2025, 1, 30, 14, 25, 36, TimeSpan.Zero)));
    }

    [Test]
    public void AYearOffsetResolvesUsingCalendarYears()
    {
        var criterion = new RelativeDateCriterion(1, RelativeDateUnit.Years);

        Assert.That(
            criterion.Resolve(ReferenceTime),
            Is.EqualTo(new DateTimeOffset(2025, 7, 30, 14, 25, 36, TimeSpan.Zero)));
    }

    [Test]
    public void ResolutionPreservesTheReferenceTimeOfDayAndOffset()
    {
        var reference = new DateTimeOffset(2026, 7, 30, 9, 41, 7, TimeSpan.FromHours(2));
        var criterion = new RelativeDateCriterion(2, RelativeDateUnit.Years);

        var resolved = criterion.Resolve(reference);

        Assert.That(resolved.TimeOfDay, Is.EqualTo(reference.TimeOfDay));
        Assert.That(resolved.Offset, Is.EqualTo(reference.Offset));
    }

    [Test]
    public void AMonthOffsetClampsToTheLastDayOfAShorterMonth()
    {
        var reference = new DateTimeOffset(2026, 3, 31, 8, 0, 0, TimeSpan.Zero);
        var criterion = new RelativeDateCriterion(1, RelativeDateUnit.Months);

        Assert.That(
            criterion.Resolve(reference),
            Is.EqualTo(new DateTimeOffset(2026, 2, 28, 8, 0, 0, TimeSpan.Zero)));
    }

    [Test]
    public void AMonthOffsetClampsToTheLeapDayOfAShorterMonth()
    {
        var reference = new DateTimeOffset(2024, 3, 31, 8, 0, 0, TimeSpan.Zero);
        var criterion = new RelativeDateCriterion(1, RelativeDateUnit.Months);

        Assert.That(
            criterion.Resolve(reference),
            Is.EqualTo(new DateTimeOffset(2024, 2, 29, 8, 0, 0, TimeSpan.Zero)));
    }

    [Test]
    public void AYearOffsetFromALeapDayClampsToTheEndOfFebruary()
    {
        var reference = new DateTimeOffset(2024, 2, 29, 8, 0, 0, TimeSpan.Zero);
        var criterion = new RelativeDateCriterion(1, RelativeDateUnit.Years);

        Assert.That(
            criterion.Resolve(reference),
            Is.EqualTo(new DateTimeOffset(2023, 2, 28, 8, 0, 0, TimeSpan.Zero)));
    }

    [TestCase(0)]
    [TestCase(-1)]
    [TestCase(int.MinValue)]
    public void ANonPositiveCountIsInvalid(int count)
    {
        var criterion = new RelativeDateCriterion(count, RelativeDateUnit.Days);

        var validation = criterion.Validate();

        Assert.That(validation.IsValid, Is.False);
        Assert.That(validation.Message, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void AnUndefinedUnitIsInvalid()
    {
        var criterion = new RelativeDateCriterion(1, (RelativeDateUnit)97);

        Assert.That(criterion.Validate().IsValid, Is.False);
    }

    [TestCase(RelativeDateUnit.Days)]
    [TestCase(RelativeDateUnit.Weeks)]
    [TestCase(RelativeDateUnit.Months)]
    [TestCase(RelativeDateUnit.Years)]
    public void APositiveCountIsValidForEveryUnit(RelativeDateUnit unit)
    {
        var criterion = new RelativeDateCriterion(1, unit);

        Assert.That(criterion.Validate().IsValid, Is.True);
    }

    [TestCase(RelativeDateUnit.Days)]
    [TestCase(RelativeDateUnit.Weeks)]
    [TestCase(RelativeDateUnit.Months)]
    [TestCase(RelativeDateUnit.Years)]
    public void AnOffsetLargerThanTheAvailableRangeClampsToTheMinimumInstant(
        RelativeDateUnit unit)
    {
        var criterion = new RelativeDateCriterion(int.MaxValue, unit);

        Assert.That(
            criterion.Resolve(ReferenceTime),
            Is.EqualTo(DateTimeOffset.MinValue));
    }

    [Test]
    public void AnAbsoluteCriterionNeverEqualsARelativeCriterion()
    {
        DateCriterion absolute = new AbsoluteDateCriterion(ReferenceTime);
        DateCriterion relative = new RelativeDateCriterion(1, RelativeDateUnit.Years);

        Assert.That(absolute, Is.Not.EqualTo(relative));
        Assert.That(absolute == relative, Is.False);
    }

    [Test]
    public void CriteriaOfTheSameFormAndValueAreEqual()
    {
        DateCriterion first = new RelativeDateCriterion(6, RelativeDateUnit.Months);
        DateCriterion second = new RelativeDateCriterion(6, RelativeDateUnit.Months);

        Assert.That(first, Is.EqualTo(second));
        Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
    }
}
