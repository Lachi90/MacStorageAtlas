namespace MacStorageAtlas.Core.History;

public static class ScanSnapshotSchema
{
    public const int CurrentVersion = 3;

    public const int MinimumSupportedVersion = 1;

    public static bool IsSupported(int schemaVersion) =>
        schemaVersion is >= MinimumSupportedVersion and <= CurrentVersion;
}
