using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.Core.Scanning;

public static class ScanResultMerger
{
    public static bool ReplaceSubtree(
        DiskItem root,
        string subtreePath,
        DiskItem? replacement)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(subtreePath);

        var normalizedPath = ScanPath.Normalize(subtreePath);
        if (ScanPath.Comparer.Equals(ScanPath.Normalize(root.Path), normalizedPath))
        {
            throw new ArgumentException(
                "The scan root cannot be replaced as a subtree.",
                nameof(subtreePath));
        }

        if (replacement is not null
            && !ScanPath.Comparer.Equals(ScanPath.Normalize(replacement.Path), normalizedPath))
        {
            throw new ArgumentException(
                "The replacement must have the path of the replaced folder.",
                nameof(replacement));
        }

        var existing = FindByPath(root, normalizedPath);
        return existing is not null && root.ReplaceDescendant(existing, replacement);
    }

    public static DiskItem? FindByPath(DiskItem root, string path)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalizedPath = ScanPath.Normalize(path);
        var current = root;
        while (true)
        {
            if (ScanPath.Comparer.Equals(ScanPath.Normalize(current.Path), normalizedPath))
            {
                return current;
            }

            var next = current.Children.FirstOrDefault(child =>
                ScanPath.IsWithin(normalizedPath, ScanPath.Normalize(child.Path)));
            if (next is null)
            {
                return null;
            }

            current = next;
        }
    }

    public static IReadOnlyList<ScanError> MergeErrors(
        IReadOnlyList<ScanError> existing,
        string subtreePath,
        IReadOnlyList<ScanError> refreshed)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentException.ThrowIfNullOrWhiteSpace(subtreePath);
        ArgumentNullException.ThrowIfNull(refreshed);

        var normalizedPath = ScanPath.Normalize(subtreePath);
        return existing
            .Where(error => !IsInside(error.Path, normalizedPath))
            .Concat(refreshed)
            .ToArray();
    }

    public static IReadOnlyList<EquivalentPath> MergeOmittedEquivalentPaths(
        IReadOnlyList<EquivalentPath> existing,
        string subtreePath,
        IReadOnlyList<EquivalentPath> refreshed)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentException.ThrowIfNullOrWhiteSpace(subtreePath);
        ArgumentNullException.ThrowIfNull(refreshed);

        var normalizedPath = ScanPath.Normalize(subtreePath);
        return existing
            .Where(omission => !IsInside(omission.AlternatePath, normalizedPath))
            .Concat(refreshed)
            .Distinct()
            .ToArray();
    }

    private static bool IsInside(string path, string normalizedSubtreePath) =>
        !string.IsNullOrWhiteSpace(path)
        && Path.IsPathFullyQualified(path)
        && ScanPath.IsWithin(ScanPath.Normalize(path), normalizedSubtreePath);
}
