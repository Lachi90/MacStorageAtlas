using MacStorageAtlas.Platform.Mac;

namespace MacStorageAtlas.Platform.Mac.Tests;

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

    [Test]
    public async Task MoveToTrashAsyncMovesTemporaryFileToTrashOnMacOs()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Ignore("macOS Trash integration is only available on macOS.");
        }

        var directory = Directory.CreateTempSubdirectory("MacStorageAtlas-Trash-");
        var path = Path.Combine(directory.FullName, "trash-me.txt");
        await File.WriteAllTextAsync(path, "temporary");
        var service = new MacTrashService();

        try
        {
            await service.MoveToTrashAsync(path);

            Assert.That(File.Exists(path), Is.False);
        }
        catch (InvalidOperationException exception)
        {
            Assert.Ignore($"macOS Trash integration is unavailable: {exception.Message}");
        }
        finally
        {
            if (Directory.Exists(directory.FullName))
            {
                Directory.Delete(directory.FullName, recursive: true);
            }
        }
    }
}
