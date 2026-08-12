namespace MacStorageAtlas.Core.Cleanup;

public enum CleanupBasketAddStatus
{
    Added,
    AlreadySelected,
    CoveredByAncestor,
    ReplacedDescendants,
    Protected
}
