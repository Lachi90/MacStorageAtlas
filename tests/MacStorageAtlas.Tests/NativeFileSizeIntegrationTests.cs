using System.IO;
using System.Runtime.InteropServices;
using MacStorageAtlas.Platform.Mac;

namespace MacStorageAtlas.Tests;

public sealed class MacFileMetadataReaderIntegrationTests
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
    public async Task ReadReportsAllocatedBlocksForNormalFileOnMacOs()
    {
        RequireMacOs();
        var path = Path.Combine(_temporaryDirectory, "normal.bin");
        await File.WriteAllBytesAsync(path, new byte[1024 * 1024]);

        var metadata = new MacFileMetadataReader().Read(path);

        Assert.Multiple(() =>
        {
            Assert.That(metadata.AllocatedSizeBytes, Is.GreaterThan(0));
            Assert.That(metadata.AllocatedSizeBytes % 512, Is.Zero);
            Assert.That(metadata.Identity.DeviceId, Is.GreaterThan(0));
            Assert.That(metadata.Identity.FileId, Is.GreaterThan(0));
            Assert.That(metadata.LinkCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void ReadReportsLessThanLogicalLengthForSparseFileOnMacOs()
    {
        RequireMacOs();
        var path = Path.Combine(_temporaryDirectory, "sparse.bin");
        const long logicalLength = 1024L * 1024 * 1024;
        using (var stream = File.Create(path))
        {
            stream.SetLength(logicalLength);
        }

        var metadata = new MacFileMetadataReader().Read(path);
        if (metadata.AllocatedSizeBytes >= logicalLength)
        {
            Assert.Ignore("The temporary filesystem did not preserve sparse allocation.");
        }

        Assert.Multiple(() =>
        {
            Assert.That(new FileInfo(path).Length, Is.EqualTo(logicalLength));
            Assert.That(metadata.AllocatedSizeBytes, Is.LessThan(logicalLength));
            Assert.That(metadata.AllocatedSizeBytes % 512, Is.Zero);
        });
    }

    [Test]
    public async Task ReadReportsSameIdentityAndAllocationForHardlinksOnMacOs()
    {
        RequireMacOs();
        var originalPath = Path.Combine(_temporaryDirectory, "original.bin");
        var linkedPath = Path.Combine(_temporaryDirectory, "linked.bin");
        await File.WriteAllBytesAsync(originalPath, new byte[4096]);
        Assert.That(link(originalPath, linkedPath), Is.Zero);
        var reader = new MacFileMetadataReader();

        var original = reader.Read(originalPath);
        var linked = reader.Read(linkedPath);

        Assert.Multiple(() =>
        {
            Assert.That(linked.Identity, Is.EqualTo(original.Identity));
            Assert.That(linked.AllocatedSizeBytes, Is.EqualTo(original.AllocatedSizeBytes));
            Assert.That(original.LinkCount, Is.EqualTo(2));
            Assert.That(linked.LinkCount, Is.EqualTo(2));
        });
    }

    [Test]
    public void ReadDoesNotFallbackForMissingFileOnMacOs()
    {
        RequireMacOs();
        var missingPath = Path.Combine(_temporaryDirectory, "missing.bin");

        Assert.That(
            () => new MacFileMetadataReader().Read(missingPath),
            Throws.InstanceOf<IOException>());
    }

    private static void RequireMacOs()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Ignore("macOS-specific allocated metadata integration.");
        }
    }

    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int link(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string existingPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string newPath);
}
