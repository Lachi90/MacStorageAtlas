using MacStorageAtlas.Core.Cleanup;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Relocation;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Tests.Relocation;

public class RelocationPreflightValidatorTests
{
    [Test]
    public void ValidateReturnsReadyToMoveForEligibleItem()
    {
        var root = Directory("scan", "/scan");
        var file = File("file.bin", "/scan/file.bin", 100, 200);
        root.AddChild(file);
        var validator = Validator(root, Snapshot(file));

        var result = validator.Validate(
            BasketItem(file),
            Destination("/Volumes/Archive"),
            CleanupOperationKind.Move);

        Assert.Multiple(() =>
        {
            Assert.That(result.CanExecute, Is.True);
            Assert.That(result.Status.Message, Does.Contain("move to the destination"));
            Assert.That(result.Status.Message, Does.Not.Contain("Trash"));
        });
    }

    [Test]
    public void ValidateReturnsReadyToCopyForEligibleItem()
    {
        var root = Directory("scan", "/scan");
        var file = File("file.bin", "/scan/file.bin", 100, 200);
        root.AddChild(file);
        var validator = Validator(root, Snapshot(file));

        var result = validator.Validate(
            BasketItem(file),
            Destination("/Volumes/Archive"),
            CleanupOperationKind.Copy);

        Assert.Multiple(() =>
        {
            Assert.That(result.CanExecute, Is.True);
            Assert.That(result.Status.Message, Does.Contain("copy to the destination"));
            Assert.That(result.Status.Message, Does.Not.Contain("Trash"));
        });
    }

    [Test]
    public void ValidateBlocksProtectedItem()
    {
        var root = Directory("scan", "/scan");
        var validator = Validator(root);

        var result = validator.Validate(
            BasketItem(root),
            Destination("/Volumes/Archive"),
            CleanupOperationKind.Move);

        Assert.Multiple(() =>
        {
            Assert.That(result.CanExecute, Is.False);
            Assert.That(result.Status.Kind, Is.EqualTo(CleanupPreflightStatusKind.Protected));
        });
    }

    [Test]
    public void ValidateBlocksMissingSource()
    {
        var root = Directory("scan", "/scan");
        var file = File("file.bin", "/scan/file.bin", 100, 200);
        root.AddChild(file);
        var validator = Validator(root);

        var result = validator.Validate(
            BasketItem(file),
            Destination("/Volumes/Archive"),
            CleanupOperationKind.Move);

        Assert.Multiple(() =>
        {
            Assert.That(result.CanExecute, Is.False);
            Assert.That(result.Status.Kind, Is.EqualTo(CleanupPreflightStatusKind.Missing));
        });
    }

    [Test]
    public void ValidateBlocksSourceWhoseIdentityChanged()
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
        var validator = Validator(
            root,
            new CleanupFileSystemSnapshot(file.Path, false, 100, 200, new FileIdentity(1, 3)));

        var result = validator.Validate(
            item,
            Destination("/Volumes/Archive"),
            CleanupOperationKind.Move);

