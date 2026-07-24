using MacStorageAtlas.Core;

namespace MacStorageAtlas.Rendering;

public readonly record struct TreemapItem(DiskItem Item, long SizeBytes)
{
    public TreemapItem(DiskItem item)
        : this(item, item?.SizeBytes ?? throw new ArgumentNullException(nameof(item)))
    {
    }
}
