using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.Core.Tests.Items;

public class DiskItemTests
{
    [Test]
    public void MeasuredSizeDefaultsToCountedSize()
    {
        var item = new DiskItem("file.bin", "/file.bin", isDirectory: false)
        {
            SizeBytes = 4096
        };

        Assert.That(item.MeasuredSizeBytes, Is.EqualTo(4096));
    }

    [Test]
    public void MetadataDefaultsToUnknownFileOrDirectoryKind()
    {
        var file = new DiskItem("file.bin", "/file.bin", isDirectory: false);
        var directory = new DiskItem("folder", "/folder", isDirectory: true);

        Assert.Multiple(() =>
        {
            Assert.That(file.Metadata.Kind, Is.EqualTo(DiskItemKind.File));
            Assert.That(file.Metadata.ModifiedTimeUtc, Is.Null);
            Assert.That(file.Metadata.CreatedTimeUtc, Is.Null);
            Assert.That(directory.Metadata.Kind, Is.EqualTo(DiskItemKind.Directory));
            Assert.That(directory.Metadata.ModifiedTimeUtc, Is.Null);
        });
    }

    [Test]
    public void MetadataCanBeAttachedWithoutChangingSizesOrChildren()
    {
        var modified = new DateTimeOffset(2026, 7, 29, 10, 15, 0, TimeSpan.Zero);
        var root = new DiskItem("root", "/root", isDirectory: true)
        {
            SizeBytes = 4096,
            Metadata = new DiskItemMetadata(
                DiskItemKind.Directory,
                FileAttributes.Directory,
                CreatedTimeUtc: null,
                modified,
                LastAccessTimeUtc: null)
        };
        var child = new DiskItem("file.bin", "/root/file.bin", isDirectory: false);

        root.AddChild(child);

        Assert.Multiple(() =>
        {
            Assert.That(root.Metadata.ModifiedTimeUtc, Is.EqualTo(modified));
            Assert.That(root.SizeBytes, Is.EqualTo(4096));
            Assert.That(root.Children.Single(), Is.SameAs(child));
        });
    }

    [Test]
    public void RemoveDescendantSubtractsCountedAndMeasuredSizes()
    {
        var root = new DiskItem("root", "/root", isDirectory: true)
        {
            SizeBytes = 4096,
            MeasuredSizeBytes = 8192,
            SharedSizeBytes = 4096
        };
        var counted = new DiskItem(
            "counted.bin",
            "/root/counted.bin",
            isDirectory: false)
        {
            SizeBytes = 4096,
            MeasuredSizeBytes = 4096
        };
        var shared = new DiskItem(
            "shared.bin",
            "/root/shared.bin",
            isDirectory: false)
        {
            SizeBytes = 0,
            MeasuredSizeBytes = 4096,
            SharedSizeBytes = 4096
        };
        root.AddChild(counted);
        root.AddChild(shared);

        var removed = root.RemoveDescendant(shared);

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.True);
            Assert.That(root.SizeBytes, Is.EqualTo(4096));
            Assert.That(root.MeasuredSizeBytes, Is.EqualTo(4096));
            Assert.That(root.SharedSizeBytes, Is.Zero);
            Assert.That(root.Children, Is.EqualTo(new[] { counted }));
        });
    }

    [Test]
    public void SharedBytesSupplementCountedContribution()
    {
        var item = new DiskItem("clone.bin", "/clone.bin", isDirectory: false)
        {
            SizeBytes = 1024,
            MeasuredSizeBytes = 5120,
            SharedSizeBytes = 4096
        };

        Assert.Multiple(() =>
        {
            Assert.That(item.IsSizeCountedElsewhere, Is.True);
            Assert.That(
                item.MeasuredSizeBytes,
                Is.EqualTo(item.SizeBytes + item.SharedSizeBytes));
        });
    }
}
