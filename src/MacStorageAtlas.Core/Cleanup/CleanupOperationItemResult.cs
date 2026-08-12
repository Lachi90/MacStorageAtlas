namespace MacStorageAtlas.Core.Cleanup;

public sealed record CleanupOperationItemResult(
    CleanupBasketItem Item,
    CleanupOperationItemStatus Status,
    string? Message = null);
