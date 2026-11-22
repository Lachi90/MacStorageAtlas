using MacStorageAtlas.Core.Filtering;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Export;

public sealed record ScanExportMetadata(
    string RootPath,
    DateTimeOffset ScanCompletedAt,
    ScanOptions Options,
    StorageMeasurementMode MeasurementMode,
    CloneAccountingCoverage CloneAccountingCoverage,
    ScanExportScope Scope,
    DiskItemFilter? Filter,
    long ItemCount,
    long TotalCountedSizeBytes)
{
    public const int CurrentSchemaVersion = 3;

    public IReadOnlyList<RefreshedFolder> RefreshedFolders { get; init; } = [];

    public bool HasRefreshedFolders => RefreshedFolders.Count > 0;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
}
