using System;
using MacStorageAtlas.Core.Filtering;

namespace MacStorageAtlas.App.ViewModels;

public sealed record CloudAvailabilityOption(CloudAvailabilityCriterion Criterion)
{
    public string DisplayName => Criterion switch
    {
        CloudAvailabilityCriterion.Any => "Local and cloud-only files",
        CloudAvailabilityCriterion.LocalOnly => "Local files only",
        CloudAvailabilityCriterion.CloudOnly => "Cloud-only files only",
        _ => throw new ArgumentOutOfRangeException(nameof(Criterion), Criterion, null)
    };
}
