namespace MacStorageAtlas.Core.Cleanup;

public sealed record CleanupPreflightResult(
    CleanupBasketItem Item,
    CleanupPreflightStatus Status)
{
    public bool CanExecute => Status.CanExecute;
}
