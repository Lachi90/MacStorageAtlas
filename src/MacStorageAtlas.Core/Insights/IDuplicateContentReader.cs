namespace MacStorageAtlas.Core.Insights;

public interface IDuplicateContentReader
{
    ValueTask<Stream> OpenReadAsync(
        string path,
        CancellationToken cancellationToken = default);
}
