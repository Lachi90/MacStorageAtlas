namespace MacStorageAtlas.Core;

public sealed record CleanupFileSystemSnapshot(
    string Path,
    bool IsDirectory,
    long SizeBytes,
    long MeasuredSizeBytes,
    FileIdentity? Identity = null);
