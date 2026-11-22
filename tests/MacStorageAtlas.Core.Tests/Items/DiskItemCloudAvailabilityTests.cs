using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.Core.Tests.Items;

public class DiskItemCloudAvailabilityTests
{
    [Test]
    public void AFileReportsTheAvailabilityOfItsMetadata()
    {
        var file = File("/root/a.bin", CloudAvailability.CloudOnly, 900);

        Assert.Multiple(() =>
        {
            Assert.That(file.CloudAvailability, Is.EqualTo(CloudAvailability.CloudOnly));
            Assert.That(file.CloudTotals.CloudOnlyFileCount, Is.EqualTo(1));
            Assert.That(file.CloudTotals.CloudOnlyLogicalSizeBytes, Is.EqualTo(900));
        });
    }

    [Test]
    public void AFileWithoutAStatusIsUnknown()
    {
        var file = new DiskItem("a.bin", "/root/a.bin", isDirectory: false);

        Assert.Multiple(() =>
        {
            Assert.That(file.CloudAvailability, Is.EqualTo(CloudAvailability.Unknown));
            Assert.That(file.CloudTotals.UnknownFileCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void AFolderWithoutFilesIsLocal()
    {
        var folder = new DiskItem("folder", "/root/folder", isDirectory: true);

        Assert.Multiple(() =>
        {
            Assert.That(folder.CloudAvailability, Is.EqualTo(CloudAvailability.Local));
            Assert.That(folder.CloudTotals, Is.EqualTo(CloudAvailabilityTotals.Empty));
        });
    }

    [Test]
    public void RemoveDescendantKeepsAncestorAvailabilityAndCloudOnlySizeCurrent()
    {
        var root = Folder("/root");
        var folder = Folder("/root/folder");
        var local = File("/root/folder/local.bin", CloudAvailability.Local);
        var placeholder = File("/root/folder/placeholder.bin", CloudAvailability.CloudOnly, 4_000);
        var other = File("/root/other.bin", CloudAvailability.CloudOnly, 1_000);
        Attach(folder, local, placeholder);
        Attach(root, folder, other);

        var removed = root.RemoveDescendant(local);

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.True);
            Assert.That(folder.CloudAvailability, Is.EqualTo(CloudAvailability.CloudOnly));
            Assert.That(root.CloudAvailability, Is.EqualTo(CloudAvailability.CloudOnly));
            Assert.That(root.CloudTotals.CloudOnlyLogicalSizeBytes, Is.EqualTo(5_000));
            Assert.That(root.CloudTotals.LocalFileCount, Is.Zero);
        });
    }

    [Test]
    public void RemovingTheLastCloudOnlyFileMakesAncestorsLocal()
    {
        var root = Folder("/root");
        var folder = Folder("/root/folder");
        var local = File("/root/local.bin", CloudAvailability.Local);
        var placeholder = File("/root/folder/placeholder.bin", CloudAvailability.CloudOnly, 4_000);
        Attach(folder, placeholder);
        Attach(root, folder, local);

        root.RemoveDescendant(folder);

        Assert.Multiple(() =>
        {
            Assert.That(root.CloudAvailability, Is.EqualTo(CloudAvailability.Local));
            Assert.That(root.CloudTotals.CloudOnlyLogicalSizeBytes, Is.Zero);
            Assert.That(root.CloudTotals.CloudOnlyFileCount, Is.Zero);
        });
    }

    [TestCase(0L, 0L, 0L, 0L, CloudAvailability.Local)]
    [TestCase(2L, 0L, 0L, 0L, CloudAvailability.Local)]
    [TestCase(0L, 2L, 0L, 0L, CloudAvailability.CloudOnly)]
    [TestCase(1L, 1L, 0L, 0L, CloudAvailability.PartlyLocal)]
    [TestCase(1L, 0L, 1L, 0L, CloudAvailability.Unknown)]
    [TestCase(0L, 1L, 1L, 0L, CloudAvailability.PartlyLocal)]
    [TestCase(0L, 0L, 0L, 1L, CloudAvailability.CloudOnly)]
    [TestCase(1L, 0L, 0L, 1L, CloudAvailability.PartlyLocal)]
    public void FolderAvailabilityFollowsTheIncludedFiles(
        long local,
        long cloudOnly,
        long unknown,
        long unlistedFolders,
        CloudAvailability expected)
    {
        var totals = new CloudAvailabilityTotals(local, cloudOnly, unknown, 0, unlistedFolders);

        Assert.That(totals.FolderAvailability, Is.EqualTo(expected));
    }

    private static DiskItem Folder(string path) =>
        new(System.IO.Path.GetFileName(path), path, isDirectory: true);

    private static DiskItem File(
        string path,
        CloudAvailability availability,
        long cloudOnlyLogicalSizeBytes = 0)
    {
        var file = new DiskItem(System.IO.Path.GetFileName(path), path, isDirectory: false)
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
