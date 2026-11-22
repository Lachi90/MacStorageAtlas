using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Tests.Scanning;

public class DiskScannerCloudAvailabilityTests
{
    private string _root = null!;
    private readonly List<string> _enumeratedDirectories = [];
    private readonly Dictionary<string, CloudAvailabilityReading> _readings =
        new(StringComparer.Ordinal);

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            $"MacStorageAtlas-cloud-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
        _enumeratedDirectories.Clear();
        _readings.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [TestCase(StorageMeasurementMode.Logical)]
    [TestCase(StorageMeasurementMode.Allocated)]
    [TestCase(StorageMeasurementMode.SharedAwareAllocated)]
    public async Task ScanRecordsEachFileAvailabilityInEveryMeasurementMode(
        StorageMeasurementMode mode)
    {
        var local = await FileAsync("local.bin", 10);
        var placeholder = await FileAsync("placeholder.bin", 20);
        var unreadable = await FileAsync("unreadable.bin", 30);
        CloudOnly(placeholder, 5_000);
        Unknown(unreadable);

        var result = await ScanAsync(mode);

        Assert.Multiple(() =>
        {
            Assert.That(Item(result.Root, local).CloudAvailability, Is.EqualTo(CloudAvailability.Local));
            Assert.That(Item(result.Root, placeholder).CloudAvailability, Is.EqualTo(CloudAvailability.CloudOnly));
            Assert.That(Item(result.Root, unreadable).CloudAvailability, Is.EqualTo(CloudAvailability.Unknown));
            Assert.That(
                Item(result.Root, placeholder).Metadata.CloudAvailability,
                Is.EqualTo(CloudAvailability.CloudOnly));
            Assert.That(result.Errors, Is.Empty);
        });
    }

    [TestCase(StorageMeasurementMode.Logical, 20)]
    [TestCase(StorageMeasurementMode.Allocated, 4096)]
    public async Task ScanKeepsTheMeasuredSizeOfACloudOnlyFileUnchanged(
        StorageMeasurementMode mode,
        long expectedSize)
    {
        var placeholder = await FileAsync("placeholder.bin", 20);
        CloudOnly(placeholder, 20);

        var result = await ScanAsync(mode);

        var file = Item(result.Root, placeholder);
        Assert.Multiple(() =>
        {
            Assert.That(file.SizeBytes, Is.EqualTo(expectedSize));
            Assert.That(file.MeasuredSizeBytes, Is.EqualTo(expectedSize));
            Assert.That(result.Root.SizeBytes, Is.EqualTo(expectedSize));
        });
    }

    [Test]
    public async Task ScanKeepsTheLogicalLengthOfEachCloudOnlyFileForTheFolderTotals()
    {
        var first = await FileAsync("first.bin", 1);
        var second = await FileAsync("nested/second.bin", 1);
        await FileAsync("local.bin", 1);
        CloudOnly(first, 7_000);
        CloudOnly(second, 3_000);

        var result = await ScanAsync(StorageMeasurementMode.Allocated);

        var nested = Item(result.Root, Path.Combine(_root, "nested"));
        Assert.Multiple(() =>
        {
            Assert.That(result.Root.CloudTotals.CloudOnlyLogicalSizeBytes, Is.EqualTo(10_000));
            Assert.That(result.Root.CloudTotals.CloudOnlyFileCount, Is.EqualTo(2));
            Assert.That(result.Root.CloudTotals.LocalFileCount, Is.EqualTo(1));
            Assert.That(nested.CloudTotals.CloudOnlyLogicalSizeBytes, Is.EqualTo(3_000));
            Assert.That(result.Root.SizeBytes, Is.EqualTo(3 * 4096));
        });
    }

    [Test]
    public async Task LogicalModeTakesTheLengthFromTheStatusReadThatReportsAvailability()
    {
        var path = await FileAsync("file.bin", 10);
        var lengthReads = 0;
        var scanner = new DiskScanner(
            Directory.EnumerateFileSystemEntries,
            logicalSizeReader: _ =>
            {
                lengthReads++;
                return 10;
            },
            cloudAvailabilityReader: _ => new CloudAvailabilityReading(CloudAvailability.Local, 10));

        var result = await LastAsync(scanner.ScanAsync(_root));

        Assert.Multiple(() =>
        {
            Assert.That(Item(result.Root, path).SizeBytes, Is.EqualTo(10));
            Assert.That(lengthReads, Is.Zero);
        });
    }

