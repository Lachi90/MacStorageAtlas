namespace MacStorageAtlas.Core;

public enum CleanupBasketAddStatus
{
    Added,
    AlreadySelected,
    CoveredByAncestor,
    ReplacedDescendants,
    Protected
}
