using MacStorageAtlas.Core;

namespace MacStorageAtlas.Core.Tests;

public class CleanupProtectedPathPolicyTests
{
    [Test]
    public void ClassifyBlocksCurrentScanRoot()
    {
        var root = Directory("scan", "/Users/test/scan");
        var policy = new CleanupProtectedPathPolicy(root);

        var status = policy.Classify(root);

        Assert.Multiple(() =>
        {
            Assert.That(status.IsProtected, Is.True);
            Assert.That(status.Reason, Is.EqualTo(CleanupProtectionReason.ScanRoot));
            Assert.That(status.Message, Does.Contain("scan root"));
        });
    }

    [Test]
    public void ClassifyBlocksSystemPathWhenItIsInScanResult()
    {
        var root = Directory("root", "/");
        var system = Directory("System", "/System");
        root.AddChild(system);
        var policy = new CleanupProtectedPathPolicy(root);

        var status = policy.Classify(system);

        Assert.Multiple(() =>
        {
            Assert.That(status.IsProtected, Is.True);
            Assert.That(status.Reason, Is.EqualTo(CleanupProtectionReason.SystemPath));
            Assert.That(status.Message, Does.Contain("system"));
        });
    }

    [Test]
    public void ClassifyBlocksTrashPathWhenItIsInScanResult()
    {
        var root = Directory("home", "/Users/test");
        var trash = Directory(".Trash", "/Users/test/.Trash");
        root.AddChild(trash);
        var policy = new CleanupProtectedPathPolicy(root);

        var status = policy.Classify(trash);

        Assert.Multiple(() =>
        {
            Assert.That(status.IsProtected, Is.True);
            Assert.That(status.Reason, Is.EqualTo(CleanupProtectionReason.TrashLocation));
            Assert.That(status.Message, Does.Contain("Trash"));
        });
    }

    [Test]
    public void ClassifyBlocksPathOutsideScanResult()
    {
        var root = Directory("scan", "/Users/test/scan");
        var policy = new CleanupProtectedPathPolicy(root);

        var status = policy.Classify("/Users/test/other/file.bin");

        Assert.Multiple(() =>
        {
            Assert.That(status.IsProtected, Is.True);
            Assert.That(status.Reason, Is.EqualTo(CleanupProtectionReason.OutsideScanResult));
            Assert.That(status.Message, Does.Contain("outside"));
        });
    }

    [Test]
    public void ClassifyAllowsOrdinaryScannedUserPath()
    {
        var root = Directory("scan", "/Users/test/scan");
        var file = File("file.bin", "/Users/test/scan/file.bin", 10, 20);
        root.AddChild(file);
        var policy = new CleanupProtectedPathPolicy(root);

        var status = policy.Classify(file);

        Assert.Multiple(() =>
        {
            Assert.That(status.IsProtected, Is.False);
            Assert.That(status.Reason, Is.EqualTo(CleanupProtectionReason.None));
        });
    }

    [Test]
    public void PlannerRejectsProtectedItem()
    {
        var root = Directory("scan", "/Users/test/scan");
        var policy = new CleanupProtectedPathPolicy(root);
        var planner = new CleanupBasketPlanner(
            root,
            StorageMeasurementMode.Logical,
            policy);

        var result = planner.Add(root);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(CleanupBasketAddStatus.Protected));
            Assert.That(result.Changed, Is.False);
            Assert.That(planner.Items, Is.Empty);
        });
    }

    private static DiskItem Directory(string name, string path) =>
        new(name, path, isDirectory: true);

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
