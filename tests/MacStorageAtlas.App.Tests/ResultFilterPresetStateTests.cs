using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.Tests;

public class ResultFilterPresetStateTests
{
    private static readonly DateTimeOffset Reference =
        new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void ApplyingAPresetIdentifiesIt()
    {
        var filter = CreateFilter();
        var preset = BuiltIn(filter, BuiltInFilterPresets.LargeArchivesName);

        filter.ApplyPresetCommand.Execute(preset);

        Assert.Multiple(() =>
        {
            Assert.That(filter.AppliedPresetName, Is.EqualTo(preset.Name));
            Assert.That(filter.HasAppliedPreset, Is.True);
            Assert.That(filter.HasEditedCriteria, Is.False);
        });
    }

    [Test]
    public void EditingCriteriaAfterApplyingAPresetReportsAnEditedState()
    {
        var filter = CreateFilter();
        var preset = BuiltIn(filter, BuiltInFilterPresets.LargeArchivesName);

        filter.ApplyPresetCommand.Execute(preset);
        filter.MinimumSizeBytes = 12345;

        Assert.Multiple(() =>
        {
            Assert.That(filter.HasEditedCriteria, Is.True);
            Assert.That(filter.EditedFromPresetName, Is.EqualTo(preset.Name));
            Assert.That(filter.HasAppliedPreset, Is.False);
        });
    }

    [Test]
    public void ReturningToThePresetCriteriaClearsTheEditedState()
    {
        var filter = CreateFilter();
        var preset = BuiltIn(filter, BuiltInFilterPresets.LargeArchivesName);

        filter.ApplyPresetCommand.Execute(preset);
        var original = filter.MinimumSizeBytes;
        filter.MinimumSizeBytes = 12345;
        filter.MinimumSizeBytes = original;

        Assert.Multiple(() =>
        {
            Assert.That(filter.HasEditedCriteria, Is.False);
            Assert.That(filter.AppliedPresetName, Is.EqualTo(preset.Name));
        });
    }

    [Test]
    public void EnteringMatchingCriteriaIdentifiesThePresetWithoutAnEditedState()
    {
        var filter = CreateFilter();

        filter.MinimumSizeBytes = BuiltInFilterPresets.OneGigabyte;

        Assert.Multiple(() =>
        {
            Assert.That(
                filter.AppliedPresetName,
                Is.EqualTo(BuiltInFilterPresets.LargerThanOneGigabyteName));
            Assert.That(filter.HasEditedCriteria, Is.False);
        });
    }

    [Test]
    public void CriteriaMatchingNoPresetIdentifyNoneAndReportNoEditedState()
    {
        var filter = CreateFilter();

        filter.MinimumSizeBytes = 4321;

        Assert.Multiple(() =>
        {
            Assert.That(filter.AppliedPreset, Is.Null);
            Assert.That(filter.HasEditedCriteria, Is.False);
            Assert.That(filter.HasPresetState, Is.False);
        });
    }

