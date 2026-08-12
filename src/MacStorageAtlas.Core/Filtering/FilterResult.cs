using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.Core.Filtering;

public sealed class FilterResult
{
    private readonly HashSet<DiskItem> _matchedFiles;
    private readonly Dictionary<DiskItem, long> _matchedBytesByDirectory;

    internal FilterResult(
        DiskItemFilter filter,
        DateTimeOffset referenceTime,
        IReadOnlyList<DiskItem> matchedFiles,
        Dictionary<DiskItem, long> matchedBytesByDirectory,
        long unknownDateExclusionCount)
    {
        Filter = filter;
        ReferenceTime = referenceTime;
        MatchedFiles = matchedFiles;
        _matchedFiles = new HashSet<DiskItem>(
            matchedFiles,
            ReferenceEqualityComparer.Instance);
        _matchedBytesByDirectory = matchedBytesByDirectory;
        UnknownDateExclusionCount = unknownDateExclusionCount;
        MatchedBytes = matchedFiles.Sum(file => file.SizeBytes);
    }

    public DiskItemFilter Filter { get; }

    public DateTimeOffset ReferenceTime { get; }

    public IReadOnlyList<DiskItem> MatchedFiles { get; }

    public int MatchCount => MatchedFiles.Count;

    public long MatchedBytes { get; }

    public long UnknownDateExclusionCount { get; }

    public bool IsFilterActive => Filter.IsActive;

    public bool Matches(DiskItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return _matchedFiles.Contains(item);
    }

    public bool IsVisible(DiskItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.IsDirectory
            ? _matchedBytesByDirectory.ContainsKey(item)
            : _matchedFiles.Contains(item);
    }

    public long MatchedBytesFor(DiskItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!item.IsDirectory)
        {
            return _matchedFiles.Contains(item) ? item.SizeBytes : 0;
        }

        return _matchedBytesByDirectory.TryGetValue(item, out var bytes) ? bytes : 0;
    }
}
