namespace MacStorageAtlas.Core.Items;

public sealed class DiskItem
{
    private readonly List<DiskItem> _children = [];
    private long _measuredSizeBytes;
    private bool _hasMeasuredSize;

    public DiskItem(string name, string path, bool isDirectory)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Name = name;
        Path = path;
        IsDirectory = isDirectory;
        Metadata = DiskItemMetadata.Unknown(
            isDirectory ? DiskItemKind.Directory : DiskItemKind.File);
    }

    public string Name { get; }

    public string Path { get; }

    public bool IsDirectory { get; }

    public DiskItemMetadata Metadata { get; internal set; }

    public long SizeBytes { get; internal set; }

    public long MeasuredSizeBytes
    {
        get => _hasMeasuredSize ? _measuredSizeBytes : SizeBytes;
        internal set
        {
            _measuredSizeBytes = value;
            _hasMeasuredSize = true;
        }
    }

    public long SharedSizeBytes { get; internal set; }

    public bool IsSizeCountedElsewhere => SharedSizeBytes > 0;

    public IReadOnlyList<DiskItem> Children => _children;

    internal void AddChild(DiskItem child) => _children.Add(child);

    public bool RemoveDescendant(DiskItem descendant)
    {
        ArgumentNullException.ThrowIfNull(descendant);

        var childIndex = _children.IndexOf(descendant);
        if (childIndex >= 0)
        {
            _children.RemoveAt(childIndex);
            MeasuredSizeBytes = Math.Max(
                0,
                MeasuredSizeBytes - descendant.MeasuredSizeBytes);
            SizeBytes = Math.Max(0, SizeBytes - descendant.SizeBytes);
            SharedSizeBytes = Math.Max(
                0,
                SharedSizeBytes - descendant.SharedSizeBytes);
            return true;
        }

        foreach (var child in _children)
        {
            if (child.RemoveDescendant(descendant))
            {
                MeasuredSizeBytes = Math.Max(
                    0,
                    MeasuredSizeBytes - descendant.MeasuredSizeBytes);
                SizeBytes = Math.Max(0, SizeBytes - descendant.SizeBytes);
                SharedSizeBytes = Math.Max(
                    0,
                    SharedSizeBytes - descendant.SharedSizeBytes);
                return true;
            }
        }

        return false;
    }

    internal void SortChildren(Comparison<DiskItem> comparison) => _children.Sort(comparison);

    internal bool ReplaceDescendant(DiskItem descendant, DiskItem? replacement)
    {
        ArgumentNullException.ThrowIfNull(descendant);

        var childIndex = _children.IndexOf(descendant);
        if (childIndex >= 0)
        {
            if (replacement is null)
            {
                _children.RemoveAt(childIndex);
            }
            else
            {
                _children[childIndex] = replacement;
            }

            ApplyReplacementDelta(descendant, replacement);
            return true;
        }

        var containingChild = _children.FirstOrDefault(
            child => child.IsDirectory && ContainsPath(child.Path, descendant.Path));
        if (containingChild is null
            || !containingChild.ReplaceDescendant(descendant, replacement))
        {
            return false;
        }

        ApplyReplacementDelta(descendant, replacement);
        return true;
    }

    private void ApplyReplacementDelta(DiskItem previous, DiskItem? replacement)
    {
        MeasuredSizeBytes = Math.Max(
            0,
            MeasuredSizeBytes
            - previous.MeasuredSizeBytes
            + (replacement?.MeasuredSizeBytes ?? 0));
        SizeBytes = Math.Max(
            0,
            SizeBytes - previous.SizeBytes + (replacement?.SizeBytes ?? 0));
        SharedSizeBytes = Math.Max(
            0,
            SharedSizeBytes
            - previous.SharedSizeBytes
            + (replacement?.SharedSizeBytes ?? 0));

        var ordered = _children
            .OrderByDescending(child => child.SizeBytes)
            .ToArray();
        _children.Clear();
        _children.AddRange(ordered);
    }

    private static bool ContainsPath(string ancestorPath, string path)
    {
        var prefix = ancestorPath.EndsWith(System.IO.Path.DirectorySeparatorChar)
            ? ancestorPath
            : ancestorPath + System.IO.Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.Ordinal);
    }
}
