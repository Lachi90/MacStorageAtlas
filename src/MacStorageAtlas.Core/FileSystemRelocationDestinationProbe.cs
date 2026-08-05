namespace MacStorageAtlas.Core;

public sealed class FileSystemRelocationDestinationProbe : IRelocationDestinationProbe
{
    public bool Exists(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return Directory.Exists(path) || File.Exists(path);
    }

    public bool IsDirectory(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        return Directory.Exists(path);
    }

    public bool IsWritable(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var probePath = Path.Combine(path, $".macstorageatlas-{Guid.NewGuid():N}");

        try
        {
            using (File.Create(probePath))
            {
            }

            File.Delete(probePath);
            return true;
        }
        catch (Exception)
        {
            TryDeleteProbe(probePath);
            return false;
        }
    }

    public RelocationFreeSpace GetFreeSpace(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            var availableBytes = new DriveInfo(path).AvailableFreeSpace;
            return availableBytes > 0
                ? RelocationFreeSpace.FromBytes(availableBytes)
                : RelocationFreeSpace.Unknown;
        }
        catch (Exception)
        {
            return RelocationFreeSpace.Unknown;
        }
    }

    private static void TryDeleteProbe(string probePath)
    {
        try
        {
            if (File.Exists(probePath))
            {
                File.Delete(probePath);
            }
        }
        catch (Exception)
        {
        }
    }
}
