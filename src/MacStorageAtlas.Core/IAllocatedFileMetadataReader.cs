namespace MacStorageAtlas.Core;

public interface IAllocatedFileMetadataReader
{
    AllocatedFileMetadata Read(string path);
}
