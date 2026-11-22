namespace MacStorageAtlas.Core.Scanning;

public interface IDiskScanner
{
    IAsyncEnumerable<ScanProgress> ScanAsync(
        string rootPath,
        ScanOptions? options = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<ScanProgress> ScanSubtreeAsync(
        string scanRootPath,
        string subtreePath,
        ScanOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This scanner cannot rescan a single folder.");
}
