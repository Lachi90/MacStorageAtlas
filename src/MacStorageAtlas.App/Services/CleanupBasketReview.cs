using System.Collections.Generic;
using System.Linq;
using MacStorageAtlas.Core.Cleanup;
using MacStorageAtlas.Core.Relocation;

namespace MacStorageAtlas.App.Services;

public sealed record CleanupBasketReview(
    CleanupBasketSummary Summary,
    IReadOnlyList<CleanupPreflightResult> Items,
    CleanupOperationKind Operation = CleanupOperationKind.Trash,
    RelocationDestination? Destination = null)
{
    public IReadOnlyList<CleanupPreflightResult> ExecutableItems =>
        Items.Where(item => item.CanExecute).ToArray();

    public long ExpectedReclaimedSizeBytes =>
        Operation == CleanupOperationKind.Copy
            ? 0
            : Summary.ExpectedReclaimableSizeBytes;

    public string OperationTitle => Operation switch
    {
        CleanupOperationKind.Move => "Move items to another location?",
        CleanupOperationKind.Copy => "Copy items to another location?",
        _ => "Move items to Trash?"
    };

    public string OperationDescription => Operation switch
    {
        CleanupOperationKind.Move =>
            "The items will be moved to the destination below. Nothing at the destination "
            + "is replaced, and an item is removed from its original location only after "
            + "the transfer is verified.",
        CleanupOperationKind.Copy =>
            "The items will be copied to the destination below. Nothing at the destination "
            + "is replaced, and no local space is reclaimed.",
        _ =>
            "The items will be moved to the macOS Trash and will not be permanently deleted."
    };

    public string ConfirmButtonText => Operation switch
    {
        CleanupOperationKind.Move => "Move Items",
        CleanupOperationKind.Copy => "Copy Items",
        _ => "Move to Trash"
    };
}
