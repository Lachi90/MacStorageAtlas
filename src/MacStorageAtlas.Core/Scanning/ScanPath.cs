namespace MacStorageAtlas.Core.Scanning;

internal static class ScanPath
{
    public static StringComparison Comparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    public static StringComparer Comparer =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    public static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    public static bool IsWithin(string path, string ancestorPath)
    {
        if (Comparer.Equals(path, ancestorPath))
        {
            return true;
        }

        var prefix = Path.EndsInDirectorySeparator(ancestorPath)
            ? ancestorPath
            : ancestorPath + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, Comparison);
    }

    public static bool IsWithinNormalized(string path, string ancestorPath) =>
        IsWithin(Normalize(path), Normalize(ancestorPath));
}
