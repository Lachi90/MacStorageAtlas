namespace MacStorageAtlas.Core;

public sealed class ScanSnapshotRequest
{
    public ScanSnapshotRequest(
        ScanSnapshotMetadata metadata,
        IEnumerable<ScanExportRow> rows,
        IReadOnlyList<ScanError>? errors = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(rows);

        Metadata = metadata;
        Rows = rows;
        Errors = errors ?? [];
    }

    public ScanSnapshotMetadata Metadata { get; }

    public IEnumerable<ScanExportRow> Rows { get; }

    public IReadOnlyList<ScanError> Errors { get; }
}
