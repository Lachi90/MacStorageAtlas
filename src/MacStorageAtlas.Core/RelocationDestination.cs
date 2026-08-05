namespace MacStorageAtlas.Core;

public sealed record RelocationDestination(string Path, string NormalizedPath)
{
    public static RelocationDestination FromPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return new RelocationDestination(
            path,
            CleanupProtectedPathPolicy.NormalizePath(path));
    }

    public string CombineWith(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return System.IO.Path.Combine(NormalizedPath, name);
    }
}
