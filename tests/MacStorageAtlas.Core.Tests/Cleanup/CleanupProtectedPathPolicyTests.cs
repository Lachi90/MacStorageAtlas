using MacStorageAtlas.Core.Cleanup;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Tests.Cleanup;

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
    public void ClassifyBlocksUserHomeWhenItIsInScanResult()
    {
        var root = Directory("root", "/");
        var home = Directory("test", "/Users/test");
        root.AddChild(home);
        var policy = new CleanupProtectedPathPolicy(root);

        var status = policy.Classify(home);

        Assert.Multiple(() =>
        {
            Assert.That(status.IsProtected, Is.True);
            Assert.That(status.Reason, Is.EqualTo(CleanupProtectionReason.SensitiveLocation));
            Assert.That(status.Message, Does.Contain("user data"));
        });
    }

    [TestCase("Desktop")]
    [TestCase("Documents")]
    [TestCase("Downloads")]
    [TestCase("Library")]
    [TestCase("Movies")]
    [TestCase("Music")]
    [TestCase("Pictures")]
    public void ClassifyBlocksStandardUserFolderContainer(string folderName)
    {
        var root = Directory("home", "/Users/test");
        var folder = Directory(folderName, $"/Users/test/{folderName}");
        root.AddChild(folder);
        var policy = new CleanupProtectedPathPolicy(root);

        var status = policy.Classify(folder);

        Assert.Multiple(() =>
        {
            Assert.That(status.IsProtected, Is.True);
            Assert.That(status.Reason, Is.EqualTo(CleanupProtectionReason.SensitiveLocation));
            Assert.That(status.Message, Does.Contain("user data"));
        });
    }

    [Test]
    public void ClassifyAllowsOrdinaryDescendantOfStandardUserFolder()
    {
        var root = Directory("home", "/Users/test");
        var documents = Directory("Documents", "/Users/test/Documents");
        var file = File("old.dmg", "/Users/test/Documents/old.dmg", 10, 20);
        documents.AddChild(file);
        root.AddChild(documents);
        var policy = new CleanupProtectedPathPolicy(root);

        var status = policy.Classify(file);

        Assert.Multiple(() =>
        {
            Assert.That(status.IsProtected, Is.False);
            Assert.That(status.Reason, Is.EqualTo(CleanupProtectionReason.None));
        });
    }

    [TestCase("Mail")]
    [TestCase("Messages")]
    [TestCase("Safari")]
    [TestCase("Containers")]
    [TestCase("Group Containers")]
    [TestCase("Application Support")]
    public void ClassifyBlocksSensitiveUserLibrarySubtreeDescendant(string subtreeName)
    {
        var root = Directory("home", "/Users/test");
        var library = Directory("Library", "/Users/test/Library");
        var subtree = Directory(subtreeName, $"/Users/test/Library/{subtreeName}");
        var file = File("data.bin", $"/Users/test/Library/{subtreeName}/data.bin", 10, 20);
        subtree.AddChild(file);
        library.AddChild(subtree);
        root.AddChild(library);
        var policy = new CleanupProtectedPathPolicy(root);

        var status = policy.Classify(file);

        Assert.Multiple(() =>
        {
            Assert.That(status.IsProtected, Is.True);
            Assert.That(status.Reason, Is.EqualTo(CleanupProtectionReason.SensitiveLocation));
            Assert.That(status.Message, Does.Contain("user data"));
        });
    }

    [Test]
    public void ClassifyAllowsNonSensitiveUserLibrarySubtreeDescendant()
    {
        var root = Directory("home", "/Users/test");
        var library = Directory("Library", "/Users/test/Library");
        var caches = Directory("Caches", "/Users/test/Library/Caches");
        var file = File("tool.cache", "/Users/test/Library/Caches/tool.cache", 10, 20);
        caches.AddChild(file);
        library.AddChild(caches);
        root.AddChild(library);
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
