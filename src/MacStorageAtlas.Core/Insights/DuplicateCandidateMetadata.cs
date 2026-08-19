using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Insights;

public sealed record DuplicateCandidateMetadata
{
    public DuplicateCandidateMetadata(
        long logicalLengthBytes,
        DuplicateContentAvailability contentAvailability,
        FileIdentity? identity = null,
        uint? linkCount = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(logicalLengthBytes);

        LogicalLengthBytes = logicalLengthBytes;
        ContentAvailability = contentAvailability;
        Identity = identity;
        LinkCount = linkCount;
    }

    public long LogicalLengthBytes { get; }

    public DuplicateContentAvailability ContentAvailability { get; }

    public FileIdentity? Identity { get; }

    public uint? LinkCount { get; }
}
