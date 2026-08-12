namespace MacStorageAtlas.Core.History;

public static class ScanSnapshotSchema
{
    public const int CurrentVersion = 1;

    public static bool IsSupported(int schemaVersion) =>
        schemaVersion == CurrentVersion;
}
