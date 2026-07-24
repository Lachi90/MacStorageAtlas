using MacStorageAtlas.Core;

namespace MacStorageAtlas.Tests;

public class DiskItemSorterTests
{
    [Test]
    public void SortBySizeDescendingSortsEveryLevelRecursively()
    {
        var root = Directory("root", 36);
        var smallDirectory = Directory("small", 6);
        var largeDirectory = Directory("large", 30);

        root.AddChild(smallDirectory);
        root.AddChild(largeDirectory);
        root.AddChild(File("middle.bin", 12));

        smallDirectory.AddChild(File("smallest.bin", 1));
        smallDirectory.AddChild(File("larger.bin", 5));

        largeDirectory.AddChild(File("small.bin", 3));
        largeDirectory.AddChild(File("largest.bin", 20));
        largeDirectory.AddChild(File("medium.bin", 7));

        DiskItemSorter.SortBySizeDescending(root);

        Assert.Multiple(() =>
        {
            Assert.That(root.Children.Select(item => item.SizeBytes),
                Is.EqualTo(new long[] { 30, 12, 6 }));
            Assert.That(smallDirectory.Children.Select(item => item.SizeBytes),
                Is.EqualTo(new long[] { 5, 1 }));
            Assert.That(largeDirectory.Children.Select(item => item.SizeBytes),
                Is.EqualTo(new long[] { 20, 7, 3 }));
        });
    }

    private static DiskItem Directory(string name, long size) =>
        new(name, $"/{name}", isDirectory: true) { SizeBytes = size };

    private static DiskItem File(string name, long size) =>
        new(name, $"/{name}", isDirectory: false) { SizeBytes = size };
}
