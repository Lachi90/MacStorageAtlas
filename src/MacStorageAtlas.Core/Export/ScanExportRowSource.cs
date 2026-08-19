using MacStorageAtlas.Core.Filtering;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Export;

public static class ScanExportRowSource
{
    public static IEnumerable<ScanExportRow> EnumerateFull(
        DiskItem root,
        StorageMeasurementMode measurementMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);

        return EnumerateFullCore(root, measurementMode, cancellationToken);
    }

    private static IEnumerable<ScanExportRow> EnumerateFullCore(
        DiskItem root,
        StorageMeasurementMode measurementMode,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<(DiskItem Item, int Depth)>();
        pending.Push((root, 0));

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var (item, depth) = pending.Pop();
            yield return ScanExportRow.FromDiskItem(item, depth, measurementMode);

            if (item.Children.Count == 0)
            {
                continue;
            }

            var ordered = item.Children.ToArray();
            Array.Sort(ordered, CompareForExport);

            for (var index = ordered.Length - 1; index >= 0; index--)
            {
                pending.Push((ordered[index], depth + 1));
            }
        }
    }

    public static IEnumerable<ScanExportRow> EnumerateFiltered(
        IReadOnlyList<DiskItem> matchedFiles,
        string rootPath,
        StorageMeasurementMode measurementMode,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(matchedFiles);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        return EnumerateFilteredCore(matchedFiles, rootPath, measurementMode, cancellationToken);
    }

    private static IEnumerable<ScanExportRow> EnumerateFilteredCore(
        IReadOnlyList<DiskItem> matchedFiles,
        string rootPath,
        StorageMeasurementMode measurementMode,
        CancellationToken cancellationToken)
    {
        var ordered = matchedFiles.ToArray();
        Array.Sort(ordered, CompareForExport);

        foreach (var file in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return ScanExportRow.FromDiskItem(
                file,
                DepthBelow(rootPath, file.Path),
                measurementMode);
        }
    }

    public static ScanExportSummary Summarize(
        DiskItem root,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(root);

        var itemCount = 0L;
        var totalCountedSizeBytes = 0L;
        var pending = new Stack<DiskItem>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var item = pending.Pop();
            itemCount++;

            if (!item.IsDirectory)
            {
                totalCountedSizeBytes += item.SizeBytes;
            }

            foreach (var child in item.Children)
            {
                pending.Push(child);
            }
        }

        return new ScanExportSummary(itemCount, totalCountedSizeBytes);
    }

    public static ScanExportSummary Summarize(FilterResult filterResult)
    {
        ArgumentNullException.ThrowIfNull(filterResult);

        return new ScanExportSummary(filterResult.MatchCount, filterResult.MatchedBytes);
    }

    internal static int DepthBelow(string rootPath, string path)
    {
        var root = rootPath.TrimEnd(Path.DirectorySeparatorChar);
        if (path.Length <= root.Length
            || !path.StartsWith(root, StringComparison.Ordinal)
            || path[root.Length] != Path.DirectorySeparatorChar)
        {
            return 0;
        }

        var depth = 0;
        var inSegment = false;

        for (var index = root.Length; index < path.Length; index++)
        {
            if (path[index] == Path.DirectorySeparatorChar)
            {
                inSegment = false;
                continue;
            }

            if (!inSegment)
            {
                inSegment = true;
                depth++;
            }
        }

        return depth;
    }

    private static int CompareForExport(DiskItem left, DiskItem right)
    {
        var bySize = right.SizeBytes.CompareTo(left.SizeBytes);
        return bySize != 0 ? bySize : string.CompareOrdinal(left.Path, right.Path);
    }
}
