using System.IO;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.Tests;

public sealed class NativeFileSizeIntegrationTests
{
    private string _temporaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"MacStorageAtlas-native-size-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    [Test]
    public async Task GetAllocatedSizeBytesReportsAllocatedBlocksForNormalFileOnMacOs()
    {
        RequireMacOs();
        var path = Path.Combine(_temporaryDirectory, "normal.bin");
        await File.WriteAllBytesAsync(path, new byte[1024 * 1024]);

        var allocatedBytes = NativeFileSize.GetAllocatedSizeBytes(path);

        Assert.Multiple(() =>
        {
            Assert.That(allocatedBytes, Is.GreaterThan(0));
            Assert.That(allocatedBytes % 512, Is.Zero);
        });
    }

    [Test]
    public void GetAllocatedSizeBytesReportsLessThanLogicalLengthForSparseFileOnMacOs()
    {
        RequireMacOs();
        var path = Path.Combine(_temporaryDirectory, "sparse.bin");
        const long logicalLength = 1024L * 1024 * 1024;
        using (var stream = File.Create(path))
        {
            stream.SetLength(logicalLength);
        }

        var allocatedBytes = NativeFileSize.GetAllocatedSizeBytes(path);
        if (allocatedBytes >= logicalLength)
        {
            Assert.Ignore("The temporary filesystem did not preserve sparse allocation.");
        }

        Assert.Multiple(() =>
        {
            Assert.That(new FileInfo(path).Length, Is.EqualTo(logicalLength));
            Assert.That(allocatedBytes, Is.LessThan(logicalLength));
            Assert.That(allocatedBytes % 512, Is.Zero);
        });
    }

    private static void RequireMacOs()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Ignore("macOS-specific allocated metadata integration.");
        }
    }
}
