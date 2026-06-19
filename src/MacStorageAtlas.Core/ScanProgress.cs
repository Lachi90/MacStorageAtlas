namespace MacStorageAtlas.Core;

public sealed record ScanProgress(
    string CurrentPath,
    long FilesScanned,
    long DirectoriesScanned,
    long BytesScanned,
    DiskItem Root,
    IReadOnlyList<ScanError> Errors,
    bool IsCompleted = false);
