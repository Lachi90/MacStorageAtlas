namespace MacStorageAtlas.Core.Items;

public readonly record struct CloudAvailabilityTotals(
    long LocalFileCount,
    long CloudOnlyFileCount,
    long UnknownFileCount,
    long CloudOnlyLogicalSizeBytes,
    long UnlistedCloudFolderCount)
{
    public static CloudAvailabilityTotals Empty { get; } = default;

    public bool HasCloudContent =>
        CloudOnlyFileCount > 0 || UnlistedCloudFolderCount > 0;

    public CloudAvailability FolderAvailability
    {
        get
        {
            if (HasCloudContent)
            {
                return LocalFileCount == 0 && UnknownFileCount == 0
                    ? CloudAvailability.CloudOnly
                    : CloudAvailability.PartlyLocal;
            }

            return UnknownFileCount > 0
                ? CloudAvailability.Unknown
                : CloudAvailability.Local;
        }
    }

    public static CloudAvailabilityTotals ForFile(
        CloudAvailability availability,
        long cloudOnlyLogicalSizeBytes = 0) =>
        availability switch
        {
            CloudAvailability.Local => new(1, 0, 0, 0, 0),
            CloudAvailability.CloudOnly => new(
                0,
                1,
                0,
                Math.Max(0, cloudOnlyLogicalSizeBytes),
                0),
            _ => new(0, 0, 1, 0, 0)
        };

    public static CloudAvailabilityTotals ForUnlistedCloudFolder() => new(0, 0, 0, 0, 1);

    public static CloudAvailabilityTotals operator +(
        CloudAvailabilityTotals left,
        CloudAvailabilityTotals right) =>
        new(
            left.LocalFileCount + right.LocalFileCount,
            left.CloudOnlyFileCount + right.CloudOnlyFileCount,
            left.UnknownFileCount + right.UnknownFileCount,
            left.CloudOnlyLogicalSizeBytes + right.CloudOnlyLogicalSizeBytes,
            left.UnlistedCloudFolderCount + right.UnlistedCloudFolderCount);

    public static CloudAvailabilityTotals operator -(
        CloudAvailabilityTotals left,
        CloudAvailabilityTotals right) =>
        new(
            Math.Max(0, left.LocalFileCount - right.LocalFileCount),
            Math.Max(0, left.CloudOnlyFileCount - right.CloudOnlyFileCount),
            Math.Max(0, left.UnknownFileCount - right.UnknownFileCount),
            Math.Max(0, left.CloudOnlyLogicalSizeBytes - right.CloudOnlyLogicalSizeBytes),
            Math.Max(0, left.UnlistedCloudFolderCount - right.UnlistedCloudFolderCount));
}
