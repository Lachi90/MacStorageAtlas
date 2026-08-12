using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Export;

public sealed record ScanExportDocument(
    ScanExportMetadata Metadata,
    IReadOnlyList<ScanExportRow> Items,
    IReadOnlyList<ScanError> Errors);
