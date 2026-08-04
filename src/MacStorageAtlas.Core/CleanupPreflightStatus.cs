namespace MacStorageAtlas.Core;

public sealed record CleanupPreflightStatus(
    CleanupPreflightStatusKind Kind,
    string Message)
{
    public bool CanExecute => Kind == CleanupPreflightStatusKind.Ready;

    public static CleanupPreflightStatus Ready { get; } =
        new(CleanupPreflightStatusKind.Ready, "Ready to move to Trash.");
}
