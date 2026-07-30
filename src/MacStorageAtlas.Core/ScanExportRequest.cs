namespace MacStorageAtlas.Core;

public sealed class ScanExportRequest
{
    public ScanExportRequest(
        ScanExportMetadata metadata,
        IEnumerable<ScanExportRow> rows,
        IReadOnlyList<ScanError>? errors = null)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(rows);

        Metadata = metadata;
        Rows = rows;
        Errors = errors ?? [];
    }

    public ScanExportMetadata Metadata { get; }

    public IEnumerable<ScanExportRow> Rows { get; }

    public IReadOnlyList<ScanError> Errors { get; }
}
