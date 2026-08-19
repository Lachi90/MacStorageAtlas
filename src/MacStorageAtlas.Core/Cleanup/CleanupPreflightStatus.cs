namespace MacStorageAtlas.Core.Cleanup;

public sealed record CleanupPreflightStatus(
    CleanupPreflightStatusKind Kind,
    string Message)
{
    public bool CanExecute => Kind == CleanupPreflightStatusKind.Ready;

    public static CleanupPreflightStatus Ready { get; } =
        new(CleanupPreflightStatusKind.Ready, "Ready to move to Trash.");

    public static CleanupPreflightStatus ReadyToMove { get; } =
        new(CleanupPreflightStatusKind.Ready, "Ready to move to the destination.");

    public static CleanupPreflightStatus ReadyToCopy { get; } =
        new(CleanupPreflightStatusKind.Ready, "Ready to copy to the destination.");

    public static CleanupPreflightStatus ReadyFor(CleanupOperationKind operation) =>
        operation switch
        {
            CleanupOperationKind.Move => ReadyToMove,
            CleanupOperationKind.Copy => ReadyToCopy,
            _ => Ready
        };
}
