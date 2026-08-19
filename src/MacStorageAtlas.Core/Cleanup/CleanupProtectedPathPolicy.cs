using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.Core.Cleanup;

public sealed class CleanupProtectedPathPolicy
{
    private static readonly string[] SystemPathPrefixes =
    [
        "/System",
        "/Library",
        "/bin",
        "/sbin",
        "/usr",
        "/private",
        "/etc",
        "/var"
    ];

    private static readonly string[] StandardUserContainers =
    [
        "Desktop",
        "Documents",
        "Downloads",
        "Library",
        "Movies",
        "Music",
        "Pictures"
    ];

    private static readonly string[] SensitiveLibrarySubtrees =
    [
        "Mail",
        "Messages",
        "Safari",
        "Containers",
        "Group Containers",
        "Application Support"
    ];

    private readonly string _scanRootPath;
    private readonly HashSet<string> _scannedPaths;

    public CleanupProtectedPathPolicy(DiskItem scanRoot)
    {
        ArgumentNullException.ThrowIfNull(scanRoot);

        _scanRootPath = NormalizePath(scanRoot.Path);
        _scannedPaths = new HashSet<string>(StringComparer.Ordinal);
        AddScannedPaths(scanRoot);
    }

    public CleanupProtectionStatus Classify(DiskItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return Classify(item.Path);
    }

    public CleanupProtectionStatus Classify(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var normalizedPath = NormalizePath(path);
        if (string.Equals(normalizedPath, _scanRootPath, StringComparison.Ordinal))
        {
            return new CleanupProtectionStatus(
                CleanupProtectionReason.ScanRoot,
                "The scan root is protected from cleanup.");
        }

        if (!_scannedPaths.Contains(normalizedPath))
        {
            return new CleanupProtectionStatus(
                CleanupProtectionReason.OutsideScanResult,
                "The path is outside the completed scan result.");
        }

        if (IsTrashPath(normalizedPath))
        {
            return new CleanupProtectionStatus(
                CleanupProtectionReason.TrashLocation,
                "Trash locations are protected from cleanup.");
        }

        if (IsSystemPath(normalizedPath))
        {
            return new CleanupProtectionStatus(
                CleanupProtectionReason.SystemPath,
                "macOS system locations are protected from cleanup.");
        }

        if (IsSensitiveUserLocation(normalizedPath))
        {
            return new CleanupProtectionStatus(
                CleanupProtectionReason.SensitiveLocation,
                "Broad or sensitive user data locations are protected from cleanup.");
        }

        return CleanupProtectionStatus.NotProtected;
    }

    private void AddScannedPaths(DiskItem item)
    {
        _scannedPaths.Add(NormalizePath(item.Path));

        foreach (var child in item.Children)
        {
            AddScannedPaths(child);
        }
    }

    private static bool IsSystemPath(string path) =>
        SystemPathPrefixes.Any(prefix =>
            string.Equals(path, prefix, StringComparison.Ordinal)
            || path.StartsWith(prefix + Path.DirectorySeparatorChar, StringComparison.Ordinal));

    private static bool IsTrashPath(string path)
    {
        var segments = path.Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries);
        return segments.Any(segment =>
            string.Equals(segment, ".Trash", StringComparison.Ordinal)
            || string.Equals(segment, ".Trashes", StringComparison.Ordinal));
    }

    private static bool IsSensitiveUserLocation(string path)
    {
        var segments = GetPathSegments(path);
        var usersIndex = Array.FindIndex(
            segments,
            segment => string.Equals(segment, "Users", StringComparison.Ordinal));
        if (usersIndex < 0 || usersIndex + 1 >= segments.Length)
        {
            return false;
        }

        var relativeSegmentCount = segments.Length - usersIndex - 2;
        if (relativeSegmentCount == 0)
        {
            return true;
        }

        var firstRelativeSegment = segments[usersIndex + 2];
        if (relativeSegmentCount == 1
            && StandardUserContainers.Contains(
                firstRelativeSegment,
                StringComparer.Ordinal))
        {
            return true;
        }

        if (!string.Equals(firstRelativeSegment, "Library", StringComparison.Ordinal)
            || relativeSegmentCount < 2)
        {
            return false;
        }

        var libraryChild = segments[usersIndex + 3];
        return SensitiveLibrarySubtrees.Contains(
            libraryChild,
            StringComparer.Ordinal);
    }

    private static string[] GetPathSegments(string path) =>
        path.Split(
            Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries);

    internal static string NormalizePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return fullPath.Length > 1
            ? fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : fullPath;
    }
}
