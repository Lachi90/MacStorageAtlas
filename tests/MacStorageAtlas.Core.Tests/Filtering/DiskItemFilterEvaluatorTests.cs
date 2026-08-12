using MacStorageAtlas.Core.Filtering;
using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.Core.Tests.Filtering;

public class DiskItemFilterEvaluatorTests
{
    private static readonly DateTimeOffset Reference =
        new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly DiskItemFilterEvaluator _evaluator = new();

    private FilterResult EvaluateAt(DiskItem root, DiskItemFilter filter) =>
        _evaluator.Evaluate(root, filter, Reference);

    [Test]
    public void AnEmptyFilterMatchesEveryFile()
    {
        var root = CreateTree();

        var result = EvaluateAt(root, DiskItemFilter.Empty);

        Assert.Multiple(() =>
        {
            Assert.That(result.MatchCount, Is.EqualTo(4));
            Assert.That(result.IsFilterActive, Is.False);
        });
    }

    [Test]
    public void TextMatchesNameOrPathIgnoringCase()
    {
        var root = CreateTree();

        var byName = EvaluateAt(root, new DiskItemFilter { TextTerm = "REPORT" });
        var byPath = EvaluateAt(root, new DiskItemFilter { TextTerm = "/photos/" });

        Assert.Multiple(() =>
        {
            Assert.That(byName.MatchedFiles.Select(file => file.Name), Is.EqualTo(["report.pdf"]));
            Assert.That(
                byPath.MatchedFiles.Select(file => file.Name),
                Is.EquivalentTo(new[] { "holiday.jpg", "clip.mov" }));
        });
    }

    [Test]
    public void ADirectoryNameInTheTextTermScopesToItsSubtree()
    {
        var root = CreateTree();

        var result = EvaluateAt(
            root,
            new DiskItemFilter { TextTerm = "Photos", MinimumSizeBytes = 8192 });

        Assert.That(result.MatchedFiles.Select(file => file.Name), Is.EqualTo(["clip.mov"]));
    }

    [Test]
    public void MinimumSizeIsInclusive()
    {
        var root = CreateTree();

        var result = EvaluateAt(root, new DiskItemFilter { MinimumSizeBytes = 2048 });

        Assert.That(
            result.MatchedFiles.Select(file => file.Name),
            Is.EquivalentTo(new[] { "report.pdf", "holiday.jpg", "clip.mov" }));
    }

    [Test]
    public void MaximumSizeIsInclusive()
    {
        var root = CreateTree();

        var result = EvaluateAt(root, new DiskItemFilter { MaximumSizeBytes = 2048 });

        Assert.That(
            result.MatchedFiles.Select(file => file.Name),
            Is.EquivalentTo(new[] { "notes.txt", "report.pdf" }));
    }

    [Test]
    public void SizeBoundsCombineAsARange()
    {
        var root = CreateTree();

        var result = EvaluateAt(
            root,
            new DiskItemFilter { MinimumSizeBytes = 2048, MaximumSizeBytes = 4096 });

        Assert.That(
            result.MatchedFiles.Select(file => file.Name),
            Is.EquivalentTo(new[] { "report.pdf", "holiday.jpg" }));
    }

    [Test]
    public void CriteriaCombineWithAndSemantics()
    {
        var root = CreateTree();

        var result = EvaluateAt(
            root,
            new DiskItemFilter { MinimumSizeBytes = 2048, Extensions = [".mov"] });

        Assert.That(result.MatchedFiles.Select(file => file.Name), Is.EqualTo(["clip.mov"]));
    }

    [Test]
    public void AFileSatisfyingOnlyOneCriterionIsNotMatched()
    {
        var root = CreateTree();

        var result = EvaluateAt(
            root,
            new DiskItemFilter { MinimumSizeBytes = 100_000, Extensions = [".mov"] });

        Assert.That(result.MatchCount, Is.Zero);
    }

    [Test]
    public void DirectoriesAreNeverMatchedBySizeCriteria()
    {
        var root = CreateTree();

        var result = EvaluateAt(root, new DiskItemFilter { MinimumSizeBytes = 1 });

        Assert.That(result.MatchedFiles.Any(file => file.IsDirectory), Is.False);
    }

    [Test]
    public void ADirectoryLargerThanTheMinimumWithNoMatchingFileIsNotReported()
    {
        var root = new DiskItem("root", "/root", isDirectory: true);
        var folder = new DiskItem("folder", "/root/folder", isDirectory: true)
        {
            SizeBytes = 10_000
        };
        folder.AddChild(new DiskItem("small.txt", "/root/folder/small.txt", isDirectory: false)
        {
            SizeBytes = 10
        });
        root.AddChild(folder);
        root.SizeBytes = 10_000;

        var result = EvaluateAt(root, new DiskItemFilter { MinimumSizeBytes = 5_000 });

        Assert.Multiple(() =>
        {
            Assert.That(result.MatchCount, Is.Zero);
            Assert.That(result.IsVisible(folder), Is.False);
            Assert.That(result.IsVisible(root), Is.False);
        });
    }

