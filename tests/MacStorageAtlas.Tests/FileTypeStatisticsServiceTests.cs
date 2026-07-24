using MacStorageAtlas.Core;

namespace MacStorageAtlas.Tests;

public class FileTypeStatisticsServiceTests
{
    [Test]
    public void CalculateGroupsExtensionsCaseInsensitivelyAndHandlesExtensionlessFiles()
    {
        var root = Directory("root");
        var nested = Directory("nested");
        root.AddChild(File("first.TXT", 10));
        root.AddChild(File("README", 20));
        root.AddChild(nested);
        nested.AddChild(File("second.txt", 30));
        nested.AddChild(File("archive.zip", 40));

        var summaries = new FileTypeStatisticsService().Calculate(root);

        Assert.Multiple(() =>
        {
            Assert.That(summaries.Select(summary => summary.Extension),
                Is.EquivalentTo(new[] { ".txt", ".zip", FileTypeStatisticsService.NoExtensionLabel }));
            Assert.That(summaries.Single(summary => summary.Extension == ".txt").FileCount,
                Is.EqualTo(2));
            Assert.That(summaries.Single(
                    summary => summary.Extension == FileTypeStatisticsService.NoExtensionLabel).FileCount,
                Is.EqualTo(1));
        });
    }

    [Test]
    public void CalculateAggregatesFileSizesAcrossTheTree()
    {
        var root = Directory("root");
        var nested = Directory("nested");
        root.AddChild(File("photo.jpg", 1_024));
        root.AddChild(nested);
        nested.AddChild(File("thumbnail.JPG", 512));
        nested.AddChild(File("data.bin", 2_048));

        var summaries = new FileTypeStatisticsService().Calculate(root);

        Assert.Multiple(() =>
        {
            Assert.That(summaries.Single(summary => summary.Extension == ".jpg").TotalSizeBytes,
                Is.EqualTo(1_536));
            Assert.That(summaries.Single(summary => summary.Extension == ".bin").TotalSizeBytes,
                Is.EqualTo(2_048));
            Assert.That(summaries.Sum(summary => summary.TotalSizeBytes), Is.EqualTo(3_584));
            Assert.That(summaries.Sum(summary => summary.FileCount), Is.EqualTo(3));
        });
    }

    private static DiskItem Directory(string name) =>
        new(name, $"/{name}", isDirectory: true);

    private static DiskItem File(string name, long size) =>
        new(name, $"/{name}", isDirectory: false) { SizeBytes = size };
}
