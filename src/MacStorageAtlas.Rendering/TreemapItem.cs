using MacStorageAtlas.Core;

namespace MacStorageAtlas.Rendering;

/// <summary>
/// An item and its weight in a treemap layout.
/// </summary>
public readonly record struct TreemapItem(DiskItem Item, long SizeBytes)
{
    public TreemapItem(DiskItem item)
        : this(item, item?.SizeBytes ?? throw new ArgumentNullException(nameof(item)))
    {
    }
}
