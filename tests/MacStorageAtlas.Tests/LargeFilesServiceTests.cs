using MacStorageAtlas.Core;

namespace MacStorageAtlas.Tests;

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

    private static DiskItem File(string name, string path, long size) =>
        new(name, path, isDirectory: false) { SizeBytes = size };
}
