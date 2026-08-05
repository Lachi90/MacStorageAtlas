namespace MacStorageAtlas.Core;

public enum CleanupPreflightStatusKind
{
    Ready,
    Missing,
    IdentityChanged,
    SizeChanged,
    Protected,
    Error,
    DestinationCollision,
    DestinationInsideSource,
    AlreadyAtDestination
}
