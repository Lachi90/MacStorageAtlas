namespace MacStorageAtlas.Core;

public static class ScanExportRequestFactory
{
    public static ScanExportRequest Create(
        DiskItem root,
        ScanOptions options,
        StorageMeasurementMode measurementMode,
        CloneAccountingCoverage cloneAccountingCoverage,
        DateTimeOffset scanCompletedAt,
        FilterResult? filterResult = null,
        IReadOnlyList<ScanError>? errors = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(options);

        var filtered = filterResult is { IsFilterActive: true };
        var scope = filtered ? ScanExportScope.Filtered : ScanExportScope.Full;
        var summary = filtered
            ? ScanExportRowSource.Summarize(filterResult!)
            : ScanExportRowSource.Summarize(root, cancellationToken);

        var rows = filtered
            ? ScanExportRowSource.EnumerateFiltered(
                filterResult!.MatchedFiles,
                root.Path,
                measurementMode,
                cancellationToken)
            : ScanExportRowSource.EnumerateFull(root, measurementMode, cancellationToken);

        var metadata = new ScanExportMetadata(
            root.Path,
            scanCompletedAt,
            options,
            measurementMode,
            cloneAccountingCoverage,
            scope,
            filtered ? filterResult!.Filter : null,
            summary.ItemCount,
            summary.TotalCountedSizeBytes);

        return new ScanExportRequest(metadata, rows, errors);
    }
}
