using MacStorageAtlas.Core.Cleanup;

namespace MacStorageAtlas.Core.Relocation;

public sealed class RelocationPreflightValidator(
    CleanupPreflightValidator sourceValidator,
    IRelocationDestinationProbe probe)
{
    public IReadOnlyList<CleanupPreflightResult> Validate(
        IEnumerable<CleanupBasketItem> items,
        RelocationDestination destination,
        CleanupOperationKind operation)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(destination);

        return items
            .Select(item => Validate(item, destination, operation))
            .ToList();
    }

    public CleanupPreflightResult Validate(
        CleanupBasketItem item,
        RelocationDestination destination,
        CleanupOperationKind operation)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(destination);

        var sourceResult = sourceValidator.Validate(item);
        if (!sourceResult.CanExecute)
        {
            return sourceResult;
        }

        var sourcePath = CleanupProtectedPathPolicy.NormalizePath(item.Snapshot.Path);

        if (IsSameOrDescendantOf(destination.NormalizedPath, sourcePath))
        {
            return Blocked(
                item,
                CleanupPreflightStatusKind.DestinationInsideSource,
                "An item cannot be moved or copied into itself.");
        }

        if (operation == CleanupOperationKind.Move
            && IsAlreadyAtDestination(sourcePath, destination.NormalizedPath))
        {
            return Blocked(
                item,
                CleanupPreflightStatusKind.AlreadyAtDestination,
                "The item is already at the destination.");
        }

        if (probe.Exists(destination.CombineWith(item.Snapshot.Name)))
        {
            return Blocked(
                item,
                CleanupPreflightStatusKind.DestinationCollision,
                "An item with the same name already exists at the destination.");
        }

        return new CleanupPreflightResult(
            item,
            CleanupPreflightStatus.ReadyFor(operation));
    }

    private static CleanupPreflightResult Blocked(
        CleanupBasketItem item,
        CleanupPreflightStatusKind kind,
        string message) =>
        new(item, new CleanupPreflightStatus(kind, message));

    private static bool IsAlreadyAtDestination(string sourcePath, string destinationPath) =>
        Path.GetDirectoryName(sourcePath) is { } parentPath
        && string.Equals(
            CleanupProtectedPathPolicy.NormalizePath(parentPath),
            destinationPath,
            StringComparison.Ordinal);

    private static bool IsSameOrDescendantOf(string path, string possibleAncestor) =>
        string.Equals(path, possibleAncestor, StringComparison.Ordinal)
        || path.StartsWith(
            possibleAncestor + Path.DirectorySeparatorChar,
            StringComparison.Ordinal);
}