        Assert.Multiple(() =>
        {
            Assert.That(result.CanExecute, Is.False);
            Assert.That(
                result.Status.Kind,
                Is.EqualTo(CleanupPreflightStatusKind.IdentityChanged));
        });
    }

    [Test]
    public void ValidateBlocksSourceWhoseSizeChanged()
    {
        var root = Directory("scan", "/scan");
        var file = File("file.bin", "/scan/file.bin", 100, 200);
        root.AddChild(file);
        var validator = Validator(
            root,
            new CleanupFileSystemSnapshot(file.Path, false, 101, 200));

        var result = validator.Validate(
            BasketItem(file),
            Destination("/Volumes/Archive"),
            CleanupOperationKind.Move);

        Assert.Multiple(() =>
        {
            Assert.That(result.CanExecute, Is.False);
            Assert.That(result.Status.Kind, Is.EqualTo(CleanupPreflightStatusKind.SizeChanged));
        });
    }

    [Test]
    public void ValidateBlocksCollidingDestinationName()
    {
        var root = Directory("scan", "/scan");
        var file = File("Archive.zip", "/scan/Archive.zip", 100, 200);
        root.AddChild(file);
        var validator = Validator(
            root,
            new[] { Snapshot(file) },
            "/Volumes/Archive/Archive.zip");

        var result = validator.Validate(
            BasketItem(file),
            Destination("/Volumes/Archive"),
            CleanupOperationKind.Copy);

        Assert.Multiple(() =>
        {
            Assert.That(result.CanExecute, Is.False);
            Assert.That(
                result.Status.Kind,
                Is.EqualTo(CleanupPreflightStatusKind.DestinationCollision));
            Assert.That(result.Status.Message, Does.Contain("same name already exists"));
        });
    }

    [Test]
    public void ValidateBlocksDestinationInsideSourceDirectory()
    {
        var root = Directory("scan", "/scan");
        var directory = Directory("media", "/scan/media");
        root.AddChild(directory);
        var validator = Validator(root, Snapshot(directory));

        var result = validator.Validate(
            BasketItem(directory),
            Destination("/scan/media/inner"),
            CleanupOperationKind.Move);

        Assert.Multiple(() =>
        {
            Assert.That(result.CanExecute, Is.False);
            Assert.That(
                result.Status.Kind,
                Is.EqualTo(CleanupPreflightStatusKind.DestinationInsideSource));
            Assert.That(result.Status.Message, Does.Contain("into itself"));
        });
    }

    [Test]
    public void ValidateBlocksDestinationEqualToSourceDirectory()
    {
        var root = Directory("scan", "/scan");
        var directory = Directory("media", "/scan/media");
        root.AddChild(directory);
        var validator = Validator(root, Snapshot(directory));

        var result = validator.Validate(
            BasketItem(directory),
            Destination("/scan/media"),
            CleanupOperationKind.Move);

        Assert.That(
            result.Status.Kind,
            Is.EqualTo(CleanupPreflightStatusKind.DestinationInsideSource));
    }

    [Test]
    public void ValidateBlocksMoveIntoTheSourceParent()
    {
        var root = Directory("scan", "/scan");
        var file = File("file.bin", "/scan/file.bin", 100, 200);
        root.AddChild(file);
        var validator = Validator(root, Snapshot(file));

        var result = validator.Validate(
            BasketItem(file),
            Destination("/scan"),
            CleanupOperationKind.Move);

        Assert.Multiple(() =>
        {
            Assert.That(result.CanExecute, Is.False);
            Assert.That(
                result.Status.Kind,
                Is.EqualTo(CleanupPreflightStatusKind.AlreadyAtDestination));
            Assert.That(result.Status.Message, Does.Contain("already at the destination"));
        });
    }

    [Test]
    public void ValidateBlocksCopyIntoTheSourceParentAsACollision()
    {
        var root = Directory("scan", "/scan");
        var file = File("file.bin", "/scan/file.bin", 100, 200);
        root.AddChild(file);
        var validator = Validator(
            root,
            new[] { Snapshot(file) },
            "/scan/file.bin");

        var result = validator.Validate(
            BasketItem(file),
            Destination("/scan"),
            CleanupOperationKind.Copy);

        Assert.Multiple(() =>
        {
            Assert.That(result.CanExecute, Is.False);
            Assert.That(
                result.Status.Kind,
                Is.EqualTo(CleanupPreflightStatusKind.DestinationCollision));
        });
    }

    [Test]
    public void ValidateKeepsNonCollidingItemsExecutableAlongsideABlockedItem()
    {
        var root = Directory("scan", "/scan");
        var colliding = File("Archive.zip", "/scan/Archive.zip", 100, 200);
        var eligible = File("clip.mov", "/scan/clip.mov", 300, 400);
        root.AddChild(colliding);
        root.AddChild(eligible);
        var validator = Validator(
            root,
            new[] { Snapshot(colliding), Snapshot(eligible) },
            "/Volumes/Archive/Archive.zip");

        var results = validator.Validate(
            [BasketItem(colliding), BasketItem(eligible)],
            Destination("/Volumes/Archive"),
            CleanupOperationKind.Move);

        Assert.Multiple(() =>
        {
            Assert.That(results, Has.Count.EqualTo(2));
            Assert.That(results[0].CanExecute, Is.False);
            Assert.That(
                results[0].Status.Kind,
                Is.EqualTo(CleanupPreflightStatusKind.DestinationCollision));
            Assert.That(results[1].CanExecute, Is.True);
        });
    }

    [Test]
    public void ValidateChecksSourceStateBeforeProbingTheDestination()
    {
        var root = Directory("scan", "/scan");
        var file = File("file.bin", "/scan/file.bin", 100, 200);
        root.AddChild(file);
        var probe = new Probe();
        var validator = new RelocationPreflightValidator(
            new CleanupPreflightValidator(
                new CleanupProtectedPathPolicy(root),
                new MetadataReader()),
            probe);

        validator.Validate(
            BasketItem(file),
            Destination("/Volumes/Archive"),
            CleanupOperationKind.Move);

        Assert.That(probe.ExistsCallCount, Is.Zero);
    }

    private static RelocationPreflightValidator Validator(
        DiskItem root,
        params CleanupFileSystemSnapshot[] snapshots) =>
        Validator(root, snapshots, []);

    private static RelocationPreflightValidator Validator(
        DiskItem root,
        CleanupFileSystemSnapshot[] snapshots,
        params string[] existingDestinationPaths) =>
        new(
            new CleanupPreflightValidator(
                new CleanupProtectedPathPolicy(root),
                new MetadataReader(snapshots)),
            new Probe(existingDestinationPaths));

    private static CleanupFileSystemSnapshot Snapshot(DiskItem item) =>
        new(
            item.Path,
            item.IsDirectory,
            item.SizeBytes,
            item.MeasuredSizeBytes);

    private static RelocationDestination Destination(string path) =>
        RelocationDestination.FromPath(path);

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

    private sealed class MetadataReader(params CleanupFileSystemSnapshot[] snapshots)
        : ICleanupFileSystemMetadataReader
    {
        private readonly Dictionary<string, CleanupFileSystemSnapshot> _snapshots =
            snapshots.ToDictionary(
                snapshot => CleanupProtectedPathPolicy.NormalizePath(snapshot.Path),
                StringComparer.Ordinal);

        public bool TryReadSnapshot(string path, out CleanupFileSystemSnapshot snapshot) =>
            _snapshots.TryGetValue(
                CleanupProtectedPathPolicy.NormalizePath(path),
                out snapshot!);
    }

    private sealed class Probe(params string[] existingPaths) : IRelocationDestinationProbe
    {
        private readonly HashSet<string> _existingPaths = new(
            existingPaths.Select(CleanupProtectedPathPolicy.NormalizePath),
            StringComparer.Ordinal);

        public int ExistsCallCount { get; private set; }

        public bool Exists(string path)
        {
            ExistsCallCount++;
            return _existingPaths.Contains(
                CleanupProtectedPathPolicy.NormalizePath(path));
        }

        public bool IsDirectory(string path) => Exists(path);

        public bool IsWritable(string path) => true;

        public RelocationFreeSpace GetFreeSpace(string path) =>
            RelocationFreeSpace.FromBytes(long.MaxValue);
    }
}
