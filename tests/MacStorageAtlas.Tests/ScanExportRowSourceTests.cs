using MacStorageAtlas.Core;

namespace MacStorageAtlas.Tests;

public class ScanExportRowSourceTests
{
    [Test]
    public void AFullExportEmitsEachDirectoryImmediatelyBeforeItsDescendants()
    {
        var root = CreateTree();

        var paths = ScanExportRowSource
            .EnumerateFull(root, StorageMeasurementMode.Logical)
            .Select(row => row.Path)
            .ToArray();

        Assert.That(paths, Is.EqualTo(new[]
        {
            "/scan",
            "/scan/videos",
            "/scan/videos/large.mov",
            "/scan/videos/small.mov",
            "/scan/docs",
            "/scan/docs/notes.txt",
            "/scan/readme.md"
        }));
    }

    [Test]
    public void AFullExportReportsDepthRelativeToTheRoot()
    {
        var root = CreateTree();

        var depths = ScanExportRowSource
            .EnumerateFull(root, StorageMeasurementMode.Logical)
            .ToDictionary(row => row.Path, row => row.Depth);

        Assert.Multiple(() =>
        {
            Assert.That(depths["/scan"], Is.EqualTo(0));
            Assert.That(depths["/scan/videos"], Is.EqualTo(1));
            Assert.That(depths["/scan/videos/large.mov"], Is.EqualTo(2));
            Assert.That(depths["/scan/readme.md"], Is.EqualTo(1));
        });
    }

    [Test]
    public void SiblingsOfEqualSizeAreOrderedByOrdinalPath()
    {
        var root = new DiskItem("root", "/scan", isDirectory: true) { SizeBytes = 300 };
        root.AddChild(new DiskItem("b.txt", "/scan/b.txt", isDirectory: false) { SizeBytes = 100 });
        root.AddChild(new DiskItem("a.txt", "/scan/a.txt", isDirectory: false) { SizeBytes = 100 });
        root.AddChild(new DiskItem("C.txt", "/scan/C.txt", isDirectory: false) { SizeBytes = 100 });

        var paths = ScanExportRowSource
            .EnumerateFull(root, StorageMeasurementMode.Logical)
            .Skip(1)
            .Select(row => row.Path)
            .ToArray();

        Assert.That(paths, Is.EqualTo(new[] { "/scan/C.txt", "/scan/a.txt", "/scan/b.txt" }));
    }

    [Test]
    public void EnumeratingTheSameTreeTwiceYieldsTheSameSequence()
    {
        var root = CreateTree();

        var first = ScanExportRowSource
            .EnumerateFull(root, StorageMeasurementMode.Logical)
            .ToArray();
        var second = ScanExportRowSource
            .EnumerateFull(root, StorageMeasurementMode.Logical)
            .ToArray();

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void EnumeratingDoesNotReorderTheScanTree()
    {
        var root = new DiskItem("root", "/scan", isDirectory: true) { SizeBytes = 300 };
        root.AddChild(new DiskItem("small.txt", "/scan/small.txt", isDirectory: false)
        {
            SizeBytes = 100
        });
        root.AddChild(new DiskItem("large.txt", "/scan/large.txt", isDirectory: false)
        {
            SizeBytes = 200
        });
        var childOrderBefore = root.Children.Select(child => child.Path).ToArray();

        _ = ScanExportRowSource
            .EnumerateFull(root, StorageMeasurementMode.Logical)
            .ToArray();

        Assert.That(
            root.Children.Select(child => child.Path).ToArray(),
            Is.EqualTo(childOrderBefore));
    }

    [Test]
    public void AFilteredExportEmitsOnlyFilesOrderedBySizeDescending()
    {
        var root = CreateTree();
        var matched = Descendants(root)
            .Where(item => !item.IsDirectory)
            .ToArray();

        var rows = ScanExportRowSource
            .EnumerateFiltered(matched, "/scan", StorageMeasurementMode.Logical)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(rows.Any(row => row.Kind == DiskItemKind.Directory), Is.False);
            Assert.That(
                rows.Select(row => row.Path).ToArray(),
                Is.EqualTo(new[]
                {
                    "/scan/videos/large.mov",
                    "/scan/videos/small.mov",
                    "/scan/docs/notes.txt",
                    "/scan/readme.md"
                }));
        });
    }

    [Test]
    public void AFilteredExportReportsDepthRelativeToTheScanRoot()
    {
        var root = CreateTree();
        var matched = Descendants(root).Where(item => !item.IsDirectory).ToArray();

        var depths = ScanExportRowSource
            .EnumerateFiltered(matched, "/scan", StorageMeasurementMode.Logical)
            .ToDictionary(row => row.Path, row => row.Depth);

        Assert.Multiple(() =>
        {
            Assert.That(depths["/scan/videos/large.mov"], Is.EqualTo(2));
            Assert.That(depths["/scan/readme.md"], Is.EqualTo(1));
        });
    }

    [TestCase("/scan", "/scan", 0)]
    [TestCase("/scan", "/scan/a.txt", 1)]
    [TestCase("/scan", "/scan/a/b/c.txt", 3)]
    [TestCase("/scan/", "/scan/a/b.txt", 2)]
    [TestCase("/", "/a.txt", 1)]
    [TestCase("/scan", "/elsewhere/a.txt", 0)]
    [TestCase("/scan", "/scanner/a.txt", 0)]
    public void DepthCountsSegmentsBelowTheRoot(string rootPath, string path, int expected)
    {
        Assert.That(ScanExportRowSource.DepthBelow(rootPath, path), Is.EqualTo(expected));
    }

    [Test]
    public void CancellationStopsAFullEnumeration()
    {
        var root = CreateTree();
        using var cancellation = new CancellationTokenSource();
        var emitted = 0;

        Assert.Throws<OperationCanceledException>(() =>
        {
            foreach (var _ in ScanExportRowSource.EnumerateFull(
                         root,
                         StorageMeasurementMode.Logical,
                         cancellation.Token))
            {
                emitted++;
                if (emitted == 2)
                {
                    cancellation.Cancel();
                }
            }
        });

        Assert.That(emitted, Is.EqualTo(2));
    }

    [Test]
    public void CancellationStopsAFilteredEnumeration()
    {
        var root = CreateTree();
        var matched = Descendants(root).Where(item => !item.IsDirectory).ToArray();
        using var cancellation = new CancellationTokenSource();
        var emitted = 0;

        Assert.Throws<OperationCanceledException>(() =>
        {
            foreach (var _ in ScanExportRowSource.EnumerateFiltered(
                         matched,
                         "/scan",
                         StorageMeasurementMode.Logical,
                         cancellation.Token))
            {
                emitted++;
                cancellation.Cancel();
            }
        });

        Assert.That(emitted, Is.EqualTo(1));
    }

    [Test]
    public void AnAlreadyCancelledTokenEmitsNothing()
    {
        var root = CreateTree();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            ScanExportRowSource
                .EnumerateFull(root, StorageMeasurementMode.Logical, cancellation.Token)
                .ToArray());
    }

    [Test]
    public void ANullRootIsRejectedBeforeEnumerationStarts()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ScanExportRowSource.EnumerateFull(null!, StorageMeasurementMode.Logical));
    }

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

    private static IEnumerable<DiskItem> Descendants(DiskItem item)
    {
        foreach (var child in item.Children)
        {
            yield return child;

            foreach (var descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
