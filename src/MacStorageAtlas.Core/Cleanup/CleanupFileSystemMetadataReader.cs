using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Cleanup;

public sealed class CleanupFileSystemMetadataReader(
    IAllocatedFileMetadataReader? allocatedMetadataReader = null)
    : ICleanupFileSystemMetadataReader
{
    public bool TryReadSnapshot(string path, out CleanupFileSystemSnapshot snapshot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            if (File.Exists(path))
            {
                var length = new FileInfo(path).Length;
                snapshot = new CleanupFileSystemSnapshot(
                    path,
                    IsDirectory: false,
                    length,
                    TryReadAllocatedSize(path) ?? length);
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

    private long? TryReadAllocatedSize(string path)
    {
        if (allocatedMetadataReader is null)
        {
            return null;
        }

        try
        {
            return allocatedMetadataReader.Read(path).AllocatedSizeBytes;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
