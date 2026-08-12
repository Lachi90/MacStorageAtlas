using MacStorageAtlas.Core.Export;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.History;

public sealed record ScanSnapshotDocument(
    ScanSnapshotMetadata Metadata,
    IReadOnlyList<ScanExportRow> Items,
    IReadOnlyList<ScanError> Errors);
