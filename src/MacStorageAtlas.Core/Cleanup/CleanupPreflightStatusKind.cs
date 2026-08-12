namespace MacStorageAtlas.Core.Cleanup;

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
