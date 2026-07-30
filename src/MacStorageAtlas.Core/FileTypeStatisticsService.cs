using System.IO;

namespace MacStorageAtlas.Core;

public sealed class FileTypeStatisticsService
{
    public const string NoExtensionLabel = "(no extension)";

    public IReadOnlyList<FileTypeSummary> Calculate(DiskItem root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var totals = CreateTotals();
        AddFiles(root, totals);

        return Summarize(totals);
    }

    public IReadOnlyList<FileTypeSummary> Calculate(IReadOnlyList<DiskItem> files)
    {
        ArgumentNullException.ThrowIfNull(files);

        var totals = CreateTotals();
        foreach (var file in files)
        {
            AddFile(file, totals);
        }

        return Summarize(totals);
    }

    private static Dictionary<string, (long FileCount, long TotalSizeBytes)> CreateTotals() =>
        new(StringComparer.OrdinalIgnoreCase);

    private static IReadOnlyList<FileTypeSummary> Summarize(
        Dictionary<string, (long FileCount, long TotalSizeBytes)> totals) =>
        totals
            .Select(pair => new FileTypeSummary(
                pair.Key,
                pair.Value.FileCount,
                pair.Value.TotalSizeBytes))
            .OrderByDescending(summary => summary.TotalSizeBytes)
            .ThenBy(summary => summary.Extension, StringComparer.Ordinal)
            .ToArray();

    private static void AddFiles(
        DiskItem item,
        IDictionary<string, (long FileCount, long TotalSizeBytes)> totals)
    {
        if (!item.IsDirectory)
        {
            AddFile(item, totals);
            return;
        }

        foreach (var child in item.Children)
        {
            AddFiles(child, totals);
        }
    }

    private static void AddFile(
        DiskItem file,
        IDictionary<string, (long FileCount, long TotalSizeBytes)> totals)
    {
        var extension = Path.GetExtension(file.Name);
        var group = string.IsNullOrEmpty(extension)
            ? NoExtensionLabel
            : extension.ToLowerInvariant();
        totals.TryGetValue(group, out var total);
        totals[group] = (total.FileCount + 1, total.TotalSizeBytes + file.SizeBytes);
    }
}
