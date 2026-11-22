using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Tests.Scanning;

public class DiskScannerSubtreeTests
{
    private string _root = null!;
    private string _project = null!;
    private string _package = null!;

    [SetUp]
    public async Task SetUp()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            $"MacStorageAtlas-{Guid.NewGuid():N}");
        _project = Path.Combine(_root, "project");
        _package = Path.Combine(_project, "Tool.app");

        await WriteFileAsync(Path.Combine(_project, "a.bin"), 100);
        await WriteFileAsync(Path.Combine(_project, ".hidden"), 10);
        await WriteFileAsync(Path.Combine(_project, ".cache", "x.bin"), 20);
        await WriteFileAsync(Path.Combine(_package, "Contents", "tool"), 30);
        await WriteFileAsync(Path.Combine(_project, "nested", "b.bin"), 40);
        await WriteFileAsync(Path.Combine(_root, "other", "c.bin"), 50);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [TestCase(StorageMeasurementMode.Logical, false, true)]
    [TestCase(StorageMeasurementMode.Logical, true, true)]
    [TestCase(StorageMeasurementMode.Logical, false, false)]
    [TestCase(StorageMeasurementMode.Logical, true, false)]
    [TestCase(StorageMeasurementMode.Allocated, false, true)]
    [TestCase(StorageMeasurementMode.Allocated, true, false)]
    public async Task ScanSubtreeAsyncEqualsTheSameFolderOfAFullScan(
        StorageMeasurementMode mode,
        bool includeHiddenFiles,
        bool treatPackagesAsDirectories)
    {
        var options = new ScanOptions
        {
            MeasurementMode = mode,
            IncludeHiddenFiles = includeHiddenFiles,
            TreatPackagesAsDirectories = treatPackagesAsDirectories
        };
        var scanner = CreateScanner();

        var full = (await CollectAsync(scanner.ScanAsync(_root, options)))[^1];
        var subtree = (await CollectAsync(
            scanner.ScanSubtreeAsync(_root, _project, options)))[^1];

        Assert.Multiple(() =>
        {
            Assert.That(subtree.IsCompleted, Is.True);
            Assert.That(subtree.IsRootPresent, Is.True);
            Assert.That(subtree.Errors, Is.Empty);
            Assert.That(subtree.MeasurementMode, Is.EqualTo(mode));
            Assert.That(
                Describe(subtree.Root),
                Is.EqualTo(Describe(FindItem(full.Root, _project)!)));
        });
    }

    [Test]
    public async Task ScanSubtreeAsyncOfAnUnexpandedPackageMeasuresItWithoutChildren()
    {
        var options = new ScanOptions { TreatPackagesAsDirectories = false };
        var scanner = CreateScanner();

        var full = (await CollectAsync(scanner.ScanAsync(_root, options)))[^1];
        var subtree = (await CollectAsync(
            scanner.ScanSubtreeAsync(_root, _package, options)))[^1];

        Assert.Multiple(() =>
        {
            Assert.That(subtree.Root.Children, Is.Empty);
            Assert.That(subtree.Root.SizeBytes, Is.EqualTo(30));
            Assert.That(subtree.Root.Metadata.Kind, Is.EqualTo(DiskItemKind.ApplicationBundle));
            Assert.That(
                Describe(subtree.Root),
                Is.EqualTo(Describe(FindItem(full.Root, _package)!)));
        });
    }

    [Test]
    public async Task ScanSubtreeAsyncKeepsAnOmittedAlternateInsideTheFolderOmitted()
    {
        var users = Path.Combine(_root, "Users");
        var system = Path.Combine(_root, "System");
        var dataUsers = Path.Combine(system, "Volumes", "Data", "Users");
        await WriteFileAsync(Path.Combine(users, "home", "a.bin"), 300);
        await WriteFileAsync(Path.Combine(dataUsers, "home", "a.bin"), 300);
        await WriteFileAsync(Path.Combine(system, "Volumes", "Data", "extra.bin"), 7);
        var omission = new EquivalentPath(users, dataUsers);
        var scanner = CreateScanner(() => [omission]);

        var full = (await CollectAsync(scanner.ScanAsync(_root)))[^1];
        var subtree = (await CollectAsync(scanner.ScanSubtreeAsync(_root, system)))[^1];

        Assert.Multiple(() =>
        {
            Assert.That(full.OmittedEquivalentPaths, Is.EqualTo(new[] { omission }));
            Assert.That(subtree.OmittedEquivalentPaths, Is.EqualTo(new[] { omission }));
            Assert.That(FindItem(subtree.Root, dataUsers), Is.Null);
            Assert.That(subtree.Root.SizeBytes, Is.EqualTo(7));
            Assert.That(
                Describe(subtree.Root),
                Is.EqualTo(Describe(FindItem(full.Root, system)!)));
        });
    }

    [Test]
    public async Task ScanSubtreeAsyncOfTheScanRootIsAFullScan()
    {
        var scanner = CreateScanner();

        var full = (await CollectAsync(scanner.ScanAsync(_root)))[^1];
        var subtree = (await CollectAsync(scanner.ScanSubtreeAsync(_root, _root)))[^1];

        Assert.That(Describe(subtree.Root), Is.EqualTo(Describe(full.Root)));
    }

    [Test]
    public void ScanSubtreeAsyncHonorsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var scanner = CreateScanner();

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await CollectAsync(scanner.ScanSubtreeAsync(
                _root,
                _project,
                cancellationToken: cancellation.Token)));
    }

    [Test]
    public void ScanSubtreeAsyncStopsWhenCancelledMidway()
    {
        using var cancellation = new CancellationTokenSource();
        var nested = Path.Combine(_project, "nested");
        var scanner = new DiskScanner(path =>
        {
            if (string.Equals(path, nested, StringComparison.Ordinal))
            {
                cancellation.Cancel();
            }

            return Directory.EnumerateFileSystemEntries(path);
        });
        var received = new List<ScanProgress>();

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var progress in scanner.ScanSubtreeAsync(
                               _root,
                               _project,
                               cancellationToken: cancellation.Token))
            {
                received.Add(progress);
            }
        });

        Assert.That(received.Any(progress => progress.IsCompleted), Is.False);
    }

    [Test]
    public async Task ScanSubtreeAsyncRecordsARecoverableErrorInsideTheFolder()
    {
        var nested = Path.Combine(_project, "nested");
        var scanner = new DiskScanner(path =>
            string.Equals(path, nested, StringComparison.Ordinal)
                ? throw new UnauthorizedAccessException("Denied.")
                : Directory.EnumerateFileSystemEntries(path));

        var result = (await CollectAsync(scanner.ScanSubtreeAsync(_root, _project)))[^1];

        Assert.Multiple(() =>
        {
            Assert.That(result.IsCompleted, Is.True);
            Assert.That(result.IsRootPresent, Is.True);
            Assert.That(result.Errors.Select(error => error.Path), Is.EqualTo(new[] { nested }));
            Assert.That(
                result.Errors[0].ExceptionType,
                Is.EqualTo(nameof(UnauthorizedAccessException)));
            Assert.That(result.Root.SizeBytes, Is.EqualTo(130));
        });
    }

    [Test]
    public async Task ScanSubtreeAsyncReportsAFolderThatNoLongerExistsAsAbsent()
    {
        Directory.Delete(_project, recursive: true);
        var scanner = CreateScanner();

        var progress = await CollectAsync(scanner.ScanSubtreeAsync(_root, _project));

        Assert.Multiple(() =>
        {
            Assert.That(progress, Has.Count.EqualTo(1));
            Assert.That(progress[0].IsCompleted, Is.True);
            Assert.That(progress[0].IsRootPresent, Is.False);
            Assert.That(progress[0].Errors, Is.Empty);
            Assert.That(progress[0].Root.Path, Is.EqualTo(_project));
        });
    }

    [Test]
    public async Task ScanSubtreeAsyncReportsAFolderReplacedByAFileAsAbsent()
    {
        Directory.Delete(_project, recursive: true);
        await File.WriteAllBytesAsync(_project, new byte[3]);
        var scanner = CreateScanner();

        var result = (await CollectAsync(scanner.ScanSubtreeAsync(_root, _project)))[^1];

        Assert.That(result.IsRootPresent, Is.False);
    }

    [Test]
    public void ScanSubtreeAsyncRejectsAFolderOutsideTheScanRoot()
    {
        var scanner = CreateScanner();

        Assert.Throws<ArgumentException>(() =>
            scanner.ScanSubtreeAsync(_project, Path.Combine(_root, "other")));
    }

    [Test]
    public void ScanSubtreeAsyncRejectsAFolderInsideAnUnexpandedPackage()
    {
        var scanner = CreateScanner();

        Assert.Throws<ArgumentException>(() => scanner.ScanSubtreeAsync(
            _root,
            Path.Combine(_package, "Contents"),
            new ScanOptions { TreatPackagesAsDirectories = false }));
    }

    [Test]
    public void ScanSubtreeAsyncRejectsAFolderInsideAnExcludedHiddenFolder()
    {
        var scanner = CreateScanner();

        Assert.Throws<ArgumentException>(() => scanner.ScanSubtreeAsync(
            _root,
            Path.Combine(_project, ".cache", "inner"),
            new ScanOptions { IncludeHiddenFiles = false }));
    }

    [Test]
    public void ScanSubtreeAsyncRejectsAFolderInsideAnOmittedAlternate()
    {
        var users = Path.Combine(_root, "Users");
        var dataUsers = Path.Combine(_root, "System", "Volumes", "Data", "Users");
        Directory.CreateDirectory(users);
        Directory.CreateDirectory(Path.Combine(dataUsers, "home"));
        var scanner = CreateScanner(() => [new EquivalentPath(users, dataUsers)]);

        Assert.Throws<ArgumentException>(() =>
            scanner.ScanSubtreeAsync(_root, Path.Combine(dataUsers, "home")));
    }

    [Test]
    public void ScanSubtreeAsyncRejectsSharedAwareAllocatedMode()
    {
        var scanner = CreateScanner();

        Assert.Throws<NotSupportedException>(() => scanner.ScanSubtreeAsync(
            _root,
            _project,
            new ScanOptions { MeasurementMode = StorageMeasurementMode.SharedAwareAllocated }));
    }

    [Test]
    public void ScanSubtreeAsyncRejectsFollowedSymbolicLinks()
    {
        var scanner = CreateScanner();

        Assert.Throws<NotSupportedException>(() => scanner.ScanSubtreeAsync(
            _root,
            _project,
            new ScanOptions { FollowSymbolicLinks = true }));
    }

    private static DiskScanner CreateScanner(
        Func<IReadOnlyList<EquivalentPath>>? equivalentPaths = null) =>
        new(
            Directory.EnumerateFileSystemEntries,
            allocatedMetadataReader: AllocatedMetadata,
            equivalentPathReader: equivalentPaths);

    private static AllocatedFileMetadata AllocatedMetadata(string path) =>
        new(
            new FileInfo(path).Length + 4_096,
            new FileIdentity(1, (ulong)path.GetHashCode() & 0x7FFFFFFF),
            LinkCount: 1);

    private static IReadOnlyList<string> Describe(DiskItem item)
    {
        var lines = new List<string>();
        Describe(item, lines);
        return lines;
    }

    private static void Describe(DiskItem item, List<string> lines)
    {
        lines.Add(
            $"{item.Path}|{item.IsDirectory}|{item.Metadata.Kind}|"
            + $"{item.SizeBytes}|{item.MeasuredSizeBytes}|{item.SharedSizeBytes}");
        foreach (var child in item.Children.OrderBy(child => child.Path, StringComparer.Ordinal))
        {
            Describe(child, lines);
        }
    }

    private static DiskItem? FindItem(DiskItem root, string path)
    {
        if (string.Equals(root.Path, path, StringComparison.Ordinal))
        {
            return root;
        }

        return root.Children
            .Select(child => FindItem(child, path))
            .FirstOrDefault(found => found is not null);
    }

    private static async Task WriteFileAsync(string path, int length)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, new byte[length]);
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
}
