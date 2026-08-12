namespace MacStorageAtlas.Core.Cleanup;

public sealed class CleanupFileSystemMetadataReader : ICleanupFileSystemMetadataReader
{
    public bool TryReadSnapshot(string path, out CleanupFileSystemSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            if (File.Exists(path))
            {
                var fileInfo = new FileInfo(path);
                snapshot = new CleanupFileSystemSnapshot(
                    path,
                    IsDirectory: false,
                    fileInfo.Length,
                    fileInfo.Length);
                return true;
            }

            if (Directory.Exists(path))
            {
                snapshot = new CleanupFileSystemSnapshot(
                    path,
                    IsDirectory: true,
                    SizeBytes: 0,
                    MeasuredSizeBytes: 0);
                return true;
            }
        }
        catch (Exception)
        {
        }

        snapshot = null!;
        return false;
    }
}
