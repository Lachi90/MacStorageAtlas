namespace MacStorageAtlas.Core;

public sealed record ScanExportDocument(
    ScanExportMetadata Metadata,
    IReadOnlyList<ScanExportRow> Items,
    IReadOnlyList<ScanError> Errors);
