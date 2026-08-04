namespace MacStorageAtlas.Core;

public sealed record CleanupItemSnapshot(
    string Name,
    string Path,
    bool IsDirectory,
    long SizeBytes,
    long MeasuredSizeBytes,
    long SharedSizeBytes,
    DiskItemMetadata Metadata,
    FileIdentity? Identity = null)
{
    public static CleanupItemSnapshot FromDiskItem(DiskItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new CleanupItemSnapshot(
            item.Name,
            item.Path,
            item.IsDirectory,
            item.SizeBytes,
            item.MeasuredSizeBytes,
            item.SharedSizeBytes,
            item.Metadata);
    }
}
