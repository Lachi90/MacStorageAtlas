namespace MacStorageAtlas.Core;

public sealed record ScanSnapshotDocument(
    ScanSnapshotMetadata Metadata,
    IReadOnlyList<ScanExportRow> Items,
    IReadOnlyList<ScanError> Errors);
