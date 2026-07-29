using MacStorageAtlas.Core;

namespace MacStorageAtlas.Tests;

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
