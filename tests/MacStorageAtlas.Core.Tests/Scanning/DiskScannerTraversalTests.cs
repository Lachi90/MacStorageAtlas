using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Tests.Scanning;

public class DiskScannerTraversalTests
{
    private string _root = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            $"MacStorageAtlas-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
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
    public async Task ScanAsyncIncludesWhitespaceOnlyNames(StorageMeasurementMode mode)
    {
        var blankDirectory = Path.Combine(_root, " ");
        Directory.CreateDirectory(blankDirectory);
        await File.WriteAllBytesAsync(Path.Combine(blankDirectory, "inner.bin"), new byte[10]);
        var noBreakSpaceFile = Path.Combine(_root, " ");
        await File.WriteAllBytesAsync(noBreakSpaceFile, new byte[5]);
        var scanner = new DiskScanner(
            Directory.EnumerateFileSystemEntries,
            allocatedMetadataReader: LengthMetadata);

        var result = (await CollectAsync(scanner.ScanAsync(
            _root,
            new ScanOptions { MeasurementMode = mode })))[^1];

        var directory = result.Root.Children.Single(item => item.Path == blankDirectory);
        var file = result.Root.Children.Single(item => item.Path == noBreakSpaceFile);
        Assert.Multiple(() =>
        {
            Assert.That(result.IsCompleted, Is.True);
            Assert.That(result.Errors, Is.Empty);
            Assert.That(directory.Name, Is.EqualTo(" "));
            Assert.That(directory.SizeBytes, Is.EqualTo(10));
            Assert.That(directory.Children.Single().Name, Is.EqualTo("inner.bin"));
            Assert.That(file.Name, Is.EqualTo(" "));
            Assert.That(file.SizeBytes, Is.EqualTo(5));
            Assert.That(result.Root.SizeBytes, Is.EqualTo(15));
        });
    }

    [Test]
    public async Task ScanAsyncFallsBackToFullPathWhenNoNameCanBeDerived()
    {
        var subdirectory = Path.Combine(_root, "sub");
        Directory.CreateDirectory(subdirectory);
        await File.WriteAllBytesAsync(Path.Combine(subdirectory, "file.bin"), new byte[3]);
        var pathWithSeparator = subdirectory + Path.DirectorySeparatorChar;
        var scanner = new DiskScanner(path =>
            string.Equals(path, _root, StringComparison.Ordinal)
                ? [pathWithSeparator]
                : Directory.EnumerateFileSystemEntries(path));

        var result = (await CollectAsync(scanner.ScanAsync(_root)))[^1];

        var item = result.Root.Children.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.Errors, Is.Empty);
            Assert.That(item.Name, Is.EqualTo(pathWithSeparator));
            Assert.That(item.SizeBytes, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task ScanAsyncRecordsUnexpectedEntryFailureAndContinues()
    {
        var first = Path.Combine(_root, "a.bin");
        var second = Path.Combine(_root, "b.bin");
        var third = Path.Combine(_root, "c.bin");
        await File.WriteAllBytesAsync(first, new byte[1]);
        await File.WriteAllBytesAsync(second, new byte[20]);
        await File.WriteAllBytesAsync(third, new byte[300]);
        var scanner = new DiskScanner(
            path => Directory.EnumerateFileSystemEntries(path).Order(StringComparer.Ordinal),
            itemMetadataReader: (path, attributes, kind) =>
                path == second
                    ? throw new ArgumentException("Unexpected entry.")
                    : DiskItemMetadata.Unknown(kind));

        var result = (await CollectAsync(scanner.ScanAsync(_root)))[^1];

        Assert.Multiple(() =>
        {
            Assert.That(result.IsCompleted, Is.True);
            Assert.That(result.Root.SizeBytes, Is.EqualTo(301));
            Assert.That(result.FilesScanned, Is.EqualTo(2));
            Assert.That(
                result.Root.Children.Select(item => item.Path),
                Is.EquivalentTo(new[] { first, third }));
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0].Path, Is.EqualTo(second));
            Assert.That(result.Errors[0].ExceptionType, Is.EqualTo(nameof(ArgumentException)));
        });
    }

    [Test]
    public async Task ScanAsyncRecordsUnexpectedMeasurementFailureWithoutCountingTheEntry()
    {
        var kept = Path.Combine(_root, "kept.bin");
        var failing = Path.Combine(_root, "failing.bin");
        await File.WriteAllBytesAsync(kept, new byte[8]);
        await File.WriteAllBytesAsync(failing, new byte[16]);
        var scanner = new DiskScanner(
            Directory.EnumerateFileSystemEntries,
            allocatedMetadataReader: path =>
                path == failing
                    ? throw new InvalidOperationException("Unexpected metadata.")
                    : LengthMetadata(path));

        var result = (await CollectAsync(scanner.ScanAsync(
            _root,
            new ScanOptions { MeasurementMode = StorageMeasurementMode.SharedAwareAllocated })))[^1];

        Assert.Multiple(() =>
        {
            Assert.That(result.IsCompleted, Is.True);
            Assert.That(result.Root.SizeBytes, Is.EqualTo(8));
            Assert.That(result.BytesScanned, Is.EqualTo(8));
            Assert.That(result.Root.Children.Single().Path, Is.EqualTo(kept));
            Assert.That(
                result.Errors.Single().ExceptionType,
                Is.EqualTo(nameof(InvalidOperationException)));
        });
    }

    [Test]
    public async Task ScanAsyncDoesNotRecordCancellationAsEntryFailure()
    {
        await File.WriteAllBytesAsync(Path.Combine(_root, "a.bin"), new byte[1]);
        using var cancellation = new CancellationTokenSource();
        var published = new List<ScanProgress>();
        var scanner = new DiskScanner(
            Directory.EnumerateFileSystemEntries,
            itemMetadataReader: (_, _, _) =>
            {
                cancellation.Cancel();
                throw new OperationCanceledException(cancellation.Token);
            });

        var action = async () =>
        {
            await foreach (var item in scanner.ScanAsync(
                               _root,
                               cancellationToken: cancellation.Token))
            {
                published.Add(item);
            }
        };

        Assert.That(action, Throws.InstanceOf<OperationCanceledException>());
        Assert.Multiple(() =>
        {
            Assert.That(published, Has.None.Matches<ScanProgress>(item => item.IsCompleted));
            Assert.That(published.SelectMany(item => item.Errors), Is.Empty);
        });
    }

    private static AllocatedFileMetadata LengthMetadata(string path) =>
        new(
            new FileInfo(path).Length,
            new FileIdentity(1, (ulong)path.GetHashCode() & 0x7FFFFFFF),
            LinkCount: 1);

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
}
