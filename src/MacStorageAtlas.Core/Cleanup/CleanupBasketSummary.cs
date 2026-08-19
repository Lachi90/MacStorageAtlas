namespace MacStorageAtlas.Core.Cleanup;

public sealed record CleanupBasketSummary(
    int ItemCount,
    long TotalLogicalSizeBytes,
    long ExpectedReclaimableSizeBytes)
{
    public static CleanupBasketSummary Empty { get; } = new(0, 0, 0);
}
