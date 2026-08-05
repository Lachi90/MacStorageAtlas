namespace MacStorageAtlas.Core;

public sealed record RelocationDestinationValidation(
    RelocationDestinationStatusKind Kind,
    string Message,
    RelocationFreeSpace FreeSpace)
{
    public bool CanExecute => Kind == RelocationDestinationStatusKind.Ready;

    public static RelocationDestinationValidation Ready(RelocationFreeSpace freeSpace) =>
        new(
            RelocationDestinationStatusKind.Ready,
            "The destination is ready.",
            freeSpace);

    public static RelocationDestinationValidation Blocked(
        RelocationDestinationStatusKind kind,
        string message,
        RelocationFreeSpace freeSpace) =>
        new(kind, message, freeSpace);
}
