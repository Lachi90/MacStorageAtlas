namespace MacStorageAtlas.Core;

public sealed record CleanupBasketItem(
    DiskItem Item,
    CleanupItemSnapshot Snapshot,
    CleanupProtectionStatus ProtectionStatus);
