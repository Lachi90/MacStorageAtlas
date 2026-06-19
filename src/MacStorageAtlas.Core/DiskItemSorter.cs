namespace MacStorageAtlas.Core;

public static class DiskItemSorter
{
    public static void SortBySizeDescending(DiskItem root)
    {
        ArgumentNullException.ThrowIfNull(root);

        foreach (var child in root.Children)
        {
            SortBySizeDescending(child);
        }

        root.SortChildren(static (left, right) => right.SizeBytes.CompareTo(left.SizeBytes));
    }
}
