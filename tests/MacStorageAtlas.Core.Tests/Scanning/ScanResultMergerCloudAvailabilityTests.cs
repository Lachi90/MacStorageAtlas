using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Tests.Scanning;

public class ScanResultMergerCloudAvailabilityTests
{
    [Test]
    public void ReplaceSubtreeRecomputesAvailabilityAlongTheAncestorPath()
    {
        var tree = Tree();
        var replacement = Folder("/root/a/project");
        Attach(replacement, File("/root/a/project/local.bin", CloudAvailability.Local));

        ScanResultMerger.ReplaceSubtree(tree.Root, "/root/a/project", replacement);

        Assert.Multiple(() =>
        {
            Assert.That(tree.A.CloudAvailability, Is.EqualTo(CloudAvailability.Local));
            Assert.That(tree.Root.CloudAvailability, Is.EqualTo(CloudAvailability.PartlyLocal));
            Assert.That(tree.A.CloudTotals.CloudOnlyLogicalSizeBytes, Is.Zero);
            Assert.That(tree.Root.CloudTotals.CloudOnlyLogicalSizeBytes, Is.EqualTo(300));
            Assert.That(tree.Root.CloudTotals.LocalFileCount, Is.EqualTo(2));
        });
    }

    [Test]
    public void ReplaceSubtreeAddsCloudOnlySizeOfTheRefreshedFolderToEveryAncestor()
    {
        var tree = Tree();
        var replacement = Folder("/root/a/project");
        Attach(
            replacement,
            File("/root/a/project/one.bin", CloudAvailability.CloudOnly, 1_000),
            File("/root/a/project/two.bin", CloudAvailability.CloudOnly, 2_000));

        ScanResultMerger.ReplaceSubtree(tree.Root, "/root/a/project", replacement);

        Assert.Multiple(() =>
        {
            Assert.That(tree.A.CloudAvailability, Is.EqualTo(CloudAvailability.PartlyLocal));
            Assert.That(tree.A.CloudTotals.CloudOnlyLogicalSizeBytes, Is.EqualTo(3_000));
            Assert.That(tree.A.CloudTotals.CloudOnlyFileCount, Is.EqualTo(2));
            Assert.That(tree.Root.CloudTotals.CloudOnlyLogicalSizeBytes, Is.EqualTo(3_300));
            Assert.That(tree.Root.CloudTotals.CloudOnlyFileCount, Is.EqualTo(3));
        });
    }

    [Test]
    public void ReplaceSubtreeWithoutReplacementRemovesTheFolderAvailability()
    {
        var tree = Tree();

        ScanResultMerger.ReplaceSubtree(tree.Root, "/root/a/project", null);

        Assert.Multiple(() =>
        {
            Assert.That(tree.A.CloudAvailability, Is.EqualTo(CloudAvailability.Local));
            Assert.That(tree.Root.CloudTotals.CloudOnlyFileCount, Is.EqualTo(1));
            Assert.That(tree.Root.CloudTotals.CloudOnlyLogicalSizeBytes, Is.EqualTo(300));
        });
    }

    [Test]
    public void MergeUnlistedCloudFoldersReplacesOnlyEntriesInsideTheRefreshedFolder()
    {
        var merged = ScanResultMerger.MergeUnlistedCloudFolders(
            ["/root/a/project/old", "/root/b/kept"],
            "/root/a/project",
            ["/root/a/project/new"]);

        Assert.That(merged, Is.EqualTo(new[] { "/root/b/kept", "/root/a/project/new" }));
    }

    [Test]
    public void MergeUnlistedCloudFoldersKeepsEachFolderOnce()
    {
        var merged = ScanResultMerger.MergeUnlistedCloudFolders(
            ["/root/b/kept"],
            "/root/a",
            ["/root/b/kept"]);

        Assert.That(merged, Is.EqualTo(new[] { "/root/b/kept" }));
    }

    private static (DiskItem Root, DiskItem A) Tree()
    {
        var root = Folder("/root");
        var a = Folder("/root/a");
        var project = Folder("/root/a/project");
        Attach(project, File("/root/a/project/cloud.bin", CloudAvailability.CloudOnly, 5_000));
        Attach(a, project, File("/root/a/keep.bin", CloudAvailability.Local));
        var b = Folder("/root/b");
        Attach(b, File("/root/b/placeholder.bin", CloudAvailability.CloudOnly, 300));
        Attach(root, a, b);
        return (root, a);
    }

    private static DiskItem Folder(string path) =>
        new(Path.GetFileName(path), path, isDirectory: true);

    private static DiskItem File(
        string path,
        CloudAvailability availability,
        long cloudOnlyLogicalSizeBytes = 0)
    {
        var file = new DiskItem(Path.GetFileName(path), path, isDirectory: false)
        {
            Metadata = DiskItemMetadata.Unknown(DiskItemKind.File) with
            {
                CloudAvailability = availability
            }
        };
        file.SetCloudTotals(CloudAvailabilityTotals.ForFile(availability, cloudOnlyLogicalSizeBytes));
        return file;
    }

    private static void Attach(DiskItem parent, params DiskItem[] children)
    {
        foreach (var child in children)
        {
            parent.AddChild(child);
            parent.AddCloudTotals(child.CloudTotals);
        }
    }
}