    [Test]
    public async Task LogicalModeReadsTheLengthSeparatelyWhenTheStatusIsUnknown()
    {
        var path = await FileAsync("file.bin", 10);
        var scanner = new DiskScanner(
            Directory.EnumerateFileSystemEntries,
            logicalSizeReader: _ => 10,
            cloudAvailabilityReader: _ => CloudAvailabilityReading.Unknown);

        var result = await LastAsync(scanner.ScanAsync(_root));

        Assert.Multiple(() =>
        {
            Assert.That(Item(result.Root, path).SizeBytes, Is.EqualTo(10));
            Assert.That(Item(result.Root, path).CloudAvailability, Is.EqualTo(CloudAvailability.Unknown));
        });
    }

    [Test]
    public async Task LogicalModeKeepsTheLengthReaderForAFollowedSymbolicLink()
    {
        var target = await FileAsync("target.bin", 10);
        var link = Path.Combine(_root, "link.bin");
        File.CreateSymbolicLink(link, target);
        var scanner = new DiskScanner(
            Directory.EnumerateFileSystemEntries,
            logicalSizeReader: path => path == link ? 77 : 10,
            cloudAvailabilityReader: _ => new CloudAvailabilityReading(CloudAvailability.Local, 10));

        var result = await LastAsync(scanner.ScanAsync(
            _root,
            new ScanOptions { FollowSymbolicLinks = true }));

        Assert.That(Item(result.Root, link).SizeBytes, Is.EqualTo(77));
    }

    [Test]
    public async Task AllocatedModeReusesTheAvailabilityFromTheAllocatedMetadataRead()
    {
        var placeholder = await FileAsync("placeholder.bin", 1);
        var calls = new List<string>();
        var scanner = new DiskScanner(
            EnumerateAndRecord,
            allocatedMetadataReader: _ => Metadata(0) with
            {
                CloudAvailability = new CloudAvailabilityReading(CloudAvailability.CloudOnly, 8_000)
            },
            cloudAvailabilityReader: path =>
            {
                calls.Add(path);
                return new CloudAvailabilityReading(CloudAvailability.Local, 0);
            });

        var result = await LastAsync(scanner.ScanAsync(
            _root,
            new ScanOptions { MeasurementMode = StorageMeasurementMode.Allocated }));

        Assert.Multiple(() =>
        {
            Assert.That(Item(result.Root, placeholder).CloudAvailability, Is.EqualTo(CloudAvailability.CloudOnly));
            Assert.That(result.Root.CloudTotals.CloudOnlyLogicalSizeBytes, Is.EqualTo(8_000));
            Assert.That(calls, Does.Not.Contain(placeholder));
        });
    }

    [Test]
    public async Task AnEmptyFolderIsLocalAndDoesNotChangeItsParent()
    {
        Directory.CreateDirectory(Path.Combine(_root, "empty"));
        var placeholder = await FileAsync("placeholder.bin", 1);
        CloudOnly(placeholder, 1);

        var result = await ScanAsync(StorageMeasurementMode.Logical);

        Assert.Multiple(() =>
        {
            Assert.That(
                Item(result.Root, Path.Combine(_root, "empty")).CloudAvailability,
                Is.EqualTo(CloudAvailability.Local));
            Assert.That(result.Root.CloudAvailability, Is.EqualTo(CloudAvailability.CloudOnly));
        });
    }

    [Test]
    public async Task AMixedFolderIsPartlyLocal()
    {
        await FileAsync("mixed/local.bin", 1);
        CloudOnly(await FileAsync("mixed/placeholder.bin", 1), 1);

        var result = await ScanAsync(StorageMeasurementMode.Logical);

        Assert.That(
            Item(result.Root, Path.Combine(_root, "mixed")).CloudAvailability,
            Is.EqualTo(CloudAvailability.PartlyLocal));
    }

