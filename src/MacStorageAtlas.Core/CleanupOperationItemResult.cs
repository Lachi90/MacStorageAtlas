namespace MacStorageAtlas.Core;

public sealed record CleanupOperationItemResult(
    CleanupBasketItem Item,
    CleanupOperationItemStatus Status,
    string? Message = null);
