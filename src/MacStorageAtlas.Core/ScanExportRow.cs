namespace MacStorageAtlas.Core;

public sealed record ScanExportRow(
    string Path,
    string Name,
    DiskItemKind Kind,
    int Depth,
    StorageMeasurementMode MeasurementMode,
    long MeasuredSizeBytes,
    long CountedSizeBytes,
    long SharedSizeBytes,
    bool IsSharedStorage,
    string Extension,
    FileCategory? Category,
    DateTimeOffset? CreatedUtc,
    DateTimeOffset? ModifiedUtc,
    DateTimeOffset? LastAccessedUtc)
{
    public static ScanExportRow FromDiskItem(
        DiskItem item,
        int depth,
        StorageMeasurementMode measurementMode)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentOutOfRangeException.ThrowIfNegative(depth);

        var extension = item.IsDirectory
            ? string.Empty
            : FileCategoryMap.NormalizeExtension(System.IO.Path.GetExtension(item.Name))
              ?? string.Empty;

        return new ScanExportRow(
            item.Path,
            item.Name,
            item.Metadata.Kind,
            depth,
            measurementMode,
            item.MeasuredSizeBytes,
            item.SizeBytes,
            item.SharedSizeBytes,
            item.IsSizeCountedElsewhere,
            extension,
            item.IsDirectory ? null : FileCategoryMap.Find(extension),
            item.Metadata.CreatedTimeUtc,
            item.Metadata.ModifiedTimeUtc,
            item.Metadata.LastAccessTimeUtc);
    }
}
