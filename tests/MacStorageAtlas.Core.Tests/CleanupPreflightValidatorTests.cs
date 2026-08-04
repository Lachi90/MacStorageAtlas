using MacStorageAtlas.Core;

namespace MacStorageAtlas.Core.Tests;

public class CleanupPreflightValidatorTests
{
    [Test]
    public void ValidateReturnsReadyForUnchangedExecutableItem()
    {
        var root = Directory("scan", "/scan");
        var file = File("file.bin", "/scan/file.bin", 100, 200);
        root.AddChild(file);
        var item = BasketItem(file);
        var reader = new MetadataReader(
            new CleanupFileSystemSnapshot(file.Path, false, 100, 200));
        var validator = Validator(root, reader);

        var result = validator.Validate(item);

        Assert.Multiple(() =>
        {
            Assert.That(result.CanExecute, Is.True);
            Assert.That(result.Status.Kind, Is.EqualTo(CleanupPreflightStatusKind.Ready));
        });
    }

    [Test]
    public void ValidateBlocksMissingItem()
    {
        var root = Directory("scan", "/scan");
        var file = File("file.bin", "/scan/file.bin", 100, 200);
        root.AddChild(file);
        var validator = Validator(root, new MetadataReader());

        var result = validator.Validate(BasketItem(file));

        Assert.Multiple(() =>
        {
            Assert.That(result.CanExecute, Is.False);
            Assert.That(result.Status.Kind, Is.EqualTo(CleanupPreflightStatusKind.Missing));
            Assert.That(result.Status.Message, Does.Contain("no longer exists"));
        });
    }

    [Test]
    public void ValidateBlocksReplacedItemWhenIdentityChanged()
    {
        var root = Directory("scan", "/scan");
        var file = File("file.bin", "/scan/file.bin", 100, 200);
        root.AddChild(file);
        var item = BasketItem(file) with
        {
            Snapshot = CleanupItemSnapshot.FromDiskItem(file) with
            {
                Identity = new FileIdentity(1, 2)
            }
        };
        var reader = new MetadataReader(
            new CleanupFileSystemSnapshot(
                file.Path,
                false,
                100,
                200,
                new FileIdentity(1, 3)));
        var validator = Validator(root, reader);

        var result = validator.Validate(item);

        Assert.Multiple(() =>
        {
            Assert.That(result.CanExecute, Is.False);
            Assert.That(result.Status.Kind, Is.EqualTo(CleanupPreflightStatusKind.IdentityChanged));
            Assert.That(result.Status.Message, Does.Contain("changed"));
        });
    }

    [Test]
    public void ValidateBlocksChangedSize()
    {
        var root = Directory("scan", "/scan");
        var file = File("file.bin", "/scan/file.bin", 100, 200);
        root.AddChild(file);
        var reader = new MetadataReader(
            new CleanupFileSystemSnapshot(file.Path, false, 101, 200));
        var validator = Validator(root, reader);

        var result = validator.Validate(BasketItem(file));

        Assert.Multiple(() =>
        {
            Assert.That(result.CanExecute, Is.False);
            Assert.That(result.Status.Kind, Is.EqualTo(CleanupPreflightStatusKind.SizeChanged));
            Assert.That(result.Status.Message, Does.Contain("changed"));
        });
    }

    [Test]
    public void ValidateBlocksProtectedItemBeforeReadingFilesystemMetadata()
    {
        var root = Directory("scan", "/scan");
        var reader = new MetadataReader();
        var validator = Validator(root, reader);

        var result = validator.Validate(BasketItem(root));

        Assert.Multiple(() =>
        {
            Assert.That(result.CanExecute, Is.False);
            Assert.That(result.Status.Kind, Is.EqualTo(CleanupPreflightStatusKind.Protected));
            Assert.That(reader.ReadCount, Is.Zero);
        });
    }

    [Test]
    public void ValidateUsesMetadataReaderOnly()
    {
        var root = Directory("scan", "/scan");
        var file = File("file.bin", "/scan/file.bin", 100, 200);
        root.AddChild(file);
        var reader = new MetadataOnlyReader(
            new CleanupFileSystemSnapshot(file.Path, false, 100, 200));
        var validator = Validator(root, reader);

        var result = validator.Validate(BasketItem(file));

        Assert.Multiple(() =>
        {
            Assert.That(result.CanExecute, Is.True);
            Assert.That(reader.ReadCount, Is.EqualTo(1));
            Assert.That(reader.ContentReadCount, Is.Zero);
        });
    }

    private static CleanupPreflightValidator Validator(
        DiskItem root,
        ICleanupFileSystemMetadataReader reader) =>
        new(new CleanupProtectedPathPolicy(root), reader);

    private static CleanupBasketItem BasketItem(DiskItem item) =>
        new(
            item,
            CleanupItemSnapshot.FromDiskItem(item),
            CleanupProtectionStatus.NotProtected);

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

    private class MetadataReader(params CleanupFileSystemSnapshot[] snapshots)
        : ICleanupFileSystemMetadataReader
    {
        private readonly Dictionary<string, CleanupFileSystemSnapshot> _snapshots =
            snapshots.ToDictionary(
                snapshot => CleanupProtectedPathPolicy.NormalizePath(snapshot.Path),
                StringComparer.Ordinal);

        public int ReadCount { get; private set; }

        public virtual bool TryReadSnapshot(
            string path,
            out CleanupFileSystemSnapshot snapshot)
        {
            ReadCount++;
            return _snapshots.TryGetValue(
                CleanupProtectedPathPolicy.NormalizePath(path),
                out snapshot!);
        }
    }

    private sealed class MetadataOnlyReader(CleanupFileSystemSnapshot snapshot)
        : MetadataReader(snapshot)
    {
        public int ContentReadCount { get; private set; }

        public string ReadContent(string path)
        {
            ContentReadCount++;
            throw new InvalidOperationException(path);
        }
    }
}
