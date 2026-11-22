using System.Globalization;
using MacStorageAtlas.App.Converters;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.App.ViewModels;

internal static class CloudAvailabilityText
{
    public static string Describe(DiskItem item, StorageMeasurementMode? measurementMode)
    {
        var label = CloudAvailabilityConverter.Describe(item.CloudAvailability);
        if (item.IsUnlistedCloudFolder)
        {
            return $"{label} · not listed, its contents were not scanned";
        }

        var totals = item.CloudTotals;
        if (totals.CloudOnlyFileCount == 0)
        {
            return label;
        }

        var size = CloudOnlySizeLabel(totals.CloudOnlyLogicalSizeBytes, measurementMode);
        return item.IsDirectory
            ? $"{label} · {CountLabel(totals.CloudOnlyFileCount, "file only in the cloud", "files only in the cloud")} ({size})"
            : $"{label} ({size})";
    }

    public static string CloudOnlySizeLabel(
        long cloudOnlyLogicalSizeBytes,
        StorageMeasurementMode? measurementMode)
    {
        var label = $"{FileSizeFormatter.Format(cloudOnlyLogicalSizeBytes)} logical, not on this Mac";
        return measurementMode == StorageMeasurementMode.Logical
            ? $"{label}, included in the logical total"
            : label;
    }

    public static string CountLabel(long count, string singular, string plural) =>
        count == 1
            ? $"1 {singular}"
            : $"{count.ToString("N0", CultureInfo.CurrentCulture)} {plural}";
}
