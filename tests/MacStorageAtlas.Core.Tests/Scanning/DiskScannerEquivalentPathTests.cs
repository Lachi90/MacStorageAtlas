using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Tests.Scanning;

public class DiskScannerEquivalentPathTests
{
    private const int HomeFileBytes = 300;
    private const int CacheFileBytes = 50;
    private const int ExtraFileBytes = 7;

    private string _root = null!;
    private string _users = null!;
    private string _dataVolume = null!;
    private string _dataUsers = null!;
    private string _systemCaches = null!;
    private string _dataSystemCaches = null!;
    private string _dataExtra = null!;

    [SetUp]
    public async Task SetUp()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            $"MacStorageAtlas-{Guid.NewGuid():N}");
        _users = Path.Combine(_root, "Users");
        _dataVolume = Path.Combine(_root, "System", "Volumes", "Data");
        _dataUsers = Path.Combine(_dataVolume, "Users");
        _systemCaches = Path.Combine(_root, "System", "Library", "Caches");
        _dataSystemCaches = Path.Combine(_dataVolume, "System", "Library", "Caches");
        _dataExtra = Path.Combine(_dataVolume, "extra");

        await WriteFileAsync(Path.Combine(_users, "home", "a.bin"), HomeFileBytes);
        await WriteFileAsync(Path.Combine(_dataUsers, "home", "a.bin"), HomeFileBytes);
        await WriteFileAsync(Path.Combine(_systemCaches, "c.bin"), CacheFileBytes);
        await WriteFileAsync(Path.Combine(_dataSystemCaches, "c.bin"), CacheFileBytes);
        await WriteFileAsync(Path.Combine(_dataExtra, "b.bin"), ExtraFileBytes);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Test]
    public async Task ScanAsyncWithoutProviderOmitsNothing()
    {
        var scanner = new DiskScanner(Directory.EnumerateFileSystemEntries);

        var progress = await CollectAsync(scanner.ScanAsync(_root));

        Assert.Multiple(() =>
        {
            Assert.That(
                progress.Select(item => item.OmittedEquivalentPaths),
                Is.All.Empty);
            Assert.That(FindItem(progress[^1].Root, _dataUsers), Is.Not.Null);
            Assert.That(
                progress[^1].Root.SizeBytes,
                Is.EqualTo((2 * HomeFileBytes) + (2 * CacheFileBytes) + ExtraFileBytes));
        });
    }

    [Test]
    public async Task ScanAsyncOfWholeRootOmitsEveryIncludedAlternate()
    {
        var scanner = CreateScanner(StandardPairs);

        var result = (await CollectAsync(scanner.ScanAsync(_root)))[^1];

        Assert.That(
            result.OmittedEquivalentPaths.Select(path => path.AlternatePath),
            Is.EquivalentTo(new[] { _dataUsers, _dataSystemCaches }));
    }

    [Test]
    public async Task ScanAsyncOfSystemOmitsOnlyAlternatesWhoseVisiblePathIsInScope()
    {
        var scanner = CreateScanner(StandardPairs);

        var result = (await CollectAsync(
            scanner.ScanAsync(Path.Combine(_root, "System"))))[^1];

        Assert.Multiple(() =>
        {
            Assert.That(
                result.OmittedEquivalentPaths.Select(path => path.AlternatePath),
                Is.EqualTo(new[] { _dataSystemCaches }));
            Assert.That(FindItem(result.Root, _dataUsers), Is.Not.Null);
            Assert.That(FindItem(result.Root, _dataSystemCaches), Is.Null);
        });
    }

    [TestCase("Users")]
    [TestCase("Users/home")]
    [TestCase("System/Volumes/Data")]
    public async Task ScanAsyncOfOneSideOfAnEquivalenceOmitsNothing(string relativeRoot)
    {
        var scanRoot = Path.Combine(_root, relativeRoot);
        var withProvider = CreateScanner(StandardPairs);
        var withoutProvider = CreateScanner(() => []);

        var result = (await CollectAsync(withProvider.ScanAsync(scanRoot)))[^1];
        var baseline = (await CollectAsync(withoutProvider.ScanAsync(scanRoot)))[^1];

        Assert.Multiple(() =>
        {
            Assert.That(result.OmittedEquivalentPaths, Is.Empty);
            Assert.That(result.FilesScanned, Is.EqualTo(baseline.FilesScanned));
            Assert.That(result.DirectoriesScanned, Is.EqualTo(baseline.DirectoriesScanned));
            Assert.That(result.Root.SizeBytes, Is.EqualTo(baseline.Root.SizeBytes));
        });
    }

    [Test]
    public async Task ScanAsyncDoesNotTraverseOrReportOmittedAlternates()
    {
        var enumerated = new List<string>();
        var scanner = new DiskScanner(
            path =>
            {
                enumerated.Add(path);
                return Directory.EnumerateFileSystemEntries(path);
            },
            equivalentPathReader: StandardPairs);

        var result = (await CollectAsync(scanner.ScanAsync(_root)))[^1];

        Assert.Multiple(() =>
        {
            Assert.That(FindItem(result.Root, _dataUsers), Is.Null);
            Assert.That(FindItem(result.Root, _dataSystemCaches), Is.Null);
            Assert.That(enumerated, Has.None.StartsWith(_dataUsers));
            Assert.That(enumerated, Has.None.StartsWith(_dataSystemCaches));
            Assert.That(result.Errors, Is.Empty);
            Assert.That(FindItem(result.Root, Path.Combine(_users, "home", "a.bin")), Is.Not.Null);
        });
    }

    [Test]
    public async Task ScanAsyncKeepsDataVolumeContentWithoutEquivalent()
    {
        var scanner = CreateScanner(StandardPairs);

        var result = (await CollectAsync(scanner.ScanAsync(_root)))[^1];

        var extra = FindItem(result.Root, _dataExtra);
        var dataVolume = FindItem(result.Root, _dataVolume);
        Assert.Multiple(() =>
        {
            Assert.That(extra, Is.Not.Null);
            Assert.That(extra!.SizeBytes, Is.EqualTo(ExtraFileBytes));
            Assert.That(dataVolume!.SizeBytes, Is.EqualTo(ExtraFileBytes));
        });
    }

    [Test]
    public async Task ScanAsyncCountsEquivalentContentOnceInLogicalMode()
    {
        var scanner = CreateScanner(StandardPairs);

        var result = (await CollectAsync(scanner.ScanAsync(_root)))[^1];

        Assert.Multiple(() =>
        {
            Assert.That(result.MeasurementMode, Is.EqualTo(StorageMeasurementMode.Logical));
            Assert.That(
                result.Root.SizeBytes,
                Is.EqualTo(HomeFileBytes + CacheFileBytes + ExtraFileBytes));
            Assert.That(result.BytesScanned, Is.EqualTo(result.Root.SizeBytes));
        });
    }

    [Test]
    public async Task ScanAsyncCountsEquivalentContentOnceInAllocatedMode()
    {
        var scanner = CreateScanner(StandardPairs, SharedIdentityMetadata);

        var result = (await CollectAsync(scanner.ScanAsync(
            _root,
            new ScanOptions { MeasurementMode = StorageMeasurementMode.Allocated })))[^1];

        Assert.Multiple(() =>
        {
            Assert.That(
                result.Root.SizeBytes,
                Is.EqualTo(HomeFileBytes + CacheFileBytes + ExtraFileBytes));
            Assert.That(result.Root.MeasuredSizeBytes, Is.EqualTo(result.Root.SizeBytes));
            Assert.That(result.Root.SharedSizeBytes, Is.Zero);
        });
    }

    [Test]
    public async Task ScanAsyncAttributesSharedAwareStorageToVisiblePathWhenAlternateIsEnumeratedFirst()
    {
        var options = new ScanOptions
        {
            MeasurementMode = StorageMeasurementMode.SharedAwareAllocated
        };
        var withProvider = CreateScanner(
            StandardPairs,
            SharedIdentityMetadata,
            AlternateFirstEnumeration);
        var withoutProvider = CreateScanner(
            () => [],
            SharedIdentityMetadata,
            AlternateFirstEnumeration);

        var result = (await CollectAsync(withProvider.ScanAsync(_root, options)))[^1];
        var baseline = (await CollectAsync(withoutProvider.ScanAsync(_root, options)))[^1];

        var home = FindItem(result.Root, Path.Combine(_users, "home"))!;
        var baselineHome = FindItem(baseline.Root, Path.Combine(_users, "home"))!;
        Assert.Multiple(() =>
        {
            Assert.That(baselineHome.SizeBytes, Is.Zero);
            Assert.That(baselineHome.SharedSizeBytes, Is.EqualTo(HomeFileBytes));
            Assert.That(home.SizeBytes, Is.EqualTo(HomeFileBytes));
            Assert.That(home.SharedSizeBytes, Is.Zero);
            Assert.That(result.Root.SizeBytes, Is.EqualTo(baseline.Root.SizeBytes));
            Assert.That(result.Root.SharedSizeBytes, Is.Zero);
        });
    }

    [Test]
    public void ScanAsyncCancellationAfterOmissionKeepsAlternateBytesOut()
    {
        using var cancellation = new CancellationTokenSource();
        var published = new List<ScanProgress>();
        var scanner = CreateScanner(
            StandardPairs,
            enumerate: path =>
            {
                if (string.Equals(path, _dataExtra, StringComparison.OrdinalIgnoreCase))
                {
                    cancellation.Cancel();
                }

                return AlternateFirstEnumeration(path);
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
        var latest = published[^1];
        Assert.Multiple(() =>
        {
            Assert.That(latest.IsCompleted, Is.False);
            Assert.That(FindItem(latest.Root, _dataUsers), Is.Null);
            Assert.That(FindItem(latest.Root, _users), Is.Null);
            Assert.That(latest.Root.SizeBytes, Is.LessThanOrEqualTo(CacheFileBytes));
            Assert.That(
                latest.OmittedEquivalentPaths.Select(path => path.AlternatePath),
                Is.SubsetOf(new[] { _dataUsers, _dataSystemCaches }));
        });
    }

    [Test]
    public async Task ScanAsyncWithEmptyProviderTraversesBothPaths()
    {
        var scanner = CreateScanner(() => []);

        var result = (await CollectAsync(scanner.ScanAsync(_root)))[^1];

        AssertUnchangedTraversal(result);
    }

    [Test]
    public async Task ScanAsyncWithThrowingProviderTraversesBothPathsWithoutErrors()
    {
        var scanner = CreateScanner(
            () => throw new IOException("Firmlink definition is unreadable."));

        var result = (await CollectAsync(scanner.ScanAsync(_root)))[^1];

        AssertUnchangedTraversal(result);
    }

    [Test]
    public async Task ScanAsyncIgnoresPairsOutsideScopeOrInvalid()
    {
        var outside = Path.Combine(Path.GetTempPath(), $"Outside-{Guid.NewGuid():N}");
        var scanner = CreateScanner(() =>
        [
            new EquivalentPath(outside, _dataUsers),
            new EquivalentPath(_users, outside),
            new EquivalentPath("Users", _dataUsers),
            new EquivalentPath(_users, ""),
            new EquivalentPath(_dataUsers, Path.Combine(_dataUsers, "home")),
            new EquivalentPath(_users, _root)
        ]);

        var result = (await CollectAsync(scanner.ScanAsync(_root)))[^1];

        AssertUnchangedTraversal(result);
    }

    private void AssertUnchangedTraversal(ScanProgress result)
    {
        Assert.Multiple(() =>
        {
            Assert.That(result.IsCompleted, Is.True);
            Assert.That(result.OmittedEquivalentPaths, Is.Empty);
            Assert.That(result.Errors, Is.Empty);
            Assert.That(FindItem(result.Root, _dataUsers), Is.Not.Null);
            Assert.That(FindItem(result.Root, _users), Is.Not.Null);
            Assert.That(
                result.Root.SizeBytes,
                Is.EqualTo((2 * HomeFileBytes) + (2 * CacheFileBytes) + ExtraFileBytes));
        });
    }

    private IReadOnlyList<EquivalentPath> StandardPairs() =>
    [
        new EquivalentPath(_users, _dataUsers),
        new EquivalentPath(_systemCaches, _dataSystemCaches)
    ];

    private DiskScanner CreateScanner(
        Func<IReadOnlyList<EquivalentPath>> equivalentPaths,
        Func<string, AllocatedFileMetadata>? allocatedMetadataReader = null,
        Func<string, IEnumerable<string>>? enumerate = null) =>
        new(
            enumerate ?? Directory.EnumerateFileSystemEntries,
            allocatedMetadataReader: allocatedMetadataReader,
            equivalentPathReader: equivalentPaths);

    private IEnumerable<string> AlternateFirstEnumeration(string path) =>
        Directory.EnumerateFileSystemEntries(path)
            .OrderBy(entry => IsUnder(entry, Path.Combine(_root, "System")) ? 0 : 1)
            .ThenBy(entry => IsUnder(entry, Path.Combine(_root, "System", "Volumes")) ? 0 : 1)
            .ThenBy(entry => IsUnder(entry, _dataUsers) ? 0 : 1)
            .ThenBy(entry => entry, StringComparer.Ordinal);

    private AllocatedFileMetadata SharedIdentityMetadata(string path)
    {
        var key = IsUnder(path, _dataVolume)
            ? Path.GetRelativePath(_dataVolume, path)
            : Path.GetRelativePath(_root, path);
        var fileId = (ulong)StringComparer.Ordinal.GetHashCode(key) & 0x7FFFFFFF;
        return new AllocatedFileMetadata(
            new FileInfo(path).Length,
            new FileIdentity(1, fileId),
            LinkCount: 1);
    }

    private static bool IsUnder(string path, string ancestor) =>
        string.Equals(path, ancestor, StringComparison.Ordinal)
        || path.StartsWith(ancestor + Path.DirectorySeparatorChar, StringComparison.Ordinal);

    private static DiskItem? FindItem(DiskItem item, string path)
    {
        if (string.Equals(item.Path, path, StringComparison.Ordinal))
        {
            return item;
        }

        foreach (var child in item.Children)
        {
            var match = FindItem(child, path);
            if (match is not null)
            {
                return match;
            }
        }

        return null;
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
