using System.Collections.Generic;
using System.Linq;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.Services;

public sealed record CleanupBasketReview(
    CleanupBasketSummary Summary,
    IReadOnlyList<CleanupPreflightResult> Items)
{
    public IReadOnlyList<CleanupPreflightResult> ExecutableItems =>
        Items.Where(item => item.CanExecute).ToArray();
}
