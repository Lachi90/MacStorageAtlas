namespace MacStorageAtlas.Core.Scanning;

public interface IDiskScanner
{
    IAsyncEnumerable<ScanProgress> ScanAsync(
        string rootPath,
        ScanOptions? options = null,
        CancellationToken cancellationToken = default);
}
