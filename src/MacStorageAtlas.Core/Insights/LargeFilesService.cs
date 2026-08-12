using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.Core.Insights;

public sealed class LargeFilesService
{
    public const int DefaultLimit = 100;

    public IReadOnlyList<DiskItem> GetLargestFiles(DiskItem root, int limit = DefaultLimit)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentOutOfRangeException.ThrowIfNegative(limit);

        if (limit == 0)
        {
            return [];
        }

        var files = new List<DiskItem>();
        Collect(root, files);

        return Rank(files, limit);
    }

    public IReadOnlyList<DiskItem> GetLargestFiles(
        IReadOnlyList<DiskItem> files,
        int limit = DefaultLimit)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentOutOfRangeException.ThrowIfNegative(limit);

        return limit == 0 ? [] : Rank(files, limit);
    }

    private static IReadOnlyList<DiskItem> Rank(IReadOnlyList<DiskItem> files, int limit) =>
        files
            .OrderByDescending(file => file.SizeBytes)
            .ThenBy(file => file.Path, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();

    private static void Collect(DiskItem item, List<DiskItem> files)
    {
        if (!item.IsDirectory)
        {
            files.Add(item);
            return;
        }

        foreach (var child in item.Children)
        {
            Collect(child, files);
        }
    }
}