    [Test]
    public async Task ANestedCloudOnlyFolderMakesItsParentPartlyLocal()
    {
        await FileAsync("parent/local.bin", 1);
        CloudOnly(await FileAsync("parent/cloud/a.bin", 1), 1);
        CloudOnly(await FileAsync("parent/cloud/deeper/b.bin", 1), 1);

        var result = await ScanAsync(StorageMeasurementMode.Logical);

        var parent = Item(result.Root, Path.Combine(_root, "parent"));
        var cloud = Item(result.Root, Path.Combine(_root, "parent", "cloud"));
        Assert.Multiple(() =>
        {
            Assert.That(cloud.CloudAvailability, Is.EqualTo(CloudAvailability.CloudOnly));
            Assert.That(
                Item(result.Root, Path.Combine(_root, "parent", "cloud", "deeper")).CloudAvailability,
                Is.EqualTo(CloudAvailability.CloudOnly));
            Assert.That(parent.CloudAvailability, Is.EqualTo(CloudAvailability.PartlyLocal));
            Assert.That(result.Root.CloudAvailability, Is.EqualTo(CloudAvailability.PartlyLocal));
        });
    }

    [Test]
    public async Task AFolderWithUnknownAndLocalFilesIsUnknown()
    {
        await FileAsync("folder/local.bin", 1);
        Unknown(await FileAsync("folder/unreadable.bin", 1));

        var result = await ScanAsync(StorageMeasurementMode.Logical);

        Assert.That(
            Item(result.Root, Path.Combine(_root, "folder")).CloudAvailability,
            Is.EqualTo(CloudAvailability.Unknown));
    }

    [Test]
    public async Task AFolderWithUnknownAndCloudOnlyFilesIsPartlyLocal()
    {
        CloudOnly(await FileAsync("folder/placeholder.bin", 1), 1);
        Unknown(await FileAsync("folder/unreadable.bin", 1));

        var result = await ScanAsync(StorageMeasurementMode.Logical);

        Assert.That(
            Item(result.Root, Path.Combine(_root, "folder")).CloudAvailability,
            Is.EqualTo(CloudAvailability.PartlyLocal));
    }

    [Test]
    public async Task AFailingStatusReadIsUnknownAndNotAScanError()
    {
        var path = await FileAsync("file.bin", 1);
        var scanner = new DiskScanner(
            Directory.EnumerateFileSystemEntries,
            cloudAvailabilityReader: _ => throw new IOException("status unavailable"));

        var result = await LastAsync(scanner.ScanAsync(_root));

        Assert.Multiple(() =>
        {
            Assert.That(Item(result.Root, path).CloudAvailability, Is.EqualTo(CloudAvailability.Unknown));
            Assert.That(result.Errors, Is.Empty);
        });
    }

    [Test]
    public async Task AScannerWithoutAStatusReaderReportsUnknown()
    {
        var path = await FileAsync("file.bin", 1);

        var result = await LastAsync(new DiskScanner().ScanAsync(_root));

        Assert.Multiple(() =>
        {
            Assert.That(Item(result.Root, path).CloudAvailability, Is.EqualTo(CloudAvailability.Unknown));
            Assert.That(result.Root.CloudAvailability, Is.EqualTo(CloudAvailability.Unknown));
            Assert.That(result.UnlistedCloudFolders, Is.Empty);
        });
    }

