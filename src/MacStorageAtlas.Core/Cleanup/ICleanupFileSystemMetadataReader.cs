namespace MacStorageAtlas.Core.Cleanup;

public interface ICleanupFileSystemMetadataReader
{
    bool TryReadSnapshot(string path, out CleanupFileSystemSnapshot snapshot);
}
