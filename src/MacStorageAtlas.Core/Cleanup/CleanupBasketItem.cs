using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.Core.Cleanup;

public sealed record CleanupBasketItem(
    DiskItem Item,
    CleanupItemSnapshot Snapshot,
    CleanupProtectionStatus ProtectionStatus);
