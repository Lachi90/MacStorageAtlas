namespace MacStorageAtlas.Core;

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
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
}
