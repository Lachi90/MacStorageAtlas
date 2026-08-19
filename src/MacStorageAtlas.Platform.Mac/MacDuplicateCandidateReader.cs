using MacStorageAtlas.Core.Insights;

namespace MacStorageAtlas.Platform.Mac;

public sealed class MacDuplicateCandidateReader : IDuplicateCandidateMetadataReader, IDuplicateContentReader
{
    private const int BufferSizeBytes = 128 * 1024;
    private readonly MacFileMetadataReader _metadataReader;
    private readonly IMacCloudFileStatusReader _cloudStatusReader;

    public MacDuplicateCandidateReader()
        : this(new MacFileMetadataReader(), new MacCloudFileStatusReader())
    {
    }

    internal MacDuplicateCandidateReader(
        MacFileMetadataReader metadataReader,
        IMacCloudFileStatusReader cloudStatusReader)
    {
        _metadataReader = metadataReader;
        _cloudStatusReader = cloudStatusReader;
    }

    public ValueTask<DuplicateCandidateMetadata> ReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The file no longer exists.", path);
        }

        var info = new FileInfo(path);
        var fileMetadata = _metadataReader.Read(path);
        var availability = _cloudStatusReader.GetContentAvailability(path)
            ?? DuplicateContentAvailability.Local;

        return ValueTask.FromResult(new DuplicateCandidateMetadata(
            info.Length,
            availability,
            fileMetadata.Identity,
            fileMetadata.LinkCount));
    }

    public ValueTask<Stream> OpenReadAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        Stream stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            BufferSizeBytes,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return ValueTask.FromResult(stream);
    }
}
