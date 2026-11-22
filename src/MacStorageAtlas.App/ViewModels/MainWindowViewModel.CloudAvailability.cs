using System;
using System.Collections.Generic;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MacStorageAtlas.App.Converters;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.App.ViewModels;

public partial class MainWindowViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUnlistedCloudFolders))]
    [NotifyPropertyChangedFor(nameof(UnlistedCloudFoldersSummary))]
    [NotifyPropertyChangedFor(nameof(UnlistedCloudFoldersDetails))]
    private IReadOnlyList<string> _unlistedCloudFolders = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasCloudAvailabilitySummary))]
    [NotifyPropertyChangedFor(nameof(CloudAvailabilitySummary))]
    [NotifyPropertyChangedFor(nameof(CloudAvailabilitySummaryDetails))]
    private CloudAvailabilityTotals _resultCloudTotals = CloudAvailabilityTotals.Empty;

    public bool HasUnlistedCloudFolders => UnlistedCloudFolders.Count > 0;

    public string? UnlistedCloudFoldersSummary =>
        UnlistedCloudFolders.Count switch
        {
            0 => null,
            1 => "1 cloud folder was not listed",
            var count => $"{count.ToString("N0", CultureInfo.CurrentCulture)} cloud folders were not listed"
        };

    public string? UnlistedCloudFoldersDetails =>
        UnlistedCloudFolders.Count == 0
            ? null
            : "These folders exist only with their cloud provider. Listing them could make "
              + "the provider download data, so their contents were not scanned:"
              + Environment.NewLine
              + string.Join(Environment.NewLine, UnlistedCloudFolders);

    public bool HasCloudAvailabilitySummary => ResultCloudTotals.CloudOnlyFileCount > 0;

    public string? CloudAvailabilitySummary
    {
        get
        {
            var totals = ResultCloudTotals;
            if (totals.CloudOnlyFileCount == 0)
            {
                return null;
            }

            var local = CountLabel(totals.LocalFileCount, "file local", "files local");
            var cloudOnly = CountLabel(
                totals.CloudOnlyFileCount,
                "file only in the cloud",
                "files only in the cloud");
            return $"{local} · {cloudOnly} ({CloudOnlySizeLabel(totals.CloudOnlyLogicalSizeBytes, ResultMeasurementMode)})";
        }
    }

    public string? CloudAvailabilitySummaryDetails =>
        ResultCloudTotals.CloudOnlyFileCount == 0
            ? null
            : ResultMeasurementMode == StorageMeasurementMode.Logical
                ? "The cloud-only size is the combined logical length of files whose content is "
                  + "stored only with their cloud provider. This logical scan total includes it."
                : "The cloud-only size is the combined logical length of files whose content is "
                  + "stored only with their cloud provider. It is not part of the measured sizes "
                  + "and is not space that can be freed on this Mac.";

    public string SelectedItemAvailability =>
        SelectedItem is { } item
            ? CloudAvailabilityText.Describe(item, ResultMeasurementMode)
            : string.Empty;

    internal static string CloudOnlySizeLabel(
        long cloudOnlyLogicalSizeBytes,
        StorageMeasurementMode measurementMode) =>
        CloudAvailabilityText.CloudOnlySizeLabel(cloudOnlyLogicalSizeBytes, measurementMode);

    private static string CountLabel(long count, string singular, string plural) =>
        CloudAvailabilityText.CountLabel(count, singular, plural);

    private void RefreshResultCloudTotals() =>
        ResultCloudTotals = _scanRoot?.CloudTotals ?? CloudAvailabilityTotals.Empty;
}
