using MacStorageAtlas.Platform.Mac;

namespace MacStorageAtlas.Platform.Mac.Tests;

public class MacFileRevealServiceTests
{
    [Test]
    public void RevealReturnsFalseForMissingPath()
    {
        var presenter = new FakeFileRevealPresenter();
        var service = new MacFileRevealService(presenter);
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            $"MacStorageAtlas-{Guid.NewGuid():N}");

        var result = service.Reveal(missingPath);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(presenter.RevealedPaths, Is.Empty);
        });
    }

    [Test]
    public void RevealAsksThePlatformToSelectAnExistingItem()
    {
        using var temporary = new TemporaryFile();
        var presenter = new FakeFileRevealPresenter();
        var service = new MacFileRevealService(presenter);

        var result = service.Reveal(temporary.Path);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.True);
            Assert.That(presenter.RevealedPaths, Is.EqualTo(new[] { temporary.Path }));
        });
    }

    [Test]
    public void RevealReturnsFalseWhenThePlatformCannotSelectTheItem()
    {
        using var temporary = new TemporaryFile();
        var presenter = new FakeFileRevealPresenter { Result = false };
        var service = new MacFileRevealService(presenter);

        var result = service.Reveal(temporary.Path);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(presenter.RevealedPaths, Is.EqualTo(new[] { temporary.Path }));
        });
    }

    [Test]
    public void RevealReturnsFalseWhenThePlatformThrows()
    {
        using var temporary = new TemporaryFile();
        var presenter = new FakeFileRevealPresenter
        {
            Exception = new InvalidOperationException("Finder unavailable.")
        };
        var service = new MacFileRevealService(presenter);

        var result = service.Reveal(temporary.Path);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(presenter.RevealedPaths, Is.EqualTo(new[] { temporary.Path }));
        });
    }

    [Test]
    public void TheWorkspaceRevealApiIsAvailableOnMacOs()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Ignore("The workspace reveal API is only available on macOS.");
        }

        Assert.That(FinderWorkspace.IsAvailable, Is.True);
    }

    private sealed class FakeFileRevealPresenter : IFileRevealPresenter
    {
        public bool Result { get; init; } = true;

        public Exception? Exception { get; init; }

        public List<string> RevealedPaths { get; } = [];

        public bool Reveal(string path)
        {
            RevealedPaths.Add(path);

            if (Exception is not null)
            {
                throw Exception;
            }

            return Result;
        }
    }

    private sealed class TemporaryFile : IDisposable
    {
        public TemporaryFile()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"MacStorageAtlas-{Guid.NewGuid():N}.txt");
            File.WriteAllText(Path, "reveal");
        }

        public string Path { get; }

        public void Dispose()
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
        }
    }
}
