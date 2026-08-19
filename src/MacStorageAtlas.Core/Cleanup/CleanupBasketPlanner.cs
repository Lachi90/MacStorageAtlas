using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Cleanup;

public sealed class CleanupBasketPlanner
{
    private readonly StorageMeasurementMode _measurementMode;
    private readonly CleanupProtectedPathPolicy? _protectedPathPolicy;
    private readonly List<CleanupBasketItem> _items = [];

    public CleanupBasketPlanner(
        DiskItem scanRoot,
        StorageMeasurementMode measurementMode,
        CleanupProtectedPathPolicy? protectedPathPolicy = null)
    {
        ArgumentNullException.ThrowIfNull(scanRoot);

        _measurementMode = measurementMode;
        _protectedPathPolicy = protectedPathPolicy;
    }

    public IReadOnlyList<CleanupBasketItem> Items => _items;

    public CleanupBasketSummary Summary => CreateSummary(CleanupOperationKind.Trash);

    public CleanupBasketSummary GetSummary(CleanupOperationKind operation) =>
        CreateSummary(operation);

    public CleanupBasketAddResult Add(DiskItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var protectionStatus = _protectedPathPolicy?.Classify(item)
            ?? CleanupProtectionStatus.NotProtected;
        if (protectionStatus.IsProtected)
        {
            return new CleanupBasketAddResult(
                CleanupBasketAddStatus.Protected,
                null,
                [],
                protectionStatus.Message);
        }

        var existing = _items.FirstOrDefault(
            basketItem => PathsEqual(basketItem.Snapshot.Path, item.Path));
        if (existing is not null)
        {
            return new CleanupBasketAddResult(
                CleanupBasketAddStatus.AlreadySelected,
                existing,
                [],
                "The item is already in the cleanup basket.");
        }

        var ancestor = _items.FirstOrDefault(
            basketItem => IsAncestorOf(basketItem.Item, item));
        if (ancestor is not null)
        {
            return new CleanupBasketAddResult(
                CleanupBasketAddStatus.CoveredByAncestor,
                ancestor,
                [],
                "The item is already covered by a selected directory.");
        }

        var coveredDescendants = _items
            .Where(basketItem => IsAncestorOf(item, basketItem.Item))
            .ToList();

        foreach (var descendant in coveredDescendants)
        {
            _items.Remove(descendant);
        }

        var added = new CleanupBasketItem(
            item,
            CleanupItemSnapshot.FromDiskItem(item),
            protectionStatus);
        _items.Add(added);

        return new CleanupBasketAddResult(
            coveredDescendants.Count == 0
                ? CleanupBasketAddStatus.Added
                : CleanupBasketAddStatus.ReplacedDescendants,
            added,
            coveredDescendants,
            coveredDescendants.Count == 0
                ? "Added to the cleanup basket."
                : "The selected directory now covers previously selected items.");
    }

    public bool Remove(DiskItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var existing = _items.FirstOrDefault(
            basketItem => PathsEqual(basketItem.Snapshot.Path, item.Path));
        return existing is not null && _items.Remove(existing);
    }

    public void Clear() => _items.Clear();

    private CleanupBasketSummary CreateSummary(CleanupOperationKind operation)
    {
        if (_items.Count == 0)
        {
            return CleanupBasketSummary.Empty;
        }

        return new CleanupBasketSummary(
            _items.Count,
            _items.Sum(item => item.Snapshot.SizeBytes),
            operation == CleanupOperationKind.Copy
                ? 0
                : _items.Sum(GetExpectedReclaimableSize));
    }

    private long GetExpectedReclaimableSize(CleanupBasketItem item) =>
        _measurementMode == StorageMeasurementMode.Logical
            ? item.Snapshot.SizeBytes
            : item.Snapshot.MeasuredSizeBytes;

    private bool IsAncestorOf(DiskItem possibleAncestor, DiskItem item) =>
        !ReferenceEquals(possibleAncestor, item)
        && ContainsDescendant(possibleAncestor, item);

    private static bool ContainsDescendant(DiskItem possibleAncestor, DiskItem item)
    {
        foreach (var child in possibleAncestor.Children)
        {
            if (ReferenceEquals(child, item) || ContainsDescendant(child, item))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            NormalizePath(left),
            NormalizePath(right),
            StringComparison.Ordinal);

    private static string NormalizePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return fullPath.Length > 1
            ? fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : fullPath;
    }
}