    [Test]
    public void DirectoriesAreNeverMatchedByCategoryOrSharedCriteria()
    {
        var root = CreateTree();

        var byCategory = EvaluateAt(
            root,
            new DiskItemFilter { Categories = [FileCategory.Video] });
        var byShared = EvaluateAt(
            root,
            new DiskItemFilter { SharedStorageOnly = true });

        Assert.Multiple(() =>
        {
            Assert.That(byCategory.MatchedFiles.Any(file => file.IsDirectory), Is.False);
            Assert.That(byShared.MatchedFiles.Any(file => file.IsDirectory), Is.False);
        });
    }

    [Test]
    public void ExtensionCriteriaIgnoreLetterCase()
    {
        var root = CreateTree();

        var result = EvaluateAt(root, new DiskItemFilter { Extensions = ["MOV"] });

        Assert.That(result.MatchedFiles.Select(file => file.Name), Is.EqualTo(["clip.mov"]));
    }

    [Test]
    public void CategoryCriteriaMatchEveryCoveredExtension()
    {
        var root = CreateTree();

        var result = EvaluateAt(
            root,
            new DiskItemFilter { Categories = [FileCategory.Document] });

        Assert.That(
            result.MatchedFiles.Select(file => file.Name),
            Is.EquivalentTo(new[] { "report.pdf", "notes.txt" }));
    }

    [Test]
    public void AFileWithoutAnExtensionIsNotMatchedByACategory()
    {
        var root = new DiskItem("root", "/root", isDirectory: true);
        root.AddChild(new DiskItem("README", "/root/README", isDirectory: false)
        {
            SizeBytes = 10
        });

        var result = EvaluateAt(
            root,
            new DiskItemFilter { Categories = [FileCategory.Document] });

        Assert.That(result.MatchCount, Is.Zero);
    }

    [Test]
    public void SharedStorageCriteriaSelectResultsCountedElsewhere()
    {
        var root = new DiskItem("root", "/root", isDirectory: true);
        var shared = new DiskItem("linked.bin", "/root/linked.bin", isDirectory: false)
        {
            SizeBytes = 0,
            SharedSizeBytes = 4096
        };
        var owned = new DiskItem("owned.bin", "/root/owned.bin", isDirectory: false)
        {
            SizeBytes = 4096
        };
        root.AddChild(shared);
        root.AddChild(owned);

        var result = EvaluateAt(root, new DiskItemFilter { SharedStorageOnly = true });

        Assert.That(result.MatchedFiles.Select(file => file.Name), Is.EqualTo(["linked.bin"]));
    }

    [Test]
    public void AFileCountedElsewhereIsExcludedByAMinimumSize()
    {
        var root = new DiskItem("root", "/root", isDirectory: true);
        var shared = new DiskItem("linked.bin", "/root/linked.bin", isDirectory: false)
        {
            SizeBytes = 0,
            SharedSizeBytes = 4_000_000_000
        };
        root.AddChild(shared);

        var result = EvaluateAt(
            root,
            new DiskItemFilter { MinimumSizeBytes = 1_000_000_000 });

        Assert.That(result.MatchCount, Is.Zero);
    }

