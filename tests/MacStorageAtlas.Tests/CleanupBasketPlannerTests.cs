using MacStorageAtlas.Core;

namespace MacStorageAtlas.Tests;

public class CleanupBasketPlannerTests
{
    [Test]
    public void AddStoresScannedItemSnapshot()
    {
        var root = Directory("root", "/scan", 100, 120);
        var file = File("file.bin", "/scan/file.bin", 100, 120);
        root.AddChild(file);
        var planner = new CleanupBasketPlanner(root, StorageMeasurementMode.Logical);

        var result = planner.Add(file);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CleanupBasketAddStatus.Added));
            Assert.That(result.Changed, Is.True);
            Assert.That(planner.Items, Has.Count.EqualTo(1));
            Assert.That(planner.Items[0].Item, Is.SameAs(file));
            Assert.That(planner.Items[0].Snapshot.Path, Is.EqualTo(file.Path));
            Assert.That(planner.Items[0].Snapshot.SizeBytes, Is.EqualTo(100));
            Assert.That(planner.Items[0].Snapshot.MeasuredSizeBytes, Is.EqualTo(120));
        });
    }

    [Test]
    public void RemoveDeletesBasketItemWithoutChangingFilesystemItem()
    {
        var root = Directory("root", "/scan", 100, 120);
        var file = File("file.bin", "/scan/file.bin", 100, 120);
        root.AddChild(file);
        var planner = new CleanupBasketPlanner(root, StorageMeasurementMode.Logical);
        planner.Add(file);

        var removed = planner.Remove(file);

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.True);
            Assert.That(planner.Items, Is.Empty);
            Assert.That(root.Children, Is.EqualTo(new[] { file }));
        });
    }

    [Test]
    public void AddingSamePathTwiceLeavesBasketUnchanged()
    {
        var root = Directory("root", "/scan", 100, 120);
        var file = File("file.bin", "/scan/file.bin", 100, 120);
        root.AddChild(file);
        var planner = new CleanupBasketPlanner(root, StorageMeasurementMode.Logical);
        planner.Add(file);

        var result = planner.Add(file);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CleanupBasketAddStatus.AlreadySelected));
            Assert.That(result.Changed, Is.False);
            Assert.That(planner.Items, Has.Count.EqualTo(1));
            Assert.That(planner.Items[0].Item, Is.SameAs(file));
        });
    }

    [Test]
    public void AddingDescendantOfSelectedDirectoryIsRejected()
    {
        var root = Directory("root", "/scan", 300, 360);
        var folder = Directory("folder", "/scan/folder", 300, 360);
        var file = File("file.bin", "/scan/folder/file.bin", 100, 120);
        root.AddChild(folder);
        folder.AddChild(file);
        var planner = new CleanupBasketPlanner(root, StorageMeasurementMode.Logical);
        planner.Add(folder);

        var result = planner.Add(file);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CleanupBasketAddStatus.CoveredByAncestor));
            Assert.That(result.Changed, Is.False);
            Assert.That(planner.Items, Has.Count.EqualTo(1));
            Assert.That(planner.Items[0].Item, Is.SameAs(folder));
            Assert.That(planner.Summary.ItemCount, Is.EqualTo(1));
            Assert.That(planner.Summary.TotalLogicalSizeBytes, Is.EqualTo(300));
        });
    }

    [Test]
    public void AddingAncestorReplacesCoveredDescendants()
    {
        var root = Directory("root", "/scan", 300, 360);
        var folder = Directory("folder", "/scan/folder", 300, 360);
        var file = File("file.bin", "/scan/folder/file.bin", 100, 120);
        root.AddChild(folder);
        folder.AddChild(file);
        var planner = new CleanupBasketPlanner(root, StorageMeasurementMode.Logical);
        planner.Add(file);

        var result = planner.Add(folder);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CleanupBasketAddStatus.ReplacedDescendants));
            Assert.That(result.Changed, Is.True);
            Assert.That(result.RemovedItems, Has.Count.EqualTo(1));
            Assert.That(result.RemovedItems[0].Item, Is.SameAs(file));
            Assert.That(planner.Items, Has.Count.EqualTo(1));
            Assert.That(planner.Items[0].Item, Is.SameAs(folder));
            Assert.That(planner.Summary.ItemCount, Is.EqualTo(1));
            Assert.That(planner.Summary.TotalLogicalSizeBytes, Is.EqualTo(300));
        });
    }

    [Test]
    public void LogicalSummaryUsesLogicalBytes()
    {
        var root = Directory("root", "/scan", 300, 800);
        var first = File("first.bin", "/scan/first.bin", 100, 400);
        var second = File("second.bin", "/scan/second.bin", 200, 400);
        root.AddChild(first);
        root.AddChild(second);
        var planner = new CleanupBasketPlanner(root, StorageMeasurementMode.Logical);

        planner.Add(first);
        planner.Add(second);

        Assert.Multiple(() =>
        {
            Assert.That(planner.Summary.ItemCount, Is.EqualTo(2));
            Assert.That(planner.Summary.TotalLogicalSizeBytes, Is.EqualTo(300));
            Assert.That(planner.Summary.ExpectedReclaimableSizeBytes, Is.EqualTo(300));
        });
    }

    [Test]
    public void AllocatedSummaryUsesMeasuredBytes()
    {
        var root = Directory("root", "/scan", 300, 800);
        var first = File("first.bin", "/scan/first.bin", 100, 400);
        var second = File("second.bin", "/scan/second.bin", 200, 400);
        root.AddChild(first);
        root.AddChild(second);
        var planner = new CleanupBasketPlanner(root, StorageMeasurementMode.Allocated);

        planner.Add(first);
        planner.Add(second);

        Assert.Multiple(() =>
        {
            Assert.That(planner.Summary.ItemCount, Is.EqualTo(2));
            Assert.That(planner.Summary.TotalLogicalSizeBytes, Is.EqualTo(300));
            Assert.That(planner.Summary.ExpectedReclaimableSizeBytes, Is.EqualTo(800));
        });
    }

    [Test]
    public void SharedAwareSummaryUsesMeasuredBytesForZeroByteSharedEntry()
    {
        var root = Directory("root", "/scan", 100, 500);
        var shared = File("shared.bin", "/scan/shared.bin", 0, 400);
        shared.SharedSizeBytes = 400;
        var ordinary = File("ordinary.bin", "/scan/ordinary.bin", 100, 100);
        root.AddChild(shared);
        root.AddChild(ordinary);
        var planner = new CleanupBasketPlanner(
            root,
            StorageMeasurementMode.SharedAwareAllocated);

        planner.Add(shared);
        planner.Add(ordinary);

        Assert.Multiple(() =>
        {
            Assert.That(planner.Summary.ItemCount, Is.EqualTo(2));
            Assert.That(planner.Summary.TotalLogicalSizeBytes, Is.EqualTo(100));
            Assert.That(planner.Summary.ExpectedReclaimableSizeBytes, Is.EqualTo(500));
        });
    }

    [Test]
    public void MixedFileAndDirectorySummaryCountsEachActiveEntryOnce()
    {
        var root = Directory("root", "/scan", 700, 900);
        var folder = Directory("folder", "/scan/folder", 300, 400);
        var nested = File("nested.bin", "/scan/folder/nested.bin", 100, 200);
        var file = File("file.bin", "/scan/file.bin", 400, 500);
        root.AddChild(folder);
        folder.AddChild(nested);
        root.AddChild(file);
        var planner = new CleanupBasketPlanner(root, StorageMeasurementMode.Allocated);

        planner.Add(nested);
        planner.Add(folder);
        planner.Add(file);

        Assert.Multiple(() =>
        {
            Assert.That(planner.Items.Select(item => item.Item), Is.EqualTo(new[] { folder, file }));
            Assert.That(planner.Summary.ItemCount, Is.EqualTo(2));
            Assert.That(planner.Summary.TotalLogicalSizeBytes, Is.EqualTo(700));
            Assert.That(planner.Summary.ExpectedReclaimableSizeBytes, Is.EqualTo(900));
        });
    }

    private static DiskItem Directory(
        string name,
        string path,
        long logicalSize,
        long measuredSize) =>
        new(name, path, isDirectory: true)
        {
            SizeBytes = logicalSize,
            MeasuredSizeBytes = measuredSize
        };

    private static DiskItem File(
        string name,
        string path,
        long logicalSize,
        long measuredSize) =>
        new(name, path, isDirectory: false)
        {
            SizeBytes = logicalSize,
            MeasuredSizeBytes = measuredSize
        };
}
