namespace MacStorageAtlas.Core;

public interface ICleanupFileSystemMetadataReader
{
    bool TryReadSnapshot(string path, out CleanupFileSystemSnapshot snapshot);
}
