namespace MacStorageAtlas.Core;

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
                "The scan root is protected from basket cleanup.");
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
                "Trash locations are protected from basket cleanup.");
        }

        if (IsSystemPath(normalizedPath))
        {
            return new CleanupProtectionStatus(
                CleanupProtectionReason.SystemPath,
                "macOS system locations are protected from basket cleanup.");
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

    internal static string NormalizePath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        return fullPath.Length > 1
            ? fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            : fullPath;
    }
}
