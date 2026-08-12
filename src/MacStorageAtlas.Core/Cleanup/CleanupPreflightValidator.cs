namespace MacStorageAtlas.Core.Cleanup;

public sealed class CleanupPreflightValidator(
    CleanupProtectedPathPolicy protectedPathPolicy,
    ICleanupFileSystemMetadataReader metadataReader)
{
    public IReadOnlyList<CleanupPreflightResult> Validate(
        IEnumerable<CleanupBasketItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items.Select(Validate).ToList();
    }

    public CleanupPreflightResult Validate(CleanupBasketItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var protectionStatus = protectedPathPolicy.Classify(item.Snapshot.Path);
        if (protectionStatus.IsProtected)
        {
            return new CleanupPreflightResult(
                item,
                new CleanupPreflightStatus(
                    CleanupPreflightStatusKind.Protected,
                    protectionStatus.Message));
        }

        if (!metadataReader.TryReadSnapshot(
                item.Snapshot.Path,
                out var currentSnapshot))
        {
            return new CleanupPreflightResult(
                item,
                new CleanupPreflightStatus(
                    CleanupPreflightStatusKind.Missing,
                    "The item no longer exists."));
        }

        if (item.Snapshot.Identity is { } scanIdentity
            && currentSnapshot.Identity is { } currentIdentity
            && scanIdentity != currentIdentity)
        {
            return new CleanupPreflightResult(
                item,
                new CleanupPreflightStatus(
                    CleanupPreflightStatusKind.IdentityChanged,
                    "The item changed since the scan."));
        }

        if (item.Snapshot.IsDirectory != currentSnapshot.IsDirectory
            || (!item.Snapshot.IsDirectory
                && item.Snapshot.SizeBytes != currentSnapshot.SizeBytes))
        {
            return new CleanupPreflightResult(
                item,
                new CleanupPreflightStatus(
                    CleanupPreflightStatusKind.SizeChanged,
                    "The item changed since the scan."));
        }

        return new CleanupPreflightResult(item, CleanupPreflightStatus.Ready);
    }
}
