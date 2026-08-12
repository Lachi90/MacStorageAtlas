using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.Core.Insights;

public sealed record DuplicateSkippedCandidate
{
    public DuplicateSkippedCandidate(
        DiskItem item,
        DuplicateSkipReason reason,
        string message)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        Item = item;
        Reason = reason;
        Message = message;
    }

    public DiskItem Item { get; }

    public DuplicateSkipReason Reason { get; }

    public string Message { get; }
}
