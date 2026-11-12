using MacStorageAtlas.Core.Cleanup;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;
using NSubstitute;

namespace MacStorageAtlas.Core.Tests.Cleanup;

public class CleanupFileSystemMetadataReaderTests
{
    private string _directory = null!;
    private string _file = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"MacStorageAtlas-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _file = Path.Combine(_directory, "file.bin");
        File.WriteAllBytes(_file, new byte[10]);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public void TryReadSnapshotReportsLogicalLengthAndAllocatedSize()
    {
        var allocatedReader = Substitute.For<IAllocatedFileMetadataReader>();
        allocatedReader.Read(_file).Returns(
            new AllocatedFileMetadata(4096, new FileIdentity(1, 2), LinkCount: 1));
        var reader = new CleanupFileSystemMetadataReader(allocatedReader);

        var found = reader.TryReadSnapshot(_file, out var snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(snapshot.SizeBytes, Is.EqualTo(10));
            Assert.That(snapshot.MeasuredSizeBytes, Is.EqualTo(4096));
        });
    }

    [Test]
    public void TryReadSnapshotFallsBackToLogicalLengthWhenAllocatedReadFails()
    {
        var allocatedReader = Substitute.For<IAllocatedFileMetadataReader>();
        allocatedReader.Read(_file).Returns(_ => throw new IOException("Unreadable."));
        var reader = new CleanupFileSystemMetadataReader(allocatedReader);

        var found = reader.TryReadSnapshot(_file, out var snapshot);

        Assert.Multiple(() =>
        {
            Assert.That(found, Is.True);
            Assert.That(snapshot.SizeBytes, Is.EqualTo(10));
            Assert.That(snapshot.MeasuredSizeBytes, Is.EqualTo(10));
        });
    }

    [Test]
    public void TryReadSnapshotWithoutAllocatedReaderReportsLogicalLength()
    {
        var reader = new CleanupFileSystemMetadataReader();

        reader.TryReadSnapshot(_file, out var snapshot);

        Assert.That(snapshot.MeasuredSizeBytes, Is.EqualTo(10));
    }

    [Test]
    public void FailedAllocatedReadBlocksAllocatedRevalidation()
    {
        var allocatedReader = Substitute.For<IAllocatedFileMetadataReader>();
        allocatedReader.Read(_file).Returns(_ => throw new IOException("Unreadable."));
        var root = new DiskItem("scan", _directory, isDirectory: true);
        var file = new DiskItem("file.bin", _file, isDirectory: false)
        {
            SizeBytes = 4096,
            MeasuredSizeBytes = 4096
        };
        root.AddChild(file);
        var validator = new CleanupPreflightValidator(
            new CleanupProtectedPathPolicy(root),
            new CleanupFileSystemMetadataReader(allocatedReader),
            StorageMeasurementMode.Allocated);

        var result = validator.Validate(new CleanupBasketItem(
            file,
            CleanupItemSnapshot.FromDiskItem(file),
            CleanupProtectionStatus.NotProtected));

        Assert.That(result.CanExecute, Is.False);
    }
}
