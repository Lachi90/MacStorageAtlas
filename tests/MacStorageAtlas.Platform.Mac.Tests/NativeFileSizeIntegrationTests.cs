using System.IO;
using System.Runtime.InteropServices;
using MacStorageAtlas.Platform.Mac;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Platform.Mac.Tests;

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
            Assert.That(
                metadata.CloneAccountingCoverage,
                Is.AnyOf(
                    CloneAccountingCoverage.Available,
                    CloneAccountingCoverage.Unavailable,
                    CloneAccountingCoverage.Partial));
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

    [Test]
    public async Task SharedAwareScanAccountsDisposableApfsCloneFixtures()
    {
        RequireMacOs();
        var originalPath = Path.Combine(_temporaryDirectory, "original.bin");
        var clonePath = Path.Combine(_temporaryDirectory, "clone.bin");
        var divergentPath = Path.Combine(_temporaryDirectory, "divergent.bin");
        var hardlinkPath = Path.Combine(_temporaryDirectory, "hardlink.bin");
        var ordinaryPath = Path.Combine(_temporaryDirectory, "ordinary.bin");
        var sparsePath = Path.Combine(_temporaryDirectory, "sparse.bin");
        var forkSourcePath = Path.Combine(_temporaryDirectory, "fork-source.bin");
        var forkClonePath = Path.Combine(_temporaryDirectory, "fork-clone.bin");
        await File.WriteAllBytesAsync(originalPath, new byte[1024 * 1024]);
        await File.WriteAllBytesAsync(ordinaryPath, new byte[1024 * 1024]);
        await File.WriteAllBytesAsync(forkSourcePath, new byte[1024 * 1024]);
        using (var sparse = File.Create(sparsePath))
        {
            sparse.SetLength(1024L * 1024 * 1024);
        }

        if (clonefile(originalPath, clonePath, 0) != 0
            || clonefile(originalPath, divergentPath, 0) != 0
            || clonefile(forkSourcePath, forkClonePath, 0) != 0)
        {
            Assert.Ignore("The temporary filesystem could not create APFS clones.");
        }

        Assert.That(link(originalPath, hardlinkPath), Is.Zero);
        using (var divergentStream = new FileStream(
                   divergentPath,
                   FileMode.Open,
                   FileAccess.Write,
                   FileShare.Read))
        {
            divergentStream.Position = 4096;
            divergentStream.WriteByte(1);
        }

        try
        {
            await File.WriteAllBytesAsync(
                $"{forkClonePath}/..namedfork/rsrc",
                new byte[8192]);
        }
        catch (IOException)
        {
            Assert.Ignore("The temporary filesystem could not create a resource fork.");
        }

        var reader = new MacFileMetadataReader();
        var original = reader.Read(originalPath);
        var clone = reader.Read(clonePath);
        var divergent = reader.Read(divergentPath);
        var hardlink = reader.Read(hardlinkPath);
        var ordinary = reader.Read(ordinaryPath);
        var sparseMetadata = reader.Read(sparsePath);
        var forkSource = reader.Read(forkSourcePath);
        var forkClone = reader.Read(forkClonePath);

        if (original.CloneAccountingCoverage != CloneAccountingCoverage.Available)
        {
            Assert.Ignore("The temporary APFS volume does not advertise clone mapping.");
        }

        if (original.SharedDataIdentity is null
            || clone.SharedDataIdentity != original.SharedDataIdentity
            || forkSource.SharedDataIdentity is null
            || forkClone.SharedDataIdentity != forkSource.SharedDataIdentity)
        {
            Assert.Ignore("The temporary filesystem did not report verified full clones.");
        }

        var expectedSize =
            original.AllocatedSizeBytes
            + clone.AllocatedSizeBytes
            - clone.DataAllocatedSizeBytes!.Value
            + divergent.AllocatedSizeBytes
            + ordinary.AllocatedSizeBytes
            + sparseMetadata.AllocatedSizeBytes
            + forkSource.AllocatedSizeBytes
            + forkClone.AllocatedSizeBytes
            - forkClone.DataAllocatedSizeBytes!.Value;
        var scanner = new DiskScanner(reader);
        var options = new ScanOptions
        {
            IncludeHiddenFiles = true,
            MeasurementMode = StorageMeasurementMode.SharedAwareAllocated
        };

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory, options));

        var result = progress[^1];
        Assert.Multiple(() =>
        {
            Assert.That(hardlink.Identity, Is.EqualTo(original.Identity));
            Assert.That(divergent.Identity, Is.Not.EqualTo(original.Identity));
            Assert.That(
                divergent.SharedDataIdentity,
                Is.Not.EqualTo(original.SharedDataIdentity));
            Assert.That(result.FilesScanned, Is.EqualTo(8));
            Assert.That(result.Root.Children, Has.Count.EqualTo(8));
            Assert.That(result.BytesScanned, Is.EqualTo(expectedSize));
            Assert.That(
                result.Root.MeasuredSizeBytes,
                Is.EqualTo(
                    original.AllocatedSizeBytes
                    + clone.AllocatedSizeBytes
                    + divergent.AllocatedSizeBytes
                    + hardlink.AllocatedSizeBytes
                    + ordinary.AllocatedSizeBytes
                    + sparseMetadata.AllocatedSizeBytes
                    + forkSource.AllocatedSizeBytes
                    + forkClone.AllocatedSizeBytes));
            Assert.That(
                result.CloneAccountingCoverage,
                Is.EqualTo(CloneAccountingCoverage.Available));
            Assert.That(
                result.Root.Children.Single(item => item.Path == forkClonePath).SizeBytes,
                Is.EqualTo(
                    forkClone.AllocatedSizeBytes
                    - forkClone.DataAllocatedSizeBytes.Value));
        });
    }

    private static async Task<List<ScanProgress>> CollectAsync(
        IAsyncEnumerable<ScanProgress> source)
    {
        var progress = new List<ScanProgress>();
        await foreach (var item in source)
        {
            progress.Add(item);
        }

        return progress;
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

    [DllImport("libc", EntryPoint = "clonefile", SetLastError = true)]
    private static extern int clonefile(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string sourcePath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string destinationPath,
        uint flags);
}
