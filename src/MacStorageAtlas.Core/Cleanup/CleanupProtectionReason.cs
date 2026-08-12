namespace MacStorageAtlas.Core.Cleanup;

public enum CleanupProtectionReason
{
    None,
    ScanRoot,
    SystemPath,
    TrashLocation,
    OutsideScanResult,
    SensitiveLocation
}
