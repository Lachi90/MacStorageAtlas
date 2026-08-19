using MacStorageAtlas.Platform.Mac;

namespace MacStorageAtlas.Platform.Mac.Tests;

public class MacQuickLookServiceTests
{
    [Test]
    public void PreviewReturnsFalseForMissingPath()
    {
        var presenter = new FakeQuickLookPresenter();
        var service = new MacQuickLookService(presenter);
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            $"MacStorageAtlas-{Guid.NewGuid():N}");

        var result = service.Preview(missingPath);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(presenter.PreviewedPaths, Is.Empty);
        });
    }

    [Test]
    public void PreviewReturnsFalseWhenQuickLookCannotStart()
    {
        using var temporary = new TemporaryFile();
        var presenter = new FakeQuickLookPresenter { Result = false };
        var service = new MacQuickLookService(presenter);

        var result = service.Preview(temporary.Path);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(presenter.PreviewedPaths, Is.EqualTo(new[] { temporary.Path }));
        });
    }

    [Test]
    public void PreviewReturnsFalseWhenQuickLookLaunchThrows()
    {
        using var temporary = new TemporaryFile();
        var presenter = new FakeQuickLookPresenter
        {
            Exception = new InvalidOperationException("Quick Look unavailable.")
        };
        var service = new MacQuickLookService(presenter);

        var result = service.Preview(temporary.Path);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.False);
            Assert.That(presenter.PreviewedPaths, Is.EqualTo(new[] { temporary.Path }));
        });
    }

    private sealed class FakeQuickLookPresenter : IQuickLookPresenter
    {
        public bool Result { get; init; } = true;

        public Exception? Exception { get; init; }

        public List<string> PreviewedPaths { get; } = [];

        public bool Preview(string path)
        {
            PreviewedPaths.Add(path);

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
            File.WriteAllText(Path, "preview");
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