    [Test]
    public void ClearingTheFilterForgetsTheAppliedPreset()
    {
        var filter = CreateFilter();
        filter.ApplyPresetCommand.Execute(
            BuiltIn(filter, BuiltInFilterPresets.LargeArchivesName));

        filter.ClearFilterCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(filter.HasEditedCriteria, Is.False);
            Assert.That(filter.EditedFromPreset, Is.Null);
        });
    }

    [Test]
    public void ABuiltInPresetCannotBeUpdated()
    {
        var filter = CreateFilter();
        filter.ApplyPresetCommand.Execute(
            BuiltIn(filter, BuiltInFilterPresets.LargeArchivesName));

        filter.MinimumSizeBytes = 12345;

        Assert.Multiple(() =>
        {
            Assert.That(filter.HasEditedCriteria, Is.True);
            Assert.That(filter.CanUpdatePreset, Is.False);
            Assert.That(filter.UpdatePresetCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public void ABuiltInPresetCannotBeRenamedOrDeleted()
    {
        var filter = CreateFilter();
        var preset = BuiltIn(filter, BuiltInFilterPresets.LargeArchivesName);

        filter.BeginRenamePresetCommand.Execute(preset);
        filter.DeletePresetCommand.Execute(preset);

        Assert.Multiple(() =>
        {
            Assert.That(filter.IsRenamingPreset, Is.False);
            Assert.That(
                filter.Presets.Any(candidate => candidate.Name == preset.Name),
                Is.True);
        });
    }

    [Test]
    public void AUserPresetIsUpdatedFromTheEditedCriteria()
    {
        var filter = CreateFilter();
        filter.MinimumSizeBytes = 1024;
        filter.NewPresetName = "Mine";
        filter.SavePresetCommand.Execute(null);

        filter.MinimumSizeBytes = 4096;

        Assert.That(filter.CanUpdatePreset, Is.True);

        filter.UpdatePresetCommand.Execute(null);

        var updated = filter.UserPresets.Single();

        Assert.Multiple(() =>
        {
            Assert.That(updated.Name, Is.EqualTo("Mine"));
            Assert.That(updated.Filter.MinimumSizeBytes, Is.EqualTo(4096));
            Assert.That(filter.HasEditedCriteria, Is.False);
        });
    }

    [Test]
    public void UpdatingIsUnavailableWhileTheCriteriaAreUnedited()
    {
        var filter = CreateFilter();
        filter.MinimumSizeBytes = 1024;
        filter.NewPresetName = "Mine";
        filter.SavePresetCommand.Execute(null);

        Assert.That(filter.CanUpdatePreset, Is.False);
    }

    [Test]
    public void RenamingAUserPresetKeepsItsCriteria()
    {
        var filter = CreateFilter();
        filter.MinimumSizeBytes = 2048;
        filter.NewPresetName = "Before";
        filter.SavePresetCommand.Execute(null);

        filter.BeginRenamePresetCommand.Execute(filter.UserPresets.Single());

        Assert.That(filter.IsRenamingPreset, Is.True);
        Assert.That(filter.RenamePresetName, Is.EqualTo("Before"));

        filter.RenamePresetName = "After";
        filter.CommitRenamePresetCommand.Execute(null);

        var renamed = filter.UserPresets.Single();

        Assert.Multiple(() =>
        {
            Assert.That(renamed.Name, Is.EqualTo("After"));
            Assert.That(renamed.Filter.MinimumSizeBytes, Is.EqualTo(2048));
            Assert.That(filter.IsRenamingPreset, Is.False);
        });
    }

    [Test]
    public void RenamingTheAppliedPresetKeepsItIdentified()
    {
        var filter = CreateFilter();
        filter.MinimumSizeBytes = 2048;
        filter.NewPresetName = "Before";
        filter.SavePresetCommand.Execute(null);

        filter.BeginRenamePresetCommand.Execute(filter.UserPresets.Single());
        filter.RenamePresetName = "After";
        filter.CommitRenamePresetCommand.Execute(null);

        Assert.That(filter.AppliedPresetName, Is.EqualTo("After"));
    }

    [Test]
    public void CancellingARenameLeavesTheNameUnchanged()
    {
        var filter = CreateFilter();
        filter.MinimumSizeBytes = 2048;
        filter.NewPresetName = "Before";
        filter.SavePresetCommand.Execute(null);

        filter.BeginRenamePresetCommand.Execute(filter.UserPresets.Single());
        filter.RenamePresetName = "After";
        filter.CancelRenamePresetCommand.Execute(null);

        Assert.Multiple(() =>
        {
            Assert.That(filter.UserPresets.Single().Name, Is.EqualTo("Before"));
            Assert.That(filter.IsRenamingPreset, Is.False);
        });
    }

    [Test]
    public void DeletingTheAppliedPresetForgetsIt()
    {
        var filter = CreateFilter();
        filter.MinimumSizeBytes = 2048;
        filter.NewPresetName = "Mine";
        filter.SavePresetCommand.Execute(null);

        filter.DeletePresetCommand.Execute(filter.UserPresets.Single());
        filter.MinimumSizeBytes = 4096;

        Assert.Multiple(() =>
        {
            Assert.That(filter.HasEditedCriteria, Is.False);
            Assert.That(filter.UserPresets, Is.Empty);
        });
    }

    [Test]
    public void APresetSavedFromBuiltInCriteriaMatchesThatBuiltInLater()
    {
        var now = Reference;
        var filter = new ResultFilterViewModel(() => now);
        var builtIn = BuiltIn(filter, BuiltInFilterPresets.NotModifiedForOneYearName);

        filter.ApplyPresetCommand.Execute(builtIn);
        filter.NewPresetName = "My stale files";
        filter.SavePresetCommand.Execute(null);

        var saved = filter.UserPresets.Single();

        now = Reference.AddYears(3);

        Assert.Multiple(() =>
        {
            Assert.That(saved.Filter, Is.EqualTo(builtIn.Filter));
            Assert.That(
                saved.Filter.ModifiedBefore!.Resolve(now),
                Is.EqualTo(builtIn.Filter.ModifiedBefore!.Resolve(now)));
            Assert.That(
                saved.Filter.ModifiedBefore!.Resolve(now),
                Is.EqualTo(now.AddYears(-1)));
        });
    }

    [Test]
    public void ARelativeBoundIsDescribedByItsSpanAndResolvedInstant()
    {
        var filter = CreateFilter();

        filter.ModifiedBefore.IsRelative = true;
        filter.ModifiedBefore.Count = 18;
        filter.ModifiedBefore.Unit = RelativeDateUnit.Months;

        Assert.Multiple(() =>
        {
            Assert.That(filter.ModifiedBefore.HasResolvedDescription, Is.True);
            Assert.That(
                filter.ModifiedBefore.ResolvedDescription,
                Does.Contain("18 months before now"));
            Assert.That(
                filter.ModifiedBefore.ResolvedDescription,
                Does.Contain("2025-01-30"));
        });
    }

    [Test]
    public void ANonPositiveRelativeCountIsReportedAsInvalid()
    {
        var filter = CreateFilter();

        filter.ModifiedBefore.IsRelative = true;
        filter.ModifiedBefore.Count = 0;

        Assert.Multiple(() =>
        {
            Assert.That(filter.IsFilterValid, Is.False);
            Assert.That(filter.HasValidationError, Is.True);
            Assert.That(filter.ModifiedBefore.HasResolvedDescription, Is.False);
        });
    }

    [Test]
    public void SwitchingABoundToRelativeDropsTheAbsoluteInstant()
    {
        var filter = CreateFilter();

        filter.CreatedAfter.Instant = Reference.AddYears(-2);
        filter.CreatedAfter.IsRelative = true;
        filter.CreatedAfter.Count = 5;
        filter.CreatedAfter.Unit = RelativeDateUnit.Days;

        Assert.That(
            filter.CurrentFilter.CreatedAfter,
            Is.EqualTo(new RelativeDateCriterion(5, RelativeDateUnit.Days)));
    }

    [Test]
    public void ApplyingAPresetWithARelativeBoundPopulatesTheRelativeEditor()
    {
        var filter = CreateFilter();
        var preset = BuiltIn(filter, BuiltInFilterPresets.NotModifiedForOneYearName);

        filter.ApplyPresetCommand.Execute(preset);

        Assert.Multiple(() =>
        {
            Assert.That(filter.ModifiedBefore.IsRelative, Is.True);
            Assert.That(filter.ModifiedBefore.Count, Is.EqualTo(1));
            Assert.That(filter.ModifiedBefore.Unit, Is.EqualTo(RelativeDateUnit.Years));
            Assert.That(filter.ModifiedBefore.Instant, Is.Null);
        });
    }

    [Test]
    public void ApplyingAPresetRaisesCriteriaChangedOnce()
    {
        var filter = CreateFilter();
        var raised = 0;
        filter.CriteriaChanged += (_, _) => raised++;

        filter.ApplyPresetCommand.Execute(
            BuiltIn(filter, BuiltInFilterPresets.NotModifiedForOneYearName));

        Assert.That(raised, Is.EqualTo(1));
    }

    [Test]
    public void TheNumericCountAdapterRoundTripsWholeNumbers()
    {
        var filter = CreateFilter();

        filter.ModifiedBefore.CountValue = 18m;

        Assert.Multiple(() =>
        {
            Assert.That(filter.ModifiedBefore.Count, Is.EqualTo(18));
            Assert.That(filter.ModifiedBefore.CountValue, Is.EqualTo(18m));
        });
    }

    [Test]
    public void TheNumericCountAdapterRoundsAFractionalEntry()
    {
        var filter = CreateFilter();

        filter.ModifiedBefore.CountValue = 2.6m;

        Assert.That(filter.ModifiedBefore.Count, Is.EqualTo(3));
    }

    [Test]
    public void ClearingTheNumericCountAdapterDeactivatesTheBound()
    {
        var filter = CreateFilter();
        filter.ModifiedBefore.IsRelative = true;
        filter.ModifiedBefore.CountValue = 5m;

        filter.ModifiedBefore.CountValue = null;

        Assert.Multiple(() =>
        {
            Assert.That(filter.ModifiedBefore.Count, Is.Null);
            Assert.That(filter.CurrentFilter.ModifiedBefore, Is.Null);
            Assert.That(filter.IsFilterActive, Is.False);
        });
    }

    private static ResultFilterViewModel CreateFilter() => new(() => Reference);

    private static FilterPreset BuiltIn(ResultFilterViewModel filter, string name) =>
        filter.Presets.Single(preset => preset.Name == name);
}
