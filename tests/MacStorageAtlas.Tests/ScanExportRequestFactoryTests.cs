using MacStorageAtlas.Core;

namespace MacStorageAtlas.Tests;

public class ScanExportRequestFactoryTests
{
    private static readonly DateTimeOffset Completed =
        new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void AFullExportCountsEveryRowAndSumsTheFileRows()
    {
        var root = CreateTree();

        var request = Create(root);
        var rows = request.Rows.ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(request.Metadata.Scope, Is.EqualTo(ScanExportScope.Full));
            Assert.That(request.Metadata.ItemCount, Is.EqualTo(rows.Length));
            Assert.That(
                request.Metadata.TotalCountedSizeBytes,
                Is.EqualTo(rows.Where(row => row.Kind != DiskItemKind.Directory)
                    .Sum(row => row.CountedSizeBytes)));
        });
    }

    [Test]
    public void AFullExportTotalEqualsTheScanRootCountedSize()
    {
        var root = CreateTree();

        var request = Create(root);

        Assert.That(request.Metadata.TotalCountedSizeBytes, Is.EqualTo(root.SizeBytes));
    }

    [Test]
    public void AFullExportDoesNotAddDirectoryRollupsToTheirDescendants()
    {
        var root = CreateTree();

        var request = Create(root);
        var everyRowSum = request.Rows.Sum(row => row.CountedSizeBytes);

        Assert.Multiple(() =>
        {
            Assert.That(request.Metadata.TotalCountedSizeBytes, Is.EqualTo(3400));
            Assert.That(everyRowSum, Is.EqualTo(10100));
            Assert.That(request.Metadata.TotalCountedSizeBytes, Is.Not.EqualTo(everyRowSum));
        });
    }

    [Test]
    public void AFilteredExportCountsAndSumsItsMatchedFiles()
    {
        var root = CreateTree();
        var filterResult = new DiskItemFilterEvaluator().Evaluate(
            root,
            new DiskItemFilter { MinimumSizeBytes = 300 },
            Completed);

        var request = Create(root, filterResult);
        var rows = request.Rows.ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(request.Metadata.Scope, Is.EqualTo(ScanExportScope.Filtered));
            Assert.That(request.Metadata.ItemCount, Is.EqualTo(rows.Length));
            Assert.That(
                request.Metadata.TotalCountedSizeBytes,
                Is.EqualTo(rows.Sum(row => row.CountedSizeBytes)));
            Assert.That(rows.Any(row => row.Kind == DiskItemKind.Directory), Is.False);
        });
    }

    [Test]
    public void AFilteredExportRecordsTheFilterThatProducedIt()
    {
        var root = CreateTree();
        var filter = new DiskItemFilter { MinimumSizeBytes = 300 };
        var filterResult = new DiskItemFilterEvaluator().Evaluate(root, filter, Completed);

        var request = Create(root, filterResult);

        Assert.That(request.Metadata.Filter, Is.EqualTo(filter));
    }

    [Test]
    public void AnInactiveFilterProducesAFullExportWithoutAFilter()
    {
        var root = CreateTree();
        var filterResult = new DiskItemFilterEvaluator().Evaluate(
            root,
            DiskItemFilter.Empty,
            Completed);

        var request = Create(root, filterResult);

        Assert.Multiple(() =>
        {
            Assert.That(request.Metadata.Scope, Is.EqualTo(ScanExportScope.Full));
            Assert.That(request.Metadata.Filter, Is.Null);
            Assert.That(request.Metadata.ItemCount, Is.EqualTo(7));
        });
    }

    [Test]
    public void AFilteredExportThatMatchesNothingReportsZeroTotals()
    {
        var root = CreateTree();
        var filterResult = new DiskItemFilterEvaluator().Evaluate(
            root,
            new DiskItemFilter { MinimumSizeBytes = 1_000_000_000 },
            Completed);

        var request = Create(root, filterResult);

        Assert.Multiple(() =>
        {
            Assert.That(request.Metadata.ItemCount, Is.Zero);
            Assert.That(request.Metadata.TotalCountedSizeBytes, Is.Zero);
            Assert.That(request.Rows, Is.Empty);
        });
    }

    [Test]
    public void TheMetadataCarriesTheScanContext()
    {
        var root = CreateTree();
        var options = ScanOptions.Default with
        {
            IncludeHiddenFiles = true,
            MeasurementMode = StorageMeasurementMode.Allocated
        };

        var request = ScanExportRequestFactory.Create(
            root,
            options,
            StorageMeasurementMode.Allocated,
            CloneAccountingCoverage.Partial,
            Completed,
            errors: [new ScanError("/scan/x", "denied", "UnauthorizedAccessException")]);

        Assert.Multiple(() =>
        {
            Assert.That(request.Metadata.RootPath, Is.EqualTo("/scan"));
            Assert.That(request.Metadata.ScanCompletedAt, Is.EqualTo(Completed));
            Assert.That(request.Metadata.Options, Is.EqualTo(options));
            Assert.That(
                request.Metadata.MeasurementMode,
                Is.EqualTo(StorageMeasurementMode.Allocated));
            Assert.That(
                request.Metadata.CloneAccountingCoverage,
                Is.EqualTo(CloneAccountingCoverage.Partial));
            Assert.That(request.Errors, Has.Count.EqualTo(1));
            Assert.That(
                request.Rows.Select(row => row.MeasurementMode).Distinct().Single(),
                Is.EqualTo(StorageMeasurementMode.Allocated));
        });
    }

    private static ScanExportRequest Create(DiskItem root, FilterResult? filterResult = null) =>
        ScanExportRequestFactory.Create(
            root,
            ScanOptions.Default,
            StorageMeasurementMode.Logical,
            CloneAccountingCoverage.Unavailable,
            Completed,
            filterResult);

    private static DiskItem CreateTree()
    {
        var root = new DiskItem("scan", "/scan", isDirectory: true) { SizeBytes = 3400 };

        var videos = new DiskItem("videos", "/scan/videos", isDirectory: true)
        {
            SizeBytes = 3000
        };
        videos.AddChild(new DiskItem("large.mov", "/scan/videos/large.mov", isDirectory: false)
        {
            SizeBytes = 2000
        });
        videos.AddChild(new DiskItem("small.mov", "/scan/videos/small.mov", isDirectory: false)
        {
            SizeBytes = 1000
        });

        var docs = new DiskItem("docs", "/scan/docs", isDirectory: true) { SizeBytes = 300 };
        docs.AddChild(new DiskItem("notes.txt", "/scan/docs/notes.txt", isDirectory: false)
        {
            SizeBytes = 300
        });

        root.AddChild(docs);
        root.AddChild(videos);
        root.AddChild(new DiskItem("readme.md", "/scan/readme.md", isDirectory: false)
        {
            SizeBytes = 100
        });

        return root;
    }
}
