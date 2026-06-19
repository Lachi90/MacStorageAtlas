namespace MacStorageAtlas.Core;

public interface ITrashService
{
    Task MoveToTrashAsync(string path, CancellationToken cancellationToken = default);
}