    [Test]
    public async Task ADatalessFolderIsNotListedAndIsReportedSeparatelyFromErrors()
    {
        await FileAsync("local.bin", 1);
        var unfetched = Path.Combine(_root, "unfetched");
        await FileAsync("unfetched/never-listed.bin", 1);
        CloudOnly(unfetched, 0);

        var result = await ScanAsync(StorageMeasurementMode.Logical);

        var folder = Item(result.Root, unfetched);
        Assert.Multiple(() =>
        {
            Assert.That(_enumeratedDirectories, Does.Not.Contain(unfetched));
            Assert.That(folder.Children, Is.Empty);
            Assert.That(folder.IsUnlistedCloudFolder, Is.True);
            Assert.That(folder.CloudAvailability, Is.EqualTo(CloudAvailability.CloudOnly));
            Assert.That(result.UnlistedCloudFolders, Is.EqualTo(new[] { unfetched }));
            Assert.That(result.Errors, Is.Empty);
            Assert.That(result.FilesScanned, Is.EqualTo(1));
            Assert.That(result.Root.CloudAvailability, Is.EqualTo(CloudAvailability.PartlyLocal));
            Assert.That(result.Root.CloudTotals.UnlistedCloudFolderCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ADatalessScanRootIsNotListed()
    {
        await FileAsync("never-listed.bin", 1);
        CloudOnly(_root, 0);

        var result = await ScanAsync(StorageMeasurementMode.Logical);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsCompleted, Is.True);
            Assert.That(_enumeratedDirectories, Is.Empty);
            Assert.That(result.Root.Children, Is.Empty);
            Assert.That(result.UnlistedCloudFolders, Is.EqualTo(new[] { _root }));
        });
    }

    [Test]
    public async Task ASubtreeScanAggregatesAvailabilityLikeAFullScan()
    {
        await FileAsync("project/local.bin", 1);
        CloudOnly(await FileAsync("project/cloud/a.bin", 1), 600);
        var unfetched = Path.Combine(_root, "project", "unfetched");
        Directory.CreateDirectory(unfetched);
        CloudOnly(unfetched, 0);
        var options = new ScanOptions { MeasurementMode = StorageMeasurementMode.Logical };
        var scanner = CreateScanner();

        var full = await LastAsync(scanner.ScanAsync(_root, options));
        var subtree = await LastAsync(
            scanner.ScanSubtreeAsync(_root, Path.Combine(_root, "project"), options));

        var fullProject = Item(full.Root, Path.Combine(_root, "project"));
        Assert.Multiple(() =>
        {
            Assert.That(subtree.Root.CloudTotals, Is.EqualTo(fullProject.CloudTotals));
            Assert.That(subtree.Root.CloudAvailability, Is.EqualTo(CloudAvailability.PartlyLocal));
            Assert.That(subtree.UnlistedCloudFolders, Is.EqualTo(new[] { unfetched }));
        });
    }

    [Test]
    public async Task ACancelledScanStopsWhileReadingAvailability()
    {
        await FileAsync("a.bin", 1);
        await FileAsync("b.bin", 1);
        using var cancellation = new CancellationTokenSource();
        var scanner = new DiskScanner(
            Directory.EnumerateFileSystemEntries,
            cloudAvailabilityReader: _ =>
            {
                cancellation.Cancel();
                return new CloudAvailabilityReading(CloudAvailability.Local, 0);
            });

        var action = async () => await LastAsync(
            scanner.ScanAsync(_root, cancellationToken: cancellation.Token));

        Assert.That(action, Throws.InstanceOf<OperationCanceledException>());
    }

    private DiskScanner CreateScanner() =>
        new(
            EnumerateAndRecord,
            allocatedMetadataReader: path => Metadata(4096, fileId: (ulong)path.GetHashCode()),
            cloudAvailabilityReader: path =>
                _readings.TryGetValue(path, out var reading)
                    ? reading
                    : new CloudAvailabilityReading(CloudAvailability.Local, LengthOf(path)));

    private static long LengthOf(string path) =>
        File.Exists(path) ? new FileInfo(path).Length : 0;

    private async Task<ScanProgress> ScanAsync(StorageMeasurementMode mode) =>
        await LastAsync(CreateScanner().ScanAsync(
            _root,
            new ScanOptions { MeasurementMode = mode }));

    private IEnumerable<string> EnumerateAndRecord(string path)
    {
        _enumeratedDirectories.Add(path);
        return Directory.EnumerateFileSystemEntries(path);
    }

    private async Task<string> FileAsync(string relativePath, int length)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, new byte[length]);
        return path;
    }

    private void CloudOnly(string path, long logicalSizeBytes) =>
        _readings[path] = new CloudAvailabilityReading(CloudAvailability.CloudOnly, logicalSizeBytes);

    private void Unknown(string path) => _readings[path] = CloudAvailabilityReading.Unknown;

    private static DiskItem Item(DiskItem root, string path) =>
        ScanResultMerger.FindByPath(root, path)
        ?? throw new AssertionException($"No item at {path}.");

    private static AllocatedFileMetadata Metadata(long allocatedSizeBytes, ulong fileId = 1) =>
        new(allocatedSizeBytes, new FileIdentity(1, fileId), LinkCount: 1);

    private static async Task<ScanProgress> LastAsync(IAsyncEnumerable<ScanProgress> source)
    {
        ScanProgress? last = null;
        await foreach (var progress in source)
        {
            last = progress;
        }

        return last ?? throw new AssertionException("The scan reported no progress.");
    }
}
