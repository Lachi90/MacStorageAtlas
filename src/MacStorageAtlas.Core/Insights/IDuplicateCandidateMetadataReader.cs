namespace MacStorageAtlas.Core.Insights;

public interface IDuplicateCandidateMetadataReader
{
    ValueTask<DuplicateCandidateMetadata> ReadAsync(
        string path,
        CancellationToken cancellationToken = default);
}
