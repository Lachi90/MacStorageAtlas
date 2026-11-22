using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.Core.Scanning;

public sealed record ScanProgress(
    string CurrentPath,
    long FilesScanned,
    long DirectoriesScanned,
    long BytesScanned,
    DiskItem Root,
    IReadOnlyList<ScanError> Errors,
    bool IsCompleted = false,
    StorageMeasurementMode MeasurementMode = StorageMeasurementMode.Logical,
    CloneAccountingCoverage CloneAccountingCoverage =
        CloneAccountingCoverage.Unavailable)
{
    public IReadOnlyList<EquivalentPath> OmittedEquivalentPaths { get; init; } = [];

    public IReadOnlyList<string> UnlistedCloudFolders { get; init; } = [];

    public bool IsRootPresent { get; init; } = true;
}
