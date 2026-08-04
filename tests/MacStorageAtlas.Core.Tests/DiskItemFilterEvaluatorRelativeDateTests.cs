using MacStorageAtlas.Core;

namespace MacStorageAtlas.Core.Tests;

public class DiskItemFilterEvaluatorRelativeDateTests
{
    private static readonly DateTimeOffset Reference =
        new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    private readonly DiskItemFilterEvaluator _evaluator = new();

    [Test]
    public void ARelativeBoundMatchesTheSameFilesAsTheInstantItResolvesTo()
    {
        var root = CreateTree();
        var relative = new RelativeDateCriterion(1, RelativeDateUnit.Years);
        var absolute = new AbsoluteDateCriterion(relative.Resolve(Reference));

        var byRelative = _evaluator.Evaluate(
            root,
            new DiskItemFilter { ModifiedBefore = relative },
            Reference);
        var byAbsolute = _evaluator.Evaluate(
            root,
            new DiskItemFilter { ModifiedBefore = absolute },
            Reference);

        Assert.Multiple(() =>
        {
            Assert.That(
                byRelative.MatchedFiles.Select(file => file.Name),
                Is.EqualTo(byAbsolute.MatchedFiles.Select(file => file.Name)));
            Assert.That(
                byRelative.UnknownDateExclusionCount,
                Is.EqualTo(byAbsolute.UnknownDateExclusionCount));
        });
    }

    [Test]
    public void ARelativeBoundReportsUnknownDateExclusionsLikeAnAbsoluteBound()
    {
        var root = CreateTree();

        var result = _evaluator.Evaluate(
            root,
            new DiskItemFilter
            {
                ModifiedBefore = new RelativeDateCriterion(1, RelativeDateUnit.Years)
            },
            Reference);

        Assert.That(result.UnknownDateExclusionCount, Is.EqualTo(1));
    }

    [Test]
    public void TheSameFilterResolvesRelativeToEachReferenceTime()
    {
        var root = CreateTree();
        var filter = new DiskItemFilter
        {
            ModifiedBefore = new RelativeDateCriterion(1, RelativeDateUnit.Years)
        };

        var atReference = _evaluator.Evaluate(root, filter, Reference);
        var twoYearsLater = _evaluator.Evaluate(root, filter, Reference.AddYears(2));

        Assert.Multiple(() =>
        {
            Assert.That(
                atReference.MatchedFiles.Select(file => file.Name),
                Is.EqualTo(new[] { "ancient.txt" }));
            Assert.That(
                twoYearsLater.MatchedFiles.Select(file => file.Name),
                Is.EqualTo(new[] { "recent.txt", "ancient.txt" }));
        });
    }

    [Test]
    public void TheEvaluatedReferenceTimeIsReported()
    {
        var result = _evaluator.Evaluate(
            CreateTree(),
            new DiskItemFilter { MinimumSizeBytes = 1 },
            Reference);

        Assert.That(result.ReferenceTime, Is.EqualTo(Reference));
    }

    [Test]
    public void EveryBoundResolvesAgainstTheSameReferenceTime()
    {
        var root = new DiskItem("root", "/root", isDirectory: true);
        root.AddChild(WithModified(
            new DiskItem("edge.txt", "/root/edge.txt", isDirectory: false) { SizeBytes = 10 },
            Reference.AddYears(-1)));

        var result = _evaluator.Evaluate(
            root,
            new DiskItemFilter
            {
                ModifiedAfter = new RelativeDateCriterion(1, RelativeDateUnit.Years),
                ModifiedBefore = new RelativeDateCriterion(1, RelativeDateUnit.Years)
            },
            Reference);

        Assert.That(result.MatchCount, Is.EqualTo(1));
    }

    private static DiskItem CreateTree()
    {
        var root = new DiskItem("root", "/Users/test", isDirectory: true);

        root.AddChild(WithModified(
            new DiskItem("recent.txt", "/Users/test/recent.txt", isDirectory: false)
            {
                SizeBytes = 100
            },
            Reference.AddDays(-30)));

        root.AddChild(WithModified(
            new DiskItem("ancient.txt", "/Users/test/ancient.txt", isDirectory: false)
            {
                SizeBytes = 200
            },
            Reference.AddDays(-400)));

        root.AddChild(new DiskItem("undated.txt", "/Users/test/undated.txt", isDirectory: false)
        {
            SizeBytes = 300
        });

        root.SizeBytes = 600;
        return root;
    }

    private static DiskItem WithModified(DiskItem item, DateTimeOffset modified)
    {
        item.Metadata = item.Metadata with { ModifiedTimeUtc = modified };
        return item;
    }
}
