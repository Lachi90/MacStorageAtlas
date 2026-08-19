namespace MacStorageAtlas.Core.Scanning;

public interface IAllocatedFileMetadataReader
{
    AllocatedFileMetadata Read(string path);
}
