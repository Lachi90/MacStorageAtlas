using System.Threading;
using System.Threading.Tasks;
using MacStorageAtlas.Core.Relocation;

namespace MacStorageAtlas.App.Services;

internal sealed class NullItemRelocationService : IItemRelocationService
{
    public Task MoveAsync(
        string sourcePath,
        string destinationDirectoryPath,
        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task CopyAsync(
        string sourcePath,
        string destinationDirectoryPath,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
