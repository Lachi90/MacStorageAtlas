using MacStorageAtlas.Platform.Mac;

namespace MacStorageAtlas.Tests;

public class MacFileRevealServiceTests
{
    [Test]
    public void RevealReturnsFalseForMissingPath()
    {
        var service = new MacFileRevealService();
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            $"MacStorageAtlas-{Guid.NewGuid():N}");

        Assert.That(service.Reveal(missingPath), Is.False);
    }
}
