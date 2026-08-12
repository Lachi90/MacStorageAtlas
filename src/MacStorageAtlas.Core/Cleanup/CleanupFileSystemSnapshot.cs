using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Cleanup;

public sealed record CleanupFileSystemSnapshot(
    string Path,
    bool IsDirectory,
    long SizeBytes,
    long MeasuredSizeBytes,
    FileIdentity? Identity = null);
