using System.IO;

namespace MacStorageAtlas.Core;

public sealed class FileTypeStatisticsService
{
    public const string NoExtensionLabel = "(no extension)";

    public IReadOnlyList<FileTypeSummary> Calculate(DiskItem root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var totals = new Dictionary<string, (long FileCount, long TotalSizeBytes)>(
            StringComparer.OrdinalIgnoreCase);
        AddFiles(root, totals);

        return totals
            .Select(pair => new FileTypeSummary(
                pair.Key,
                pair.Value.FileCount,
                pair.Value.TotalSizeBytes))
            .OrderByDescending(summary => summary.TotalSizeBytes)
            .ThenBy(summary => summary.Extension, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddFiles(
        DiskItem item,
        IDictionary<string, (long FileCount, long TotalSizeBytes)> totals)
    {
        if (!item.IsDirectory)
        {
            var extension = Path.GetExtension(item.Name);
            var group = string.IsNullOrEmpty(extension)
                ? NoExtensionLabel
                : extension.ToLowerInvariant();
            totals.TryGetValue(group, out var total);
            totals[group] = (total.FileCount + 1, total.TotalSizeBytes + item.SizeBytes);
            return;
        }

        foreach (var child in item.Children)
        {
            AddFiles(child, totals);
        }
    }
}
