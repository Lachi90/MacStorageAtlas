namespace MacStorageAtlas.Core;

public sealed class DiskItem
{
    private readonly List<DiskItem> _children = [];

    public DiskItem(string name, string path, bool isDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        Name = name;
        Path = path;
        IsDirectory = isDirectory;
    }

    public string Name { get; }

    public string Path { get; }

    public bool IsDirectory { get; }

    public long SizeBytes { get; internal set; }

    public IReadOnlyList<DiskItem> Children => _children;

    internal void AddChild(DiskItem child) => _children.Add(child);

    public bool RemoveDescendant(DiskItem descendant)
    {
        ArgumentNullException.ThrowIfNull(descendant);

        var childIndex = _children.IndexOf(descendant);
        if (childIndex >= 0)
        {
            _children.RemoveAt(childIndex);
            SizeBytes = Math.Max(0, SizeBytes - descendant.SizeBytes);
            return true;
        }

        foreach (var child in _children)
        {
            if (child.RemoveDescendant(descendant))
            {
                SizeBytes = Math.Max(0, SizeBytes - descendant.SizeBytes);
                return true;
            }
        }

        return false;
    }

    internal void SortChildren(Comparison<DiskItem> comparison) => _children.Sort(comparison);
}
