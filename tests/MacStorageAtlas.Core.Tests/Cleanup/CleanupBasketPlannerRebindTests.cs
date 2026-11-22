using MacStorageAtlas.Core.Cleanup;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Tests.Cleanup;

public class CleanupBasketPlannerRebindTests
{
    [Test]
    public void RebindTakesTheRefreshedSizeOfAnEntryInsideTheFolder()
    {
        var scan = Scan();
        var planner = new CleanupBasketPlanner(scan.Root, StorageMeasurementMode.Logical);
        planner.Add(scan.Grow);
        var refreshedGrow = FileItem("/scan/work/project/grow.bin", 50);

        ScanResultMerger.ReplaceSubtree(scan.Root, "/scan/work/project", Folder("/scan/work/project", refreshedGrow));
        var removed = planner.Rebind(scan.Root, "/scan/work/project");

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.Zero);
            Assert.That(planner.Items.Single().Item, Is.SameAs(refreshedGrow));
            Assert.That(planner.Items.Single().Snapshot.SizeBytes, Is.EqualTo(50));
            Assert.That(planner.Summary.TotalLogicalSizeBytes, Is.EqualTo(50));
        });
    }

    [Test]
    public void RebindKeepsAnUnchangedEntryInsideTheFolderBoundToTheRefreshedItem()
    {
        var scan = Scan();
        var planner = new CleanupBasketPlanner(scan.Root, StorageMeasurementMode.Logical);
        planner.Add(scan.Grow);
        var sameGrow = FileItem("/scan/work/project/grow.bin", 10);

        ScanResultMerger.ReplaceSubtree(scan.Root, "/scan/work/project", Folder("/scan/work/project", sameGrow));
        planner.Rebind(scan.Root, "/scan/work/project");

        Assert.Multiple(() =>
        {
            Assert.That(planner.Items.Single().Item, Is.SameAs(sameGrow));
            Assert.That(planner.Items.Single().Snapshot.SizeBytes, Is.EqualTo(10));
        });
    }

    [Test]
    public void RebindRemovesEntriesThatVanishedAndReportsHowMany()
    {
        var scan = Scan();
        var planner = new CleanupBasketPlanner(scan.Root, StorageMeasurementMode.Logical);
        planner.Add(scan.Grow);
        planner.Add(scan.Gone);

        ScanResultMerger.ReplaceSubtree(
            scan.Root,
            "/scan/work/project",
            Folder("/scan/work/project", FileItem("/scan/work/project/grow.bin", 10)));
        var removed = planner.Rebind(scan.Root, "/scan/work/project");

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.EqualTo(1));
            Assert.That(
                planner.Items.Select(item => item.Snapshot.Path),
                Is.EqualTo(new[] { "/scan/work/project/grow.bin" }));
        });
    }

    [Test]
    public void RebindRemovesEveryEntryOfAFolderThatNoLongerExists()
    {
        var scan = Scan();
        var planner = new CleanupBasketPlanner(scan.Root, StorageMeasurementMode.Logical);
        planner.Add(scan.Grow);
        planner.Add(scan.Gone);
        planner.Add(scan.Other);

        ScanResultMerger.ReplaceSubtree(scan.Root, "/scan/work/project", null);
        var removed = planner.Rebind(scan.Root, "/scan/work/project");

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.EqualTo(2));
            Assert.That(planner.Items.Single().Item, Is.SameAs(scan.Other));
        });
    }

    [Test]
    public void RebindLeavesEntriesOutsideTheFolderUnchanged()
    {
        var scan = Scan();
        var planner = new CleanupBasketPlanner(scan.Root, StorageMeasurementMode.Logical);
        planner.Add(scan.Other);
        var before = planner.Items.Single();

        ScanResultMerger.ReplaceSubtree(
            scan.Root,
            "/scan/work/project",
            Folder("/scan/work/project", FileItem("/scan/work/project/grow.bin", 99)));
        var removed = planner.Rebind(scan.Root, "/scan/work/project");

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.Zero);
            Assert.That(planner.Items.Single(), Is.SameAs(before));
            Assert.That(planner.Summary.TotalLogicalSizeBytes, Is.EqualTo(7));
        });
    }

    [Test]
    public void RebindRefreshesTheSizeOfAnEntryThatContainsTheFolder()
    {
        var scan = Scan();
        var planner = new CleanupBasketPlanner(scan.Root, StorageMeasurementMode.Logical);
        planner.Add(scan.Work);

        ScanResultMerger.ReplaceSubtree(
            scan.Root,
            "/scan/work/project",
            Folder("/scan/work/project", FileItem("/scan/work/project/grow.bin", 40)));
        planner.Rebind(scan.Root, "/scan/work/project");

        Assert.Multiple(() =>
        {
            Assert.That(planner.Items.Single().Item, Is.SameAs(scan.Work));
            Assert.That(planner.Items.Single().Snapshot.SizeBytes, Is.EqualTo(40));
        });
    }

    [Test]
    public void RebindAppliesTheRefreshedProtectedPathPolicy()
    {
        var scan = Scan();
        var planner = new CleanupBasketPlanner(
            scan.Root,
            StorageMeasurementMode.Logical,
            new CleanupProtectedPathPolicy(scan.Root));
        planner.Add(scan.Grow);
        var refreshedGrow = FileItem("/scan/work/project/grow.bin", 12);

        ScanResultMerger.ReplaceSubtree(scan.Root, "/scan/work/project", Folder("/scan/work/project", refreshedGrow));
        planner.Rebind(scan.Root, "/scan/work/project", new CleanupProtectedPathPolicy(scan.Root));

        Assert.That(planner.Items.Single().ProtectionStatus.IsProtected, Is.False);
    }

    private static ScanFixture Scan()
    {
        var root = new DiskItem("scan", "/scan", isDirectory: true);
        var work = new DiskItem("work", "/scan/work", isDirectory: true);
        var grow = FileItem("/scan/work/project/grow.bin", 10);
        var gone = FileItem("/scan/work/project/gone.bin", 5);
        var project = Folder("/scan/work/project", grow, gone);
        var other = FileItem("/scan/other.bin", 7);

        work.AddChild(project);
        work.SizeBytes = project.SizeBytes;
        root.AddChild(work);
        root.AddChild(other);
        root.SizeBytes = work.SizeBytes + other.SizeBytes;
        return new ScanFixture(root, work, grow, gone, other);
    }

    private static DiskItem Folder(string path, params DiskItem[] files)
    {
        var folder = new DiskItem(Path.GetFileName(path), path, isDirectory: true);
        foreach (var file in files)
        {
            folder.AddChild(file);
        }

        folder.SizeBytes = files.Sum(file => file.SizeBytes);
        return folder;
    }

    private static DiskItem FileItem(string path, long size) =>
        new(Path.GetFileName(path), path, isDirectory: false) { SizeBytes = size };

    private sealed record ScanFixture(
        DiskItem Root,
        DiskItem Work,
        DiskItem Grow,
        DiskItem Gone,
        DiskItem Other);
}
