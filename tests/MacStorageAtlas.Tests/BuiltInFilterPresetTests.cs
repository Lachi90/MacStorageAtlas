using MacStorageAtlas.Core;

namespace MacStorageAtlas.Tests;

public class BuiltInFilterPresetTests
{
    private static readonly DateTimeOffset Reference =
        new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void TheBuiltInListIsEqualAcrossWidelySeparatedReferenceTimes()
    {
        var first = BuiltInFilterPresets.Create();
        var second = BuiltInFilterPresets.Create();

        Assert.That(first, Is.EqualTo(second));
    }

    [Test]
    public void EveryBuiltInPresetIsMarkedAsBuiltIn()
    {
        Assert.That(
            BuiltInFilterPresets.Create().All(preset => preset.IsBuiltIn),
            Is.True);
    }

    [Test]
    public void TheAgePresetIsExpressedRelatively()
    {
        var preset = BuiltInFilterPresets.Create().Single(
            candidate => candidate.Name == BuiltInFilterPresets.NotModifiedForOneYearName);

        Assert.That(
            preset.Filter.ModifiedBefore,
            Is.EqualTo(new RelativeDateCriterion(1, RelativeDateUnit.Years)));
    }

    [Test]
    public void TheAgePresetResolvesRelativeToWhicheverReferenceTimeIsUsed()
    {
        var preset = BuiltInFilterPresets.Create().Single(
            candidate => candidate.Name == BuiltInFilterPresets.NotModifiedForOneYearName);
        var bound = preset.Filter.ModifiedBefore;

        Assert.That(bound, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(
                bound!.Resolve(Reference),
                Is.EqualTo(Reference.AddYears(-1)));
            Assert.That(
                bound.Resolve(Reference.AddYears(3)),
                Is.EqualTo(Reference.AddYears(2)));
        });
    }

    [Test]
    public void EveryBuiltInPresetIsValid()
    {
        Assert.That(
            BuiltInFilterPresets.Create()
                .All(preset => preset.Filter.Validate(Reference).IsValid),
            Is.True);
    }
}
