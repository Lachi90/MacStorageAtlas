using MacStorageAtlas.Core;

namespace MacStorageAtlas.Tests;

public class DiskItemFilterTests
{
    [Test]
    public void AnEmptyFilterIsInactive()
    {
        Assert.That(DiskItemFilter.Empty.IsActive, Is.False);
    }

    [TestCase("   ")]
    [TestCase("")]
    [TestCase(null)]
    public void BlankTextDoesNotActivateAFilter(string? textTerm)
    {
        var filter = new DiskItemFilter { TextTerm = textTerm };

        Assert.That(filter.IsActive, Is.False);
    }

    [Test]
    public void AnyCriterionActivatesAFilter()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new DiskItemFilter { TextTerm = "a" }.IsActive, Is.True);
            Assert.That(new DiskItemFilter { MinimumSizeBytes = 1 }.IsActive, Is.True);
            Assert.That(new DiskItemFilter { MaximumSizeBytes = 1 }.IsActive, Is.True);
            Assert.That(
                new DiskItemFilter { ModifiedBefore = new AbsoluteDateCriterion(DateTimeOffset.UnixEpoch) }.IsActive,
                Is.True);
            Assert.That(new DiskItemFilter { Extensions = [".mov"] }.IsActive, Is.True);
            Assert.That(
                new DiskItemFilter { Categories = [FileCategory.Video] }.IsActive,
                Is.True);
            Assert.That(new DiskItemFilter { SharedStorageOnly = true }.IsActive, Is.True);
        });
    }

    [Test]
    public void AnEmptyFilterIsValid()
    {
        Assert.That(DiskItemFilter.Empty.Validate().IsValid, Is.True);
    }

    [Test]
    public void MinimumSizeAboveMaximumSizeIsInvalid()
    {
        var filter = new DiskItemFilter
        {
            MinimumSizeBytes = 2048,
            MaximumSizeBytes = 1024
        };

        var validation = filter.Validate();

        Assert.Multiple(() =>
        {
            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Message, Does.Contain("Minimum size"));
        });
    }

    [Test]
    public void EqualMinimumAndMaximumSizeIsValid()
    {
        var filter = new DiskItemFilter
        {
            MinimumSizeBytes = 1024,
            MaximumSizeBytes = 1024
        };

        Assert.That(filter.Validate().IsValid, Is.True);
    }

    [TestCase(-1L, null)]
    [TestCase(null, -1L)]
    public void NegativeSizeBoundsAreInvalid(long? minimum, long? maximum)
    {
        var filter = new DiskItemFilter
        {
            MinimumSizeBytes = minimum,
            MaximumSizeBytes = maximum
        };

        Assert.That(filter.Validate().IsValid, Is.False);
    }

    [Test]
    public void AnInvertedModifiedRangeIsInvalid()
    {
        var filter = new DiskItemFilter
        {
            ModifiedAfter = new AbsoluteDateCriterion(new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)),
            ModifiedBefore = new AbsoluteDateCriterion(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))
        };

        var validation = filter.Validate();

        Assert.Multiple(() =>
        {
            Assert.That(validation.IsValid, Is.False);
            Assert.That(validation.Message, Does.Contain("Modified"));
        });
    }

    [Test]
    public void AnInvertedCreatedRangeIsInvalid()
    {
        var filter = new DiskItemFilter
        {
            CreatedAfter = new AbsoluteDateCriterion(new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)),
            CreatedBefore = new AbsoluteDateCriterion(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))
        };

        Assert.That(filter.Validate().IsValid, Is.False);
    }

    [Test]
    public void AnInvertedAccessedRangeIsInvalid()
    {
        var filter = new DiskItemFilter
        {
            LastAccessedAfter = new AbsoluteDateCriterion(new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)),
            LastAccessedBefore = new AbsoluteDateCriterion(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero))
        };

        Assert.That(filter.Validate().IsValid, Is.False);
    }

    [Test]
    public void HasDateCriteriaReflectsOnlyDateBounds()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                new DiskItemFilter { MinimumSizeBytes = 1 }.HasDateCriteria,
                Is.False);
            Assert.That(
                new DiskItemFilter { LastAccessedAfter = new AbsoluteDateCriterion(DateTimeOffset.UnixEpoch) }
                    .HasDateCriteria,
                Is.True);
        });
    }

    [Test]
    public void NormalizedExtensionsAreLowercasedDottedAndDeduplicated()
    {
        var filter = new DiskItemFilter { Extensions = ["MOV", ".mov", "zip", "  "] };

        Assert.That(filter.NormalizedExtensions, Is.EqualTo([".mov", ".zip"]));
    }

    [Test]
    public void FiltersWithEqualCriteriaAreEqual()
    {
        var first = new DiskItemFilter
        {
            TextTerm = "report",
            MinimumSizeBytes = 1024,
            Extensions = [".mov"],
            Categories = [FileCategory.Video]
        };
        var second = new DiskItemFilter
        {
            TextTerm = "report",
            MinimumSizeBytes = 1024,
            Extensions = [".mov"],
            Categories = [FileCategory.Video]
        };

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(second));
            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        });
    }

    [Test]
    public void FiltersWithDifferentCollectionCriteriaAreNotEqual()
    {
        var first = new DiskItemFilter { Extensions = [".mov"] };
        var second = new DiskItemFilter { Extensions = [".zip"] };

        Assert.That(first, Is.Not.EqualTo(second));
    }
}
