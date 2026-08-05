namespace MacStorageAtlas.Core;

public interface IRelocationDestinationProbe
{
    bool Exists(string path);

    bool IsDirectory(string path);

    bool IsWritable(string path);

    RelocationFreeSpace GetFreeSpace(string path);
}
