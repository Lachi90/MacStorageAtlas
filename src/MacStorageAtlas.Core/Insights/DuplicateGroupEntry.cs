using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Insights;

public sealed record DuplicateGroupEntry
{
    public DuplicateGroupEntry(
        DiskItem item,
        long logicalSizeBytes,
        DuplicateGroupEntryKind kind,
        FileIdentity? identity = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentOutOfRangeException.ThrowIfNegative(logicalSizeBytes);

        Item = item;
        LogicalSizeBytes = logicalSizeBytes;
        Kind = kind;
        Identity = identity;
    }

    public DiskItem Item { get; }

    public long LogicalSizeBytes { get; }

    public DuplicateGroupEntryKind Kind { get; }

    public FileIdentity? Identity { get; }

    public long ReclaimableSizeBytes =>
        Kind == DuplicateGroupEntryKind.ReclaimableCopy
            ? LogicalSizeBytes
            : 0;

    public bool IsLinkedPath => Kind == DuplicateGroupEntryKind.LinkedPath;
}
