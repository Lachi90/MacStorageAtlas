namespace MacStorageAtlas.Core.Scanning;

public interface IEquivalentPathProvider
{
    IReadOnlyList<EquivalentPath> GetEquivalentPaths();
}
