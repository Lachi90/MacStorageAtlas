namespace MacStorageAtlas.Core.Insights;

public sealed record DuplicateGroup
{
    public DuplicateGroup(long LogicalSizeBytes, IReadOnlyList<DuplicateGroupEntry> Entries)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(LogicalSizeBytes);
        ArgumentNullException.ThrowIfNull(Entries);

        if (Entries.Count < 2)
        {
            throw new ArgumentException(
                "A duplicate group must contain at least two entries.",
                nameof(Entries));
        }

        if (!Entries.Any(entry => entry.Kind == DuplicateGroupEntryKind.RetainedCopy))
        {
            throw new ArgumentException(
                "A duplicate group must preserve one retained copy.",
                nameof(Entries));
        }

        if (Entries.Any(entry => entry.LogicalSizeBytes != LogicalSizeBytes))
        {
            throw new ArgumentException(
                "Every duplicate group entry must have the group logical size.",
                nameof(Entries));
        }

        this.LogicalSizeBytes = LogicalSizeBytes;
        this.Entries = Entries.ToArray();
    }

    public long LogicalSizeBytes { get; }

    public IReadOnlyList<DuplicateGroupEntry> Entries { get; }

    public int ReclaimableCopyCount =>
        Entries.Count(entry => entry.Kind == DuplicateGroupEntryKind.ReclaimableCopy);

    public long ReclaimableSizeBytes =>
        Entries.Sum(entry => entry.ReclaimableSizeBytes);
}
