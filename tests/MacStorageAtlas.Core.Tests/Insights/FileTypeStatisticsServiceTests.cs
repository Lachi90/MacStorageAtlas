using MacStorageAtlas.Core.Insights;
using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.Core.Tests.Insights;

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

    [Test]
    public void CalculateCountsSharedPathButDoesNotDuplicateItsStorageWeight()
    {
        var root = Directory("root");
        root.AddChild(File("counted.bin", 4096));
        root.AddChild(new DiskItem(
            "shared.bin",
            "/shared.bin",
            isDirectory: false)
        {
            SizeBytes = 0,
            MeasuredSizeBytes = 4096,
            SharedSizeBytes = 4096
        });

        var summary = new FileTypeStatisticsService().Calculate(root).Single();

        Assert.Multiple(() =>
        {
            Assert.That(summary.FileCount, Is.EqualTo(2));
            Assert.That(summary.TotalSizeBytes, Is.EqualTo(4096));
        });
    }

    [Test]
    public void CalculateUsesPositiveNonDataContributionFromSharedClone()
    {
        var root = Directory("root");
        root.AddChild(File("representative.bin", 5120));
        root.AddChild(new DiskItem(
            "clone.bin",
            "/clone.bin",
            isDirectory: false)
        {
            SizeBytes = 1024,
            MeasuredSizeBytes = 5120,
            SharedSizeBytes = 4096
        });

        var summary = new FileTypeStatisticsService().Calculate(root).Single();

        Assert.Multiple(() =>
        {
            Assert.That(summary.FileCount, Is.EqualTo(2));
            Assert.That(summary.TotalSizeBytes, Is.EqualTo(6144));
        });
    }

    [Test]
    public void ScopedStatisticsCountOnlyFilesInsideTheScopeAndItsDescendants()
    {
        var (_, scope, _) = ScopedTree();

        var summaries = new FileTypeStatisticsService().Calculate(
            scope,
            include: null,
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(
                summaries.Select(summary => summary.Extension),
                Is.EqualTo(new[] { ".mov", ".zip" }));
            Assert.That(summaries[0].FileCount, Is.EqualTo(2));
            Assert.That(summaries[0].TotalSizeBytes, Is.EqualTo(500));
        });
    }

    [Test]
    public void ScopedStatisticsOfAFileScopeDescribeOnlyThatFile()
    {
        var summaries = new FileTypeStatisticsService().Calculate(
            File("photo.HEIC", 64),
            include: null,
            CancellationToken.None);

        Assert.That(
            summaries.Select(summary => (summary.Extension, summary.FileCount)),
            Is.EqualTo(new[] { (".heic", 1L) }));
    }

    [Test]
    public void ScopedStatisticsOfAnEmptyScopeAreEmpty()
    {
        var summaries = new FileTypeStatisticsService().Calculate(
            Directory("empty"),
            include: null,
            CancellationToken.None);

        Assert.That(summaries, Is.Empty);
    }

    [Test]
    public void ScopedStatisticsSkipFilesAndDirectoriesThePredicateExcludes()
    {
        var (_, scope, nested) = ScopedTree();

        var withoutNested = new FileTypeStatisticsService().Calculate(
            scope,
            item => !ReferenceEquals(item, nested),
            CancellationToken.None);
        var withoutMovies = new FileTypeStatisticsService().Calculate(
            scope,
            item => item.IsDirectory || !item.Name.EndsWith(".mov", StringComparison.Ordinal),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(
                withoutNested.Select(summary => (summary.Extension, summary.FileCount)),
                Is.EqualTo(new[] { (".mov", 1L) }));
            Assert.That(
                withoutMovies.Select(summary => summary.Extension),
                Is.EqualTo(new[] { ".zip" }));
        });
    }

    [Test]
    public void ScopedStatisticsStopWhenCancelled()
    {
        var (_, scope, _) = ScopedTree();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => new FileTypeStatisticsService().Calculate(
            scope,
            include: null,
            cancellation.Token));
    }

    private static (DiskItem Root, DiskItem Scope, DiskItem Nested) ScopedTree()
    {
        var root = Directory("root");
        var scope = Directory("scope");
        var nested = Directory("nested");
        nested.AddChild(File("clip.mov", 400));
        nested.AddChild(File("bundle.zip", 50));
        scope.AddChild(File("intro.mov", 100));
        scope.AddChild(nested);
        root.AddChild(scope);
        root.AddChild(File("outside.pdf", 900));
        return (root, scope, nested);
    }

    private static DiskItem Directory(string name) =>
        new(name, $"/{name}", isDirectory: true);

    private static DiskItem File(string name, long size) =>
        new(name, $"/{name}", isDirectory: false) { SizeBytes = size };
}
