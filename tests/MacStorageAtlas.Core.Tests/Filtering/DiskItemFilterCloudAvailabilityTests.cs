using MacStorageAtlas.Core.Filtering;
using MacStorageAtlas.Core.Insights;
using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.Core.Tests.Filtering;

public class DiskItemFilterCloudAvailabilityTests
{
    private static readonly DateTimeOffset Reference =
        new(2026, 11, 22, 12, 0, 0, TimeSpan.Zero);

    private readonly DiskItemFilterEvaluator _evaluator = new();

    [Test]
    public void AnyIsTheDefaultAndKeepsTheFilterInactive()
    {
        var filter = new DiskItemFilter();

        Assert.Multiple(() =>
        {
            Assert.That(filter.CloudAvailability, Is.EqualTo(CloudAvailabilityCriterion.Any));
            Assert.That(filter.IsActive, Is.False);
            Assert.That(
                new DiskItemFilter { CloudAvailability = CloudAvailabilityCriterion.LocalOnly }.IsActive,
                Is.True);
        });
    }

    [Test]
    public void LocalOnlyMatchesOnlyLocalFiles()
    {
        var result = Evaluate(new DiskItemFilter { CloudAvailability = CloudAvailabilityCriterion.LocalOnly });

        Assert.That(
            result.MatchedFiles.Select(file => file.Name),
            Is.EquivalentTo(new[] { "local-small.txt", "local-large.mov" }));
    }

    [Test]
    public void CloudOnlyMatchesOnlyCloudOnlyFiles()
    {
        var result = Evaluate(new DiskItemFilter { CloudAvailability = CloudAvailabilityCriterion.CloudOnly });

        Assert.That(
            result.MatchedFiles.Select(file => file.Name),
            Is.EquivalentTo(new[] { "cloud-small.txt", "cloud-large.mov" }));
    }

    [Test]
    public void AnyMatchesFilesOfEveryAvailabilityIncludingUnknown()
    {
        var result = Evaluate(new DiskItemFilter { MinimumSizeBytes = 0 });

        Assert.That(result.MatchCount, Is.EqualTo(5));
    }

    [TestCase(CloudAvailabilityCriterion.LocalOnly)]
    [TestCase(CloudAvailabilityCriterion.CloudOnly)]
    public void NeitherRestrictionMatchesAFileWithUnknownAvailability(
        CloudAvailabilityCriterion criterion)
    {
        var result = Evaluate(new DiskItemFilter { CloudAvailability = criterion });

        Assert.That(result.MatchedFiles.Select(file => file.Name), Does.Not.Contain("unknown.txt"));
    }

    [Test]
    public void CloudOnlyCombinesWithASizeCriterion()
    {
        var result = Evaluate(new DiskItemFilter
        {
            CloudAvailability = CloudAvailabilityCriterion.CloudOnly,
            MinimumSizeBytes = 100
        });

        Assert.That(result.MatchedFiles.Select(file => file.Name), Is.EqualTo(new[] { "cloud-large.mov" }));
    }

    [Test]
    public void LocalOnlyCombinesWithACategoryCriterion()
    {
        var result = Evaluate(new DiskItemFilter
        {
            CloudAvailability = CloudAvailabilityCriterion.LocalOnly,
            Categories = [FileCategory.Video]
        });

        Assert.That(result.MatchedFiles.Select(file => file.Name), Is.EqualTo(new[] { "local-large.mov" }));
    }

    [Test]
    public void TheCriterionMatchesFilesOnlyAndKeepsTheirFoldersVisible()
    {
        var root = CreateTree();

        var result = _evaluator.Evaluate(
            root,
            new DiskItemFilter { CloudAvailability = CloudAvailabilityCriterion.CloudOnly },
            Reference);

        var cloud = root.Children.Single(child => child.Name == "Cloud");
        Assert.Multiple(() =>
        {
            Assert.That(result.Matches(cloud), Is.False);
            Assert.That(result.IsVisible(cloud), Is.True);
        });
    }

    [Test]
    public void LargestFilesHonorTheCriterion()
    {
        var root = CreateTree();
        var result = _evaluator.Evaluate(
            root,
            new DiskItemFilter { CloudAvailability = CloudAvailabilityCriterion.LocalOnly },
            Reference);

        var largest = new LargeFilesService().GetLargestFiles(
            root,
            item => item.IsDirectory ? result.IsVisible(item) : result.Matches(item),
            LargeFilesService.DefaultLimit,
            CancellationToken.None);

        Assert.That(
            largest.Select(file => file.Name),
            Is.EqualTo(new[] { "local-large.mov", "local-small.txt" }));
    }

    [Test]
    public void AnUndefinedCriterionIsInvalid()
    {
        var filter = new DiskItemFilter { CloudAvailability = (CloudAvailabilityCriterion)9 };

        Assert.That(filter.Validate().IsValid, Is.False);
    }

    [Test]
    public void FiltersDifferingOnlyInTheCriterionAreNotEqual()
    {
        var local = new DiskItemFilter { CloudAvailability = CloudAvailabilityCriterion.LocalOnly };
        var cloud = new DiskItemFilter { CloudAvailability = CloudAvailabilityCriterion.CloudOnly };

        Assert.Multiple(() =>
        {
            Assert.That(local, Is.Not.EqualTo(cloud));
            Assert.That(local, Is.EqualTo(local with { }));
            Assert.That(local.GetHashCode(), Is.EqualTo((local with { }).GetHashCode()));
        });
    }

    private FilterResult Evaluate(DiskItemFilter filter) =>
        _evaluator.Evaluate(CreateTree(), filter, Reference);

    private static DiskItem CreateTree()
    {
        var root = new DiskItem("root", "/Users/test", isDirectory: true);
        var local = new DiskItem("Local", "/Users/test/Local", isDirectory: true);
        local.AddChild(File("/Users/test/Local/local-small.txt", 10, CloudAvailability.Local));
        local.AddChild(File("/Users/test/Local/local-large.mov", 500, CloudAvailability.Local));
        var cloud = new DiskItem("Cloud", "/Users/test/Cloud", isDirectory: true);
        cloud.AddChild(File("/Users/test/Cloud/cloud-small.txt", 10, CloudAvailability.CloudOnly));
        cloud.AddChild(File("/Users/test/Cloud/cloud-large.mov", 500, CloudAvailability.CloudOnly));
        root.AddChild(local);
        root.AddChild(cloud);
        root.AddChild(File("/Users/test/unknown.txt", 10, CloudAvailability.Unknown));
        return root;
    }

    private static DiskItem File(string path, long size, CloudAvailability availability) =>
        new(Path.GetFileName(path), path, isDirectory: false)
        {
            SizeBytes = size,
            Metadata = DiskItemMetadata.Unknown(DiskItemKind.File) with
            {
                CloudAvailability = availability
            }
        };
}
