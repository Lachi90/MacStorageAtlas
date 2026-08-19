namespace MacStorageAtlas.Core.Relocation;

public interface IRelocationDestinationProbe
{
    bool Exists(string path);

    bool IsDirectory(string path);

    bool IsWritable(string path);

    RelocationFreeSpace GetFreeSpace(string path);
}
