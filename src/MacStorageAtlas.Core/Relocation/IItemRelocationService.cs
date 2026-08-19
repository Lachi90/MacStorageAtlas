namespace MacStorageAtlas.Core.Relocation;

public interface IItemRelocationService
{
    Task MoveAsync(
        string sourcePath,
        string destinationDirectoryPath,
        CancellationToken cancellationToken = default);

    Task CopyAsync(
        string sourcePath,
        string destinationDirectoryPath,
        CancellationToken cancellationToken = default);
}