    [Test]
    public void AFileWithAnUnknownRequiredDateIsExcludedAndCounted()
    {
        var root = new DiskItem("root", "/root", isDirectory: true);
        var undated = new DiskItem("undated.txt", "/root/undated.txt", isDirectory: false)
        {
            SizeBytes = 100
        };
        root.AddChild(undated);

        var result = EvaluateAt(
            root,
            new DiskItemFilter { ModifiedBefore = new AbsoluteDateCriterion(Reference) });

        Assert.Multiple(() =>
        {
            Assert.That(result.MatchCount, Is.Zero);
            Assert.That(result.UnknownDateExclusionCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void AnUnknownDateDoesNotExcludeWhenNoDateCriterionIsActive()
    {
        var root = new DiskItem("root", "/root", isDirectory: true);
        root.AddChild(new DiskItem("undated.txt", "/root/undated.txt", isDirectory: false)
        {
            SizeBytes = 100
        });

        var result = EvaluateAt(root, new DiskItemFilter { MinimumSizeBytes = 1 });

        Assert.Multiple(() =>
        {
            Assert.That(result.MatchCount, Is.EqualTo(1));
            Assert.That(result.UnknownDateExclusionCount, Is.Zero);
        });
    }

    [Test]
    public void AFileExcludedByAKnownDateIsNotCountedAsUnknown()
    {
        var root = CreateTree();

        var result = EvaluateAt(
            root,
            new DiskItemFilter { ModifiedAfter = new AbsoluteDateCriterion(Reference.AddDays(1)) });

        Assert.Multiple(() =>
        {
            Assert.That(result.MatchCount, Is.Zero);
            Assert.That(result.UnknownDateExclusionCount, Is.Zero);
        });
    }

    [Test]
    public void DateBoundsAreInclusive()
    {
        var root = CreateTree();

        var result = EvaluateAt(
            root,
            new DiskItemFilter
            {
                ModifiedAfter = new AbsoluteDateCriterion(Reference.AddDays(-30)),
                ModifiedBefore = new AbsoluteDateCriterion(Reference.AddDays(-30))
            });

        Assert.That(result.MatchedFiles.Select(file => file.Name), Is.EqualTo(["report.pdf"]));
    }

    [Test]
    public void MatchedBytesSumTheMatchedFiles()
    {
        var root = CreateTree();

        var result = EvaluateAt(root, new DiskItemFilter { Extensions = [".mov"] });

        Assert.That(result.MatchedBytes, Is.EqualTo(8192));
    }

    [Test]
    public void MatchedSubtotalsAreReportedPerDirectory()
    {
        var root = CreateTree();
        var documents = root.Children.Single(child => child.Name == "Documents");

        var result = EvaluateAt(root, new DiskItemFilter { MinimumSizeBytes = 2048 });

        Assert.Multiple(() =>
        {
            Assert.That(result.MatchedBytesFor(documents), Is.EqualTo(2048));
            Assert.That(result.MatchedBytesFor(root), Is.EqualTo(14336));
        });
    }

    [Test]
    public void AVisibleDirectoryIsAnAncestorOfAMatch()
    {
        var root = CreateTree();
        var documents = root.Children.Single(child => child.Name == "Documents");
        var photos = root.Children.Single(child => child.Name == "Photos");

        var result = EvaluateAt(root, new DiskItemFilter { Extensions = [".pdf"] });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsVisible(root), Is.True);
            Assert.That(result.IsVisible(documents), Is.True);
            Assert.That(result.IsVisible(photos), Is.False);
        });
    }

    [Test]
    public void ADirectoryWholeMatchesAreZeroBytesIsStillVisible()
    {
        var root = new DiskItem("root", "/root", isDirectory: true);
        var folder = new DiskItem("folder", "/root/folder", isDirectory: true);
        folder.AddChild(new DiskItem("empty.mov", "/root/folder/empty.mov", isDirectory: false)
        {
            SizeBytes = 0
        });
        root.AddChild(folder);

        var result = EvaluateAt(root, new DiskItemFilter { Extensions = [".mov"] });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsVisible(folder), Is.True);
            Assert.That(result.MatchedBytesFor(folder), Is.Zero);
            Assert.That(result.MatchCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void EvaluationObservesCancellation()
    {
        var root = CreateTree();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(
            () => _evaluator.Evaluate(root, DiskItemFilter.Empty, Reference, cancellation.Token));
    }

    [Test]
    public void EvaluationDoesNotModifyTheScanResult()
    {
        var root = CreateTree();
        var documents = root.Children.Single(child => child.Name == "Documents");
        var rootSize = root.SizeBytes;
        var documentsChildCount = documents.Children.Count;

        EvaluateAt(root, new DiskItemFilter { MinimumSizeBytes = 2048 });

        Assert.Multiple(() =>
        {
            Assert.That(root.SizeBytes, Is.EqualTo(rootSize));
            Assert.That(root.Children, Has.Count.EqualTo(2));
            Assert.That(documents.Children, Has.Count.EqualTo(documentsChildCount));
        });
    }

    private static DiskItem CreateTree()
    {
        var root = new DiskItem("root", "/Users/test", isDirectory: true);

        var documents = new DiskItem("Documents", "/Users/test/Documents", isDirectory: true);
        documents.AddChild(WithModified(
            new DiskItem("report.pdf", "/Users/test/Documents/report.pdf", isDirectory: false)
            {
                SizeBytes = 2048
            },
            Reference.AddDays(-30)));
        documents.AddChild(WithModified(
            new DiskItem("notes.txt", "/Users/test/Documents/notes.txt", isDirectory: false)
            {
                SizeBytes = 512
            },
            Reference.AddDays(-400)));
        documents.SizeBytes = 2560;

        var photos = new DiskItem("Photos", "/Users/test/Photos", isDirectory: true);
        photos.AddChild(WithModified(
            new DiskItem("holiday.jpg", "/Users/test/Photos/holiday.jpg", isDirectory: false)
            {
                SizeBytes = 4096
            },
            Reference.AddDays(-10)));
        photos.AddChild(WithModified(
            new DiskItem("clip.mov", "/Users/test/Photos/clip.mov", isDirectory: false)
            {
                SizeBytes = 8192
            },
            Reference.AddDays(-5)));
        photos.SizeBytes = 12288;

        root.AddChild(documents);
        root.AddChild(photos);
        root.SizeBytes = 14848;
        return root;
    }

    private static DiskItem WithModified(DiskItem item, DateTimeOffset modified)
    {
        item.Metadata = item.Metadata with { ModifiedTimeUtc = modified };
        return item;
    }
}
