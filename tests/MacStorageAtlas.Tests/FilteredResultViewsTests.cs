using MacStorageAtlas.Core;

namespace MacStorageAtlas.Tests;

public class FilteredResultViewsTests
{
    private readonly DiskItemFilterEvaluator _evaluator = new();
    private readonly LargeFilesService _largeFilesService = new();
    private readonly FileTypeStatisticsService _fileTypeStatisticsService = new();

    [Test]
    public void LargestFilesOverAFilterResultReturnsOnlyMatches()
    {
        var root = CreateTree();
        var result = _evaluator.Evaluate(root, new DiskItemFilter { Extensions = [".mov"] });

        var largest = _largeFilesService.GetLargestFiles(result.MatchedFiles);

        Assert.That(
            largest.Select(file => file.Name),
            Is.EqualTo(["big.mov", "small.mov"]));
    }

    [Test]
    public void LargestFilesOverAFilterResultKeepsSizeOrdering()
    {
        var root = CreateTree();
        var result = _evaluator.Evaluate(root, DiskItemFilter.Empty);

        var largest = _largeFilesService.GetLargestFiles(result.MatchedFiles);

        Assert.That(
            largest.Select(file => file.SizeBytes),
            Is.Ordered.Descending);
    }

    [Test]
    public void LargestFilesOverAnEmptyFilterResultMatchesTheWholeTreeResult()
    {
        var root = CreateTree();
        var result = _evaluator.Evaluate(root, DiskItemFilter.Empty);

        var fromTree = _largeFilesService.GetLargestFiles(root);
        var fromMatches = _largeFilesService.GetLargestFiles(result.MatchedFiles);

        Assert.That(
            fromMatches.Select(file => file.Path),
            Is.EqualTo(fromTree.Select(file => file.Path)));
    }

    [Test]
    public void LargestFilesRespectsTheLimitOverAFilterResult()
    {
        var root = CreateTree();
        var result = _evaluator.Evaluate(root, DiskItemFilter.Empty);

        var largest = _largeFilesService.GetLargestFiles(result.MatchedFiles, limit: 2);

        Assert.That(largest, Has.Count.EqualTo(2));
    }

    [Test]
    public void LargestFilesReturnsNothingForAZeroLimitOverAFilterResult()
    {
        var root = CreateTree();
        var result = _evaluator.Evaluate(root, DiskItemFilter.Empty);

        Assert.That(
            _largeFilesService.GetLargestFiles(result.MatchedFiles, limit: 0),
            Is.Empty);
    }

    [Test]
    public void FileTypeSummariesOverAFilterResultDescribeOnlyMatches()
    {
        var root = CreateTree();
        var result = _evaluator.Evaluate(root, new DiskItemFilter { Extensions = [".mov"] });

        var summaries = _fileTypeStatisticsService.Calculate(result.MatchedFiles);

        Assert.Multiple(() =>
        {
            Assert.That(summaries.Select(summary => summary.Extension), Is.EqualTo([".mov"]));
            Assert.That(summaries.Single().FileCount, Is.EqualTo(2));
            Assert.That(summaries.Single().TotalSizeBytes, Is.EqualTo(9216));
        });
    }

    [Test]
    public void FileTypeSummariesOverAnEmptyFilterResultMatchTheWholeTreeResult()
    {
        var root = CreateTree();
        var result = _evaluator.Evaluate(root, DiskItemFilter.Empty);

        var fromTree = _fileTypeStatisticsService.Calculate(root);
        var fromMatches = _fileTypeStatisticsService.Calculate(result.MatchedFiles);

        Assert.That(fromMatches, Is.EqualTo(fromTree));
    }

    [Test]
    public void FileTypeSummariesOverAFilterResultAgreeWithTheMatchedTotal()
    {
        var root = CreateTree();
        var result = _evaluator.Evaluate(root, new DiskItemFilter { MinimumSizeBytes = 1024 });

        var summaries = _fileTypeStatisticsService.Calculate(result.MatchedFiles);

        Assert.That(
            summaries.Sum(summary => summary.TotalSizeBytes),
            Is.EqualTo(result.MatchedBytes));
    }

    [Test]
    public void FilesWithoutAnExtensionKeepTheirLabelOverAFilterResult()
    {
        var root = new DiskItem("root", "/root", isDirectory: true);
        root.AddChild(new DiskItem("README", "/root/README", isDirectory: false)
        {
            SizeBytes = 64
        });
        var result = _evaluator.Evaluate(root, DiskItemFilter.Empty);

        var summaries = _fileTypeStatisticsService.Calculate(result.MatchedFiles);

        Assert.That(
            summaries.Single().Extension,
            Is.EqualTo(FileTypeStatisticsService.NoExtensionLabel));
    }

    [Test]
    public void EmptyMatchesProduceEmptyResultViews()
    {
        var root = CreateTree();
        var result = _evaluator.Evaluate(
            root,
            new DiskItemFilter { Extensions = [".nothing"] });

        Assert.Multiple(() =>
        {
            Assert.That(_largeFilesService.GetLargestFiles(result.MatchedFiles), Is.Empty);
            Assert.That(_fileTypeStatisticsService.Calculate(result.MatchedFiles), Is.Empty);
        });
    }

    private static DiskItem CreateTree()
    {
        var root = new DiskItem("root", "/Users/test", isDirectory: true);

        var media = new DiskItem("Media", "/Users/test/Media", isDirectory: true);
        media.AddChild(new DiskItem("big.mov", "/Users/test/Media/big.mov", isDirectory: false)
        {
            SizeBytes = 8192
        });
        media.AddChild(new DiskItem("small.mov", "/Users/test/Media/small.mov", isDirectory: false)
        {
            SizeBytes = 1024
        });
        media.SizeBytes = 9216;

        var documents = new DiskItem("Documents", "/Users/test/Documents", isDirectory: true);
        documents.AddChild(new DiskItem(
            "report.pdf",
            "/Users/test/Documents/report.pdf",
            isDirectory: false)
        {
            SizeBytes = 2048
        });
        documents.AddChild(new DiskItem(
            "notes.txt",
            "/Users/test/Documents/notes.txt",
            isDirectory: false)
        {
            SizeBytes = 512
        });
        documents.SizeBytes = 2560;

        root.AddChild(media);
        root.AddChild(documents);
        root.SizeBytes = 11776;
        return root;
    }
}
