namespace MacStorageAtlas.Core.Cleanup;

public interface ITrashService
{
    Task MoveToTrashAsync(string path, CancellationToken cancellationToken = default);
}
