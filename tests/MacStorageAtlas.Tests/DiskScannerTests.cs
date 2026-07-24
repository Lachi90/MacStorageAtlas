using System.IO;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.Tests;

public class DiskScannerTests
{
    private string _temporaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"MacStorageAtlas-{Guid.NewGuid():N}");
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
    public async Task ScanAsyncBuildsRecursiveTreeAndAggregatesDirectorySizes()
    {
        // Arrange
        var nestedDirectory = Directory.CreateDirectory(
            Path.Combine(_temporaryDirectory, "first", "second"));
        await File.WriteAllBytesAsync(
            Path.Combine(_temporaryDirectory, "root.bin"),
            new byte[7]);
        await File.WriteAllBytesAsync(
            Path.Combine(nestedDirectory.FullName, "nested.bin"),
            new byte[11]);
        var scanner = new DiskScanner();

        // Act
        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory));

        // Assert
        var result = progress[^1];
        var first = result.Root.Children.Single(item => item.Name == "first");
        var second = first.Children.Single(item => item.Name == "second");
        Assert.Multiple(() =>
        {
            Assert.That(result.IsCompleted, Is.True);
            Assert.That(result.FilesScanned, Is.EqualTo(2));
            Assert.That(result.DirectoriesScanned, Is.EqualTo(3));
            Assert.That(result.BytesScanned, Is.EqualTo(18));
            Assert.That(result.Root.SizeBytes, Is.EqualTo(18));
            Assert.That(first.SizeBytes, Is.EqualTo(11));
            Assert.That(second.SizeBytes, Is.EqualTo(11));
        });
    }

    [Test]
    public async Task ScanAsyncSortsCompletedTreeBySizeDescendingRecursively()
    {
        // Arrange
        var nestedDirectory = Directory.CreateDirectory(
            Path.Combine(_temporaryDirectory, "nested"));
        await File.WriteAllBytesAsync(
            Path.Combine(_temporaryDirectory, "small.bin"),
            new byte[2]);
        await File.WriteAllBytesAsync(
            Path.Combine(_temporaryDirectory, "large.bin"),
            new byte[20]);
        await File.WriteAllBytesAsync(
            Path.Combine(nestedDirectory.FullName, "nested-small.bin"),
            new byte[3]);
        await File.WriteAllBytesAsync(
            Path.Combine(nestedDirectory.FullName, "nested-large.bin"),
            new byte[8]);
        var scanner = new DiskScanner();

        // Act
        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory));

        // Assert
        var root = progress[^1].Root;
        var nested = root.Children.Single(item => item.Name == "nested");
        Assert.Multiple(() =>
        {
            Assert.That(root.Children.Select(item => item.SizeBytes),
                Is.EqualTo(new long[] { 20, 11, 2 }));
            Assert.That(nested.Children.Select(item => item.SizeBytes),
                Is.EqualTo(new long[] { 8, 3 }));
        });
    }

    [Test]
    public async Task ScanAsyncStreamsProgressBeforeCompletion()
    {
        // Arrange
        await File.WriteAllBytesAsync(
            Path.Combine(_temporaryDirectory, "file.bin"),
            new byte[5]);
        var scanner = new DiskScanner();

        // Act
        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(progress, Has.Count.GreaterThanOrEqualTo(3));
            Assert.That(progress[0].IsCompleted, Is.False);
            Assert.That(progress.Any(item => item.FilesScanned == 1 && !item.IsCompleted), Is.True);
            Assert.That(progress[^1].IsCompleted, Is.True);
        });
    }

    [Test]
    public async Task ScanAsyncRespectsHiddenFileOption()
    {
        // Arrange
        await File.WriteAllBytesAsync(
            Path.Combine(_temporaryDirectory, ".hidden"),
            new byte[3]);
        await File.WriteAllBytesAsync(
            Path.Combine(_temporaryDirectory, "visible"),
            new byte[5]);
        var scanner = new DiskScanner();

        // Act
        var defaultProgress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory));
        var includedProgress = await CollectAsync(scanner.ScanAsync(
            _temporaryDirectory,
            new ScanOptions { IncludeHiddenFiles = true }));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(defaultProgress[^1].FilesScanned, Is.EqualTo(1));
            Assert.That(defaultProgress[^1].Root.SizeBytes, Is.EqualTo(5));
            Assert.That(
                defaultProgress[^1].Root.Children.Any(item => item.Name == ".hidden"),
                Is.False);
            Assert.That(includedProgress[^1].FilesScanned, Is.EqualTo(2));
            Assert.That(includedProgress[^1].Root.SizeBytes, Is.EqualTo(8));
            Assert.That(
                includedProgress[^1].Root.Children.Any(item => item.Name == ".hidden"),
                Is.True);
        });
    }

    [Test]
    public async Task ScanAsyncExcludesHiddenFoldersByDefault()
    {
        // Arrange
        var hiddenDirectory = Directory.CreateDirectory(
            Path.Combine(_temporaryDirectory, ".hidden-folder"));
        await File.WriteAllBytesAsync(
            Path.Combine(hiddenDirectory.FullName, "buried.bin"),
            new byte[6]);
        await File.WriteAllBytesAsync(
            Path.Combine(_temporaryDirectory, "visible.bin"),
            new byte[5]);
        var scanner = new DiskScanner();

        // Act
        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory));

        // Assert
        var result = progress[^1];
        Assert.Multiple(() =>
        {
            Assert.That(result.FilesScanned, Is.EqualTo(1));
            Assert.That(result.Root.SizeBytes, Is.EqualTo(5));
            Assert.That(
                result.Root.Children.Any(item => item.Name == ".hidden-folder"),
                Is.False);
        });
    }

    [Test]
    public async Task ScanAsyncIncludesHiddenFoldersWhenOptionIsEnabled()
    {
        // Arrange
        var hiddenDirectory = Directory.CreateDirectory(
            Path.Combine(_temporaryDirectory, ".hidden-folder"));
        await File.WriteAllBytesAsync(
            Path.Combine(hiddenDirectory.FullName, "buried.bin"),
            new byte[6]);
        await File.WriteAllBytesAsync(
            Path.Combine(_temporaryDirectory, "visible.bin"),
            new byte[5]);
        var scanner = new DiskScanner();

        // Act
        var progress = await CollectAsync(scanner.ScanAsync(
            _temporaryDirectory,
            new ScanOptions { IncludeHiddenFiles = true }));

        // Assert
        var result = progress[^1];
        var hidden = result.Root.Children.Single(item => item.Name == ".hidden-folder");
        Assert.Multiple(() =>
        {
            Assert.That(result.FilesScanned, Is.EqualTo(2));
            Assert.That(result.Root.SizeBytes, Is.EqualTo(11));
            Assert.That(hidden.SizeBytes, Is.EqualTo(6));
            Assert.That(hidden.Children.Single().Name, Is.EqualTo("buried.bin"));
        });
    }

    [Test]
    public async Task ScanAsyncTreatsPackageAsSingleItemWhenPackageExpansionIsDisabled()
    {
        // Arrange
        var package = Directory.CreateDirectory(
            Path.Combine(_temporaryDirectory, "Example.app", "Contents"));
        await File.WriteAllBytesAsync(
            Path.Combine(package.FullName, "payload"),
            new byte[13]);
        var scanner = new DiskScanner();
        var options = new ScanOptions { TreatPackagesAsDirectories = false };

        // Act
        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory, options));

        // Assert
        var packageItem = progress[^1].Root.Children.Single();
        Assert.Multiple(() =>
        {
            Assert.That(packageItem.Name, Is.EqualTo("Example.app"));
            Assert.That(packageItem.SizeBytes, Is.EqualTo(13));
            Assert.That(packageItem.Children, Is.Empty);
            Assert.That(progress[^1].FilesScanned, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ScanAsyncExpandsPackageContentsWhenPackageExpansionIsEnabled()
    {
        // Arrange
        var package = Directory.CreateDirectory(
            Path.Combine(_temporaryDirectory, "Example.app", "Contents"));
        await File.WriteAllBytesAsync(
            Path.Combine(package.FullName, "payload"),
            new byte[13]);
        var scanner = new DiskScanner();
        var options = new ScanOptions { TreatPackagesAsDirectories = true };

        // Act
        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory, options));

        // Assert
        var packageItem = progress[^1].Root.Children.Single();
        var contents = packageItem.Children.Single();
        Assert.Multiple(() =>
        {
            Assert.That(packageItem.Name, Is.EqualTo("Example.app"));
            Assert.That(packageItem.SizeBytes, Is.EqualTo(13));
            Assert.That(contents.Name, Is.EqualTo("Contents"));
            Assert.That(contents.Children.Single().Name, Is.EqualTo("payload"));
            Assert.That(progress[^1].FilesScanned, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ScanAsyncExpandsPackagesByDefault()
    {
        // Arrange
        var package = Directory.CreateDirectory(
            Path.Combine(_temporaryDirectory, "Example.app"));
        await File.WriteAllBytesAsync(
            Path.Combine(package.FullName, "payload"),
            new byte[5]);
        var scanner = new DiskScanner();

        // Act
        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory));

        // Assert
        var packageItem = progress[^1].Root.Children.Single();
        Assert.Multiple(() =>
        {
            Assert.That(packageItem.Name, Is.EqualTo("Example.app"));
            Assert.That(packageItem.Children.Single().Name, Is.EqualTo("payload"));
        });
    }

    [Test]
    public async Task ScanAsyncDoesNotFollowSymbolicLinksByDefault()
    {
        // Arrange
        var target = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "target"));
        await File.WriteAllBytesAsync(Path.Combine(target.FullName, "file"), new byte[9]);
        var linkPath = Path.Combine(_temporaryDirectory, "link");
        Directory.CreateSymbolicLink(linkPath, target.FullName);
        var scanner = new DiskScanner();

        // Act
        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(progress[^1].FilesScanned, Is.EqualTo(1));
            Assert.That(progress[^1].Root.SizeBytes, Is.EqualTo(9));
            Assert.That(progress[^1].Root.Children.Any(item => item.Name == "link"), Is.False);
        });
    }

    [Test]
    public async Task ScanAsyncExcludesSymbolicLinksToFilesByDefault()
    {
        // Arrange
        var targetFile = Path.Combine(_temporaryDirectory, "target.bin");
        await File.WriteAllBytesAsync(targetFile, new byte[12]);
        File.CreateSymbolicLink(Path.Combine(_temporaryDirectory, "link.bin"), targetFile);
        var scanner = new DiskScanner();

        // Act
        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory));

        // Assert
        var result = progress[^1];
        Assert.Multiple(() =>
        {
            Assert.That(result.FilesScanned, Is.EqualTo(1));
            Assert.That(result.Root.SizeBytes, Is.EqualTo(12));
            Assert.That(result.Root.Children.Any(item => item.Name == "link.bin"), Is.False);
        });
    }

    [Test]
    public async Task ScanAsyncFollowsSymbolicLinksWhenOptionIsEnabled()
    {
        // Arrange
        // The target lives outside the scanned tree so the link is the only path to it.
        var target = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "outside", "target"));
        await File.WriteAllBytesAsync(Path.Combine(target.FullName, "file"), new byte[9]);
        var scanRoot = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "scan")).FullName;
        var linkPath = Path.Combine(scanRoot, "link");
        Directory.CreateSymbolicLink(linkPath, target.FullName);
        var scanner = new DiskScanner();

        // Act
        var progress = await CollectAsync(scanner.ScanAsync(
            scanRoot,
            new ScanOptions { FollowSymbolicLinks = true }));

        // Assert
        var result = progress[^1];
        var link = result.Root.Children.Single(item => item.Name == "link");
        Assert.Multiple(() =>
        {
            Assert.That(link.Children.Single().Name, Is.EqualTo("file"));
            Assert.That(link.SizeBytes, Is.EqualTo(9));
        });
    }

    [Test]
    public async Task ScanAsyncAvoidsCyclesWhenFollowingSymbolicLinks()
    {
        // Arrange
        var branch = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "branch"));
        await File.WriteAllBytesAsync(Path.Combine(branch.FullName, "file"), new byte[4]);
        // A symbolic link that points back to its own ancestor would loop forever
        // if cycles were not detected.
        Directory.CreateSymbolicLink(
            Path.Combine(branch.FullName, "loop"),
            _temporaryDirectory);
        var scanner = new DiskScanner();

        // Act
        var progress = await CollectAsync(scanner.ScanAsync(
            _temporaryDirectory,
            new ScanOptions { FollowSymbolicLinks = true }));

        // Assert
        var result = progress[^1];
        var loop = result.Root.Children
            .Single(item => item.Name == "branch")
            .Children
            .Single(item => item.Name == "loop");
        Assert.Multiple(() =>
        {
            Assert.That(result.IsCompleted, Is.True);
            // The loop link resolves to an already-visited directory, so it is not re-expanded.
            Assert.That(loop.Children, Is.Empty);
            Assert.That(result.FilesScanned, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ScanAsyncCollectsIoErrorsAndCompletes()
    {
        // Arrange
        var missingDirectory = Path.Combine(_temporaryDirectory, "missing");
        var scanner = new DiskScanner();

        // Act
        var progress = await CollectAsync(scanner.ScanAsync(missingDirectory));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(progress[^1].IsCompleted, Is.True);
            Assert.That(progress[^1].Errors, Has.Count.EqualTo(1));
            Assert.That(progress[^1].Errors[0].Path, Is.EqualTo(missingDirectory));
            Assert.That(progress[^1].Errors[0].ExceptionType, Is.EqualTo(nameof(DirectoryNotFoundException)));
        });
    }

    [TestCase(nameof(UnauthorizedAccessException))]
    [TestCase(nameof(IOException))]
    public async Task ScanAsyncContinuesAfterRecoverableChildError(string exceptionType)
    {
        // Arrange
        var inaccessibleEntry = Directory.CreateDirectory(
            Path.Combine(_temporaryDirectory, "inaccessible")).FullName;
        var accessibleFile = Path.Combine(_temporaryDirectory, "accessible.bin");
        await File.WriteAllBytesAsync(accessibleFile, new byte[17]);
        var scanner = new DiskScanner(path =>
        {
            if (path == inaccessibleEntry)
            {
                throw exceptionType == nameof(UnauthorizedAccessException)
                    ? new UnauthorizedAccessException("Access denied for the test.")
                    : new IOException("I/O failed for the test.");
            }

            return Directory.GetFileSystemEntries(path);
        });

        // Act
        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory));

        // Assert
        var result = progress[^1];
        Assert.Multiple(() =>
        {
            Assert.That(result.IsCompleted, Is.True);
            Assert.That(result.FilesScanned, Is.EqualTo(1));
            Assert.That(result.BytesScanned, Is.EqualTo(17));
            Assert.That(result.Root.Children.Select(item => item.Path), Does.Contain(accessibleFile));
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0].Path, Is.EqualTo(inaccessibleEntry));
            Assert.That(result.Errors[0].Message, Is.Not.Empty);
            Assert.That(result.Errors[0].ExceptionType, Is.EqualTo(exceptionType));
        });
    }

    [Test]
    public async Task ScanAsyncUsesAllocatedSizeReaderWhenMeasureAllocatedSizeIsEnabled()
    {
        // Arrange
        var smallFile = Path.Combine(_temporaryDirectory, "placeholder.bin");
        var largeFile = Path.Combine(_temporaryDirectory, "local.bin");
        await File.WriteAllBytesAsync(smallFile, new byte[10]);
        await File.WriteAllBytesAsync(largeFile, new byte[10]);
        // Simulate an undownloaded cloud placeholder: it has a logical length but
        // occupies no blocks on disk, so the allocated reader returns zero for it.
        var scanner = new DiskScanner(
            Directory.EnumerateFileSystemEntries,
            allocatedSizeReader: path => path == smallFile ? 0 : 4096);
        var options = new ScanOptions { MeasureAllocatedSize = true };

        // Act
        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory, options));

        // Assert
        var result = progress[^1];
        var placeholder = result.Root.Children.Single(item => item.Name == "placeholder.bin");
        var local = result.Root.Children.Single(item => item.Name == "local.bin");
        Assert.Multiple(() =>
        {
            Assert.That(placeholder.SizeBytes, Is.EqualTo(0));
            Assert.That(local.SizeBytes, Is.EqualTo(4096));
            Assert.That(result.Root.SizeBytes, Is.EqualTo(4096));
            Assert.That(result.BytesScanned, Is.EqualTo(4096));
        });
    }

    [Test]
    public void ScanAsyncHonorsCancellation()
    {
        // Arrange
        var scanner = new DiskScanner();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        // Act
        var action = async () =>
            await CollectAsync(scanner.ScanAsync(_temporaryDirectory, cancellationToken: cancellation.Token));

        // Assert
        Assert.That(action, Throws.InstanceOf<OperationCanceledException>());
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
