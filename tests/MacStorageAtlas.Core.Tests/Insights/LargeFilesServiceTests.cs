using MacStorageAtlas.Core.Insights;
using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.Core.Tests.Insights;

public class LargeFilesServiceTests
{
    private readonly LargeFilesService _service = new();

    [Test]
    public void GetLargestFilesReturnsFilesOrderedBySizeDescending()
    {
        var root = new DiskItem("root", "/root", isDirectory: true);
        var nested = new DiskItem("nested", "/root/nested", isDirectory: true);
        var small = File("small.bin", "/root/small.bin", 10);
        var large = File("large.bin", "/root/nested/large.bin", 500);
        var medium = File("medium.bin", "/root/nested/medium.bin", 200);
        root.AddChild(small);
        nested.AddChild(large);
        nested.AddChild(medium);
        root.AddChild(nested);

        var largest = _service.GetLargestFiles(root);

        Assert.Multiple(() =>
        {
            Assert.That(largest.Select(file => file.Name),
                Is.EqualTo(new[] { "large.bin", "medium.bin", "small.bin" }));
            Assert.That(largest, Has.None.Matches<DiskItem>(item => item.IsDirectory));
        });
    }

    [Test]
    public void GetLargestFilesHonorsTheRequestedLimit()
    {
        var root = new DiskItem("root", "/root", isDirectory: true);
        root.AddChild(File("a", "/root/a", 30));
        root.AddChild(File("b", "/root/b", 20));
        root.AddChild(File("c", "/root/c", 10));

        var largest = _service.GetLargestFiles(root, limit: 2);

        Assert.That(largest.Select(file => file.Name), Is.EqualTo(new[] { "a", "b" }));
    }

    [Test]
    public void GetLargestFilesBreaksSizeTiesByPath()
    {
        var root = new DiskItem("root", "/root", isDirectory: true);
        root.AddChild(File("z", "/root/z", 50));
        root.AddChild(File("a", "/root/a", 50));

        var largest = _service.GetLargestFiles(root);

        Assert.That(largest.Select(file => file.Path), Is.EqualTo(new[] { "/root/a", "/root/z" }));
    }

    [Test]
    public void GetLargestFilesReturnsEmptyForADirectoryWithoutFiles()
    {
        var root = new DiskItem("root", "/root", isDirectory: true);
        root.AddChild(new DiskItem("empty", "/root/empty", isDirectory: true));

        var largest = _service.GetLargestFiles(root);

        Assert.That(largest, Is.Empty);
    }

    [Test]
    public void GetLargestFilesRanksSharedPathByItsCountedContribution()
    {
        var root = new DiskItem("root", "/root", isDirectory: true);
        var counted = File("counted.bin", "/root/counted.bin", 4096);
        var shared = new DiskItem(
            "shared.bin",
            "/root/shared.bin",
            isDirectory: false)
        {
            SizeBytes = 0,
            MeasuredSizeBytes = 4096,
            SharedSizeBytes = 4096
        };
        root.AddChild(shared);
        root.AddChild(counted);

        var largest = _service.GetLargestFiles(root);

        Assert.Multiple(() =>
        {
            Assert.That(largest, Is.EqualTo(new[] { counted, shared }));
            Assert.That(shared.MeasuredSizeBytes, Is.EqualTo(4096));
        });
    }

    [Test]
    public void GetLargestFilesRanksPartiallySharedCloneByCountedContribution()
    {
        var root = new DiskItem("root", "/root", isDirectory: true);
        var ordinary = File("ordinary.bin", "/root/ordinary.bin", 2048);
        var clone = new DiskItem(
            "clone.bin",
            "/root/clone.bin",
            isDirectory: false)
        {
            SizeBytes = 1024,
            MeasuredSizeBytes = 5120,
            SharedSizeBytes = 4096
        };
        root.AddChild(clone);
        root.AddChild(ordinary);

        var largest = _service.GetLargestFiles(root);

        Assert.That(largest, Is.EqualTo(new[] { ordinary, clone }));
    }

    [Test]
    public void ScopedLargestFilesIncludeOnlyFilesInsideTheScopeAndItsDescendants()
    {
        var (root, downloads, _) = ScopedTree();

        var largest = _service.GetLargestFiles(
            downloads,
            include: null,
            LargeFilesService.DefaultLimit,
            CancellationToken.None);

        Assert.That(
            largest.Select(file => file.Name),
            Is.EqualTo(new[] { "movie.mov", "deep.zip", "setup.dmg" }));
        Assert.That(_service.GetLargestFiles(root), Has.Count.EqualTo(5));
    }

