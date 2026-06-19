using MacStorageAtlas.Platform.Mac;

namespace MacStorageAtlas.Tests;

public class MacTrashServiceTests
{
    [Test]
    public void MoveToTrashAsyncRejectsAMissingPath()
    {
        var service = new MacTrashService();
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            $"MacStorageAtlas-{Guid.NewGuid():N}");

        Assert.That(
            async () => await service.MoveToTrashAsync(missingPath),
            Throws.TypeOf<FileNotFoundException>());
    }
}
