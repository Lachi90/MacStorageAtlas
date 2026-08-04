namespace MacStorageAtlas.Core;

public sealed record CleanupProtectionStatus(
    CleanupProtectionReason Reason,
    string Message)
{
    public bool IsProtected => Reason != CleanupProtectionReason.None;

    public static CleanupProtectionStatus NotProtected { get; } =
        new(CleanupProtectionReason.None, string.Empty);
}
