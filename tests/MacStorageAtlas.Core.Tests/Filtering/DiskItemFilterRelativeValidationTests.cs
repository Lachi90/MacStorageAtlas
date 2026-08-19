using MacStorageAtlas.Core.Filtering;

namespace MacStorageAtlas.Core.Tests.Filtering;

public class DiskItemFilterRelativeValidationTests
{
    private static readonly DateTimeOffset Reference =
        new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void ANonPositiveRelativeCountIsInvalidWithoutAClock()
    {
        var filter = new DiskItemFilter
        {
            ModifiedBefore = new RelativeDateCriterion(0, RelativeDateUnit.Days)
        };

        var validation = filter.Validate();

        Assert.That(validation.IsValid, Is.False);
        Assert.That(validation.Message, Does.Contain("greater than zero"));
    }

    [Test]
    public void ANonPositiveRelativeCountIsInvalidWithAClock()
    {
        var filter = new DiskItemFilter
        {
            ModifiedBefore = new RelativeDateCriterion(-5, RelativeDateUnit.Years)
        };

        Assert.That(filter.Validate(Reference).IsValid, Is.False);
    }

    [Test]
    public void ARelativeBoundResolvingPastAnAbsoluteBoundIsInvalid()
    {
        var filter = new DiskItemFilter
        {
            ModifiedAfter = new RelativeDateCriterion(1, RelativeDateUnit.Days),
            ModifiedBefore = new AbsoluteDateCriterion(Reference.AddYears(-5))
        };

        var validation = filter.Validate(Reference);

        Assert.That(validation.IsValid, Is.False);
        Assert.That(validation.Message, Does.Contain("Modified"));
    }

    [Test]
    public void TheClockFreeOverloadDoesNotFailOnResolvedOrdering()
    {
        var filter = new DiskItemFilter
        {
            ModifiedAfter = new RelativeDateCriterion(1, RelativeDateUnit.Days),
            ModifiedBefore = new AbsoluteDateCriterion(Reference.AddYears(-5))
        };

        Assert.That(filter.Validate().IsValid, Is.True);
    }

    [Test]
    public void MixedBoundsInValidOrderAreValid()
    {
        var filter = new DiskItemFilter
        {
            ModifiedAfter = new AbsoluteDateCriterion(Reference.AddYears(-5)),
            ModifiedBefore = new RelativeDateCriterion(1, RelativeDateUnit.Days)
        };

        Assert.That(filter.Validate(Reference).IsValid, Is.True);
        Assert.That(filter.Validate().IsValid, Is.True);
    }

    [Test]
    public void TwoAbsoluteBoundsOutOfOrderAreInvalidWithoutAClock()
    {
        var filter = new DiskItemFilter
        {
            CreatedAfter = new AbsoluteDateCriterion(Reference),
            CreatedBefore = new AbsoluteDateCriterion(Reference.AddDays(-1))
        };

        var validation = filter.Validate();

        Assert.That(validation.IsValid, Is.False);
        Assert.That(validation.Message, Does.Contain("Created"));
    }

    [Test]
    public void TwoRelativeBoundsOutOfOrderAreInvalidWithAClock()
    {
        var filter = new DiskItemFilter
        {
            LastAccessedAfter = new RelativeDateCriterion(1, RelativeDateUnit.Days),
            LastAccessedBefore = new RelativeDateCriterion(1, RelativeDateUnit.Years)
        };

        Assert.That(filter.Validate(Reference).IsValid, Is.False);
    }

    [Test]
    public void ARelativeBoundMakesTheFilterActiveAndDateBearing()
    {
        var filter = new DiskItemFilter
        {
            ModifiedBefore = new RelativeDateCriterion(1, RelativeDateUnit.Years)
        };

        Assert.That(filter.IsActive, Is.True);
        Assert.That(filter.HasDateCriteria, Is.True);
    }

    [Test]
    public void FiltersDifferingOnlyInBoundFormAreNotEqual()
    {
        var absolute = new DiskItemFilter
        {
            ModifiedBefore = new AbsoluteDateCriterion(Reference.AddYears(-1))
        };
        var relative = new DiskItemFilter
        {
            ModifiedBefore = new RelativeDateCriterion(1, RelativeDateUnit.Years)
        };

        Assert.That(absolute, Is.Not.EqualTo(relative));
    }
}