    [Test]
    public void ScopedLargestFilesOfAFileScopeContainOnlyThatFile()
    {
        var file = File("single.bin", "/root/single.bin", 42);

        var largest = _service.GetLargestFiles(
            file,
            include: null,
            LargeFilesService.DefaultLimit,
            CancellationToken.None);

        Assert.That(largest, Is.EqualTo(new[] { file }));
    }

    [Test]
    public void ScopedLargestFilesOfAnEmptyScopeAreEmpty()
    {
        var empty = new DiskItem("empty", "/root/empty", isDirectory: true);
        empty.AddChild(new DiskItem("nested", "/root/empty/nested", isDirectory: true));

        var largest = _service.GetLargestFiles(
            empty,
            include: null,
            LargeFilesService.DefaultLimit,
            CancellationToken.None);

        Assert.That(largest, Is.Empty);
    }

    [Test]
    public void ScopedLargestFilesSkipFilesTheInclusionPredicateExcludes()
    {
        var (_, downloads, _) = ScopedTree();

        var largest = _service.GetLargestFiles(
            downloads,
            item => item.IsDirectory || item.Name.EndsWith(".zip", StringComparison.Ordinal),
            LargeFilesService.DefaultLimit,
            CancellationToken.None);

        Assert.That(largest.Select(file => file.Name), Is.EqualTo(new[] { "deep.zip" }));
    }

    [Test]
    public void ScopedLargestFilesDoNotDescendIntoDirectoriesThePredicateExcludes()
    {
        var (_, downloads, nested) = ScopedTree();
        var visited = new List<DiskItem>();

        var largest = _service.GetLargestFiles(
            downloads,
            item =>
            {
                visited.Add(item);
                return !ReferenceEquals(item, nested);
            },
            LargeFilesService.DefaultLimit,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(
                largest.Select(file => file.Name),
                Is.EqualTo(new[] { "movie.mov", "setup.dmg" }));
            Assert.That(visited, Has.None.Matches<DiskItem>(item => item.Name == "deep.zip"));
        });
    }

    [Test]
    public void ScopedLargestFilesHonorTheLimitInsideTheScope()
    {
        var (_, downloads, _) = ScopedTree();

        var largest = _service.GetLargestFiles(
            downloads,
            include: null,
            limit: 2,
            CancellationToken.None);

        Assert.That(
            largest.Select(file => file.Name),
            Is.EqualTo(new[] { "movie.mov", "deep.zip" }));
    }

    [Test]
    public void ScopedLargestFilesStopWhenCancelled()
    {
        var (_, downloads, _) = ScopedTree();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => _service.GetLargestFiles(
            downloads,
            include: null,
            LargeFilesService.DefaultLimit,
            cancellation.Token));
    }

    [Test]
    public void ScopedLargestFilesStopWhenCancelledDuringTheWalk()
    {
        var (_, downloads, nested) = ScopedTree();
        using var cancellation = new CancellationTokenSource();

        Assert.Throws<OperationCanceledException>(() => _service.GetLargestFiles(
            downloads,
            item =>
            {
                if (ReferenceEquals(item, nested))
                {
                    cancellation.Cancel();
                }

                return true;
            },
            LargeFilesService.DefaultLimit,
            cancellation.Token));
    }

    private static (DiskItem Root, DiskItem Downloads, DiskItem Nested) ScopedTree()
    {
        var root = new DiskItem("root", "/root", isDirectory: true);
        var downloads = new DiskItem("Downloads", "/root/Downloads", isDirectory: true);
        var nested = new DiskItem("Archive", "/root/Downloads/Archive", isDirectory: true);
        var documents = new DiskItem("Documents", "/root/Documents", isDirectory: true);
        nested.AddChild(File("deep.zip", "/root/Downloads/Archive/deep.zip", 300));
        downloads.AddChild(File("setup.dmg", "/root/Downloads/setup.dmg", 200));
        downloads.AddChild(File("movie.mov", "/root/Downloads/movie.mov", 400));
        downloads.AddChild(nested);
        documents.AddChild(File("huge.pdf", "/root/Documents/huge.pdf", 900));
        root.AddChild(downloads);
        root.AddChild(documents);
        root.AddChild(File("top.txt", "/root/top.txt", 1000));
        return (root, downloads, nested);
    }

    private static DiskItem File(string name, string path, long size) =>
        new(name, path, isDirectory: false) { SizeBytes = size };
}
