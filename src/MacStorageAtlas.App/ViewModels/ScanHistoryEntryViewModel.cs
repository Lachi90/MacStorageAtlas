using System;
using System.Globalization;
using MacStorageAtlas.App.Converters;
using MacStorageAtlas.Core.History;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.App.ViewModels;

public sealed class ScanHistoryEntryViewModel
{
    public ScanHistoryEntryViewModel(ScanHistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        SnapshotId = entry.SnapshotId;
        IsReadable = entry.IsReadable;
        StoredSize = FileSizeFormatter.Format(entry.StoredSizeBytes);
        StoredSizeBytes = entry.StoredSizeBytes;

        if (entry.Descriptor is { } descriptor)
        {
            RootPath = descriptor.RootPath;
            ScanCompletedAt = descriptor.ScanCompletedAt;
            CapturedAt = descriptor.CapturedAt;
            RefreshedFolderCount = descriptor.Metadata.RefreshedFolders.Count;
            CompletedAt = descriptor.ScanCompletedAt.ToLocalTime().ToString(
                "g",
                CultureInfo.CurrentCulture);
            ItemCount = descriptor.ItemCount.ToString("N0", CultureInfo.CurrentCulture);
            MeasurementBasis = StorageMeasurementModeLabelConverter.Label(
                descriptor.MeasurementMode);
            Completeness = DescribeCompleteness(descriptor.Completeness);
            IsComplete = descriptor.IsComplete;
            return;
        }

        RootPath = string.Empty;
        ScanCompletedAt = DateTimeOffset.MinValue;
        CapturedAt = DateTimeOffset.MinValue;
        CompletedAt = string.Empty;
        ItemCount = string.Empty;
        MeasurementBasis = string.Empty;
        Completeness = entry.UnreadableMessage ?? "This snapshot could not be read.";
        IsComplete = false;
    }

    public string SnapshotId { get; }

    public string RootPath { get; }

    public DateTimeOffset ScanCompletedAt { get; }

    public DateTimeOffset CapturedAt { get; }

    public int RefreshedFolderCount { get; }

    public bool HasRefreshedFolders => RefreshedFolderCount > 0;

    public string RefreshedFoldersLabel =>
        RefreshedFolderCount switch
        {
            0 => string.Empty,
            1 => "Contains rescanned folders (1 folder rescanned after the original scan)",
            _ => $"Contains rescanned folders ({RefreshedFolderCount.ToString("N0", CultureInfo.CurrentCulture)} folders rescanned after the original scan)"
        };

    public string CompletedAt { get; }

    public string ItemCount { get; }

    public string StoredSize { get; }

    public long StoredSizeBytes { get; }

    public string MeasurementBasis { get; }

    public string Completeness { get; }

    public bool IsComplete { get; }

    public bool IsReadable { get; }

    private static string DescribeCompleteness(ScanCompleteness completeness) =>
        completeness switch
        {
            ScanCompleteness.Complete => "Complete scan",
            ScanCompleteness.IncompleteRecoverableErrors =>
                "Partial scan, some paths could not be read",
            ScanCompleteness.IncompleteAccessRestricted =>
                "Partial scan, Full Disk Access was missing",
            ScanCompleteness.IncompleteCloudFoldersNotListed =>
                "Partial scan, some cloud folders were not listed",
            _ => "Scan completeness undetermined"
        };
}
