namespace MacStorageAtlas.Core.Cleanup;

public sealed record CleanupBasketAddResult(
    CleanupBasketAddStatus Status,
    CleanupBasketItem? Item,
    IReadOnlyList<CleanupBasketItem> RemovedItems,
    string Message)
{
    public bool Changed =>
        Status is CleanupBasketAddStatus.Added
            or CleanupBasketAddStatus.ReplacedDescendants;
}
