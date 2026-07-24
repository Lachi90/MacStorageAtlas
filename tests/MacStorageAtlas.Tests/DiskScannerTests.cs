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
        var nestedDirectory = Directory.CreateDirectory(
            Path.Combine(_temporaryDirectory, "first", "second"));
        await File.WriteAllBytesAsync(
            Path.Combine(_temporaryDirectory, "root.bin"),
            new byte[7]);
        await File.WriteAllBytesAsync(
            Path.Combine(nestedDirectory.FullName, "nested.bin"),
            new byte[11]);
        var scanner = new DiskScanner();

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory));

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
            Assert.That(
                progress.Select(item => item.MeasurementMode),
                Is.All.EqualTo(StorageMeasurementMode.Logical));
        });
    }

    [Test]
    public async Task ScanAsyncSortsCompletedTreeBySizeDescendingRecursively()
    {
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

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory));

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
        await File.WriteAllBytesAsync(
            Path.Combine(_temporaryDirectory, "file.bin"),
            new byte[5]);
        var scanner = new DiskScanner();

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory));

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
        await File.WriteAllBytesAsync(
            Path.Combine(_temporaryDirectory, ".hidden"),
            new byte[3]);
        await File.WriteAllBytesAsync(
            Path.Combine(_temporaryDirectory, "visible"),
            new byte[5]);
        var scanner = new DiskScanner();

        var defaultProgress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory));
        var includedProgress = await CollectAsync(scanner.ScanAsync(
            _temporaryDirectory,
            new ScanOptions { IncludeHiddenFiles = true }));

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
        var hiddenDirectory = Directory.CreateDirectory(
            Path.Combine(_temporaryDirectory, ".hidden-folder"));
        await File.WriteAllBytesAsync(
            Path.Combine(hiddenDirectory.FullName, "buried.bin"),
            new byte[6]);
        await File.WriteAllBytesAsync(
            Path.Combine(_temporaryDirectory, "visible.bin"),
            new byte[5]);
        var scanner = new DiskScanner();

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory));

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
        var hiddenDirectory = Directory.CreateDirectory(
            Path.Combine(_temporaryDirectory, ".hidden-folder"));
        await File.WriteAllBytesAsync(
            Path.Combine(hiddenDirectory.FullName, "buried.bin"),
            new byte[6]);
        await File.WriteAllBytesAsync(
            Path.Combine(_temporaryDirectory, "visible.bin"),
            new byte[5]);
        var scanner = new DiskScanner();

        var progress = await CollectAsync(scanner.ScanAsync(
            _temporaryDirectory,
            new ScanOptions { IncludeHiddenFiles = true }));

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
        var package = Directory.CreateDirectory(
            Path.Combine(_temporaryDirectory, "Example.app", "Contents"));
        await File.WriteAllBytesAsync(
            Path.Combine(package.FullName, "payload"),
            new byte[13]);
        var scanner = new DiskScanner();
        var options = new ScanOptions { TreatPackagesAsDirectories = false };

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory, options));

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
        var package = Directory.CreateDirectory(
            Path.Combine(_temporaryDirectory, "Example.app", "Contents"));
        await File.WriteAllBytesAsync(
            Path.Combine(package.FullName, "payload"),
            new byte[13]);
        var scanner = new DiskScanner();
        var options = new ScanOptions { TreatPackagesAsDirectories = true };

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory, options));

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
        var package = Directory.CreateDirectory(
            Path.Combine(_temporaryDirectory, "Example.app"));
        await File.WriteAllBytesAsync(
            Path.Combine(package.FullName, "payload"),
            new byte[5]);
        var scanner = new DiskScanner();

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory));

        var packageItem = progress[^1].Root.Children.Single();
        Assert.Multiple(() =>
        {
            Assert.That(packageItem.Name, Is.EqualTo("Example.app"));
            Assert.That(packageItem.Children.Single().Name, Is.EqualTo("payload"));
        });
    }

    [Test]
    public async Task ScanAsyncUsesAllocatedMeasurementForCollapsedPackageAndExcludesSymbolicLink()
    {
        var scanRoot = Directory.CreateDirectory(
            Path.Combine(_temporaryDirectory, "scan"));
        var package = Directory.CreateDirectory(
            Path.Combine(scanRoot.FullName, "Example.app", "Contents"));
        var payloadPath = Path.Combine(package.FullName, "payload");
        await File.WriteAllBytesAsync(payloadPath, new byte[13]);

        var outsideTarget = Path.Combine(_temporaryDirectory, "outside.bin");
        await File.WriteAllBytesAsync(outsideTarget, new byte[23]);
        var linkPath = Path.Combine(scanRoot.FullName, "outside-link.bin");
        File.CreateSymbolicLink(linkPath, outsideTarget);

        var scanner = new DiskScanner(
            Directory.EnumerateFileSystemEntries,
            allocatedMetadataReader: path =>
                Metadata(path == payloadPath ? 8192 : 16384));
        var options = new ScanOptions
        {
            MeasurementMode = StorageMeasurementMode.Allocated,
            TreatPackagesAsDirectories = false
        };

        var progress = await CollectAsync(scanner.ScanAsync(scanRoot.FullName, options));

        var result = progress[^1];
        var packageItem = result.Root.Children.Single();
        Assert.Multiple(() =>
        {
            Assert.That(packageItem.Name, Is.EqualTo("Example.app"));
            Assert.That(packageItem.Children, Is.Empty);
            Assert.That(packageItem.SizeBytes, Is.EqualTo(8192));
            Assert.That(result.Root.SizeBytes, Is.EqualTo(8192));
            Assert.That(result.BytesScanned, Is.EqualTo(8192));
            Assert.That(result.Root.Children.Any(item => item.Path == linkPath), Is.False);
            Assert.That(
                progress.Select(item => item.MeasurementMode),
                Is.All.EqualTo(StorageMeasurementMode.Allocated));
        });
    }

    [Test]
    public async Task ScanAsyncDoesNotFollowSymbolicLinksByDefault()
    {
        var target = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "target"));
        await File.WriteAllBytesAsync(Path.Combine(target.FullName, "file"), new byte[9]);
        var linkPath = Path.Combine(_temporaryDirectory, "link");
        Directory.CreateSymbolicLink(linkPath, target.FullName);
        var scanner = new DiskScanner();

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory));

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
        var targetFile = Path.Combine(_temporaryDirectory, "target.bin");
        await File.WriteAllBytesAsync(targetFile, new byte[12]);
        File.CreateSymbolicLink(Path.Combine(_temporaryDirectory, "link.bin"), targetFile);
        var scanner = new DiskScanner();

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory));

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
        var target = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "outside", "target"));
        await File.WriteAllBytesAsync(Path.Combine(target.FullName, "file"), new byte[9]);
        var scanRoot = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "scan")).FullName;
        var linkPath = Path.Combine(scanRoot, "link");
        Directory.CreateSymbolicLink(linkPath, target.FullName);
        var scanner = new DiskScanner();

        var progress = await CollectAsync(scanner.ScanAsync(
            scanRoot,
            new ScanOptions { FollowSymbolicLinks = true }));

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
        var branch = Directory.CreateDirectory(Path.Combine(_temporaryDirectory, "branch"));
        await File.WriteAllBytesAsync(Path.Combine(branch.FullName, "file"), new byte[4]);
        Directory.CreateSymbolicLink(
            Path.Combine(branch.FullName, "loop"),
            _temporaryDirectory);
        var scanner = new DiskScanner();

        var progress = await CollectAsync(scanner.ScanAsync(
            _temporaryDirectory,
            new ScanOptions { FollowSymbolicLinks = true }));

        var result = progress[^1];
        var loop = result.Root.Children
            .Single(item => item.Name == "branch")
            .Children
            .Single(item => item.Name == "loop");
        Assert.Multiple(() =>
        {
            Assert.That(result.IsCompleted, Is.True);
            Assert.That(loop.Children, Is.Empty);
            Assert.That(result.FilesScanned, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ScanAsyncCollectsIoErrorsAndCompletes()
    {
        var missingDirectory = Path.Combine(_temporaryDirectory, "missing");
        var scanner = new DiskScanner();

        var progress = await CollectAsync(scanner.ScanAsync(missingDirectory));

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

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory));

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
    public async Task ScanAsyncUsesAllocatedMetadataReaderInAllocatedMode()
    {
        var smallFile = Path.Combine(_temporaryDirectory, "placeholder.bin");
        var largeFile = Path.Combine(_temporaryDirectory, "local.bin");
        await File.WriteAllBytesAsync(smallFile, new byte[10]);
        await File.WriteAllBytesAsync(largeFile, new byte[10]);
        var scanner = new DiskScanner(
            Directory.EnumerateFileSystemEntries,
            allocatedMetadataReader: path =>
                Metadata(path == smallFile ? 0 : 4096));
        var options = new ScanOptions
        {
            MeasurementMode = StorageMeasurementMode.Allocated
        };

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory, options));

        var result = progress[^1];
        var placeholder = result.Root.Children.Single(item => item.Name == "placeholder.bin");
        var local = result.Root.Children.Single(item => item.Name == "local.bin");
        Assert.Multiple(() =>
        {
            Assert.That(placeholder.SizeBytes, Is.EqualTo(0));
            Assert.That(local.SizeBytes, Is.EqualTo(4096));
            Assert.That(result.Root.SizeBytes, Is.EqualTo(4096));
            Assert.That(result.BytesScanned, Is.EqualTo(4096));
            Assert.That(
                progress.Select(item => item.MeasurementMode),
                Is.All.EqualTo(StorageMeasurementMode.Allocated));
        });
    }

    [TestCase(StorageMeasurementMode.Allocated)]
    [TestCase(StorageMeasurementMode.HardlinkAwareAllocated)]
    public async Task ScanAsyncReportsFailedAllocatedReadAndKeepsSuccessfulSibling(
        StorageMeasurementMode measurementMode)
    {
        var failedFile = Path.Combine(_temporaryDirectory, "failed.bin");
        var successfulFile = Path.Combine(_temporaryDirectory, "successful.bin");
        await File.WriteAllBytesAsync(failedFile, new byte[10]);
        await File.WriteAllBytesAsync(successfulFile, new byte[10]);
        var scanner = new DiskScanner(
            Directory.EnumerateFileSystemEntries,
            allocatedMetadataReader: path =>
            {
                if (path == failedFile)
                {
                    throw new IOException("Allocated metadata unavailable.");
                }

                return Metadata(4096);
            });
        var options = new ScanOptions
        {
            MeasurementMode = measurementMode
        };

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory, options));

        var result = progress[^1];
        Assert.Multiple(() =>
        {
            Assert.That(result.IsCompleted, Is.True);
            Assert.That(result.FilesScanned, Is.EqualTo(1));
            Assert.That(result.BytesScanned, Is.EqualTo(4096));
            Assert.That(result.Root.SizeBytes, Is.EqualTo(4096));
            Assert.That(result.Root.Children.Select(item => item.Path),
                Is.EquivalentTo(new[] { successfulFile }));
            Assert.That(result.Errors, Has.Count.EqualTo(1));
            Assert.That(result.Errors[0].Path, Is.EqualTo(failedFile));
            Assert.That(result.Errors[0].ExceptionType, Is.EqualTo(nameof(IOException)));
            Assert.That(result.MeasurementMode, Is.EqualTo(measurementMode));
        });
    }

    [Test]
    public async Task ScanAsyncCountsRepeatedIdentityPerPathInAllocatedMode()
    {
        var firstFile = Path.Combine(_temporaryDirectory, "first.bin");
        var secondFile = Path.Combine(_temporaryDirectory, "second.bin");
        await File.WriteAllBytesAsync(firstFile, new byte[10]);
        await File.WriteAllBytesAsync(secondFile, new byte[10]);
        var scanner = new DiskScanner(
            path => path == _temporaryDirectory
                ? [firstFile, secondFile]
                : Directory.EnumerateFileSystemEntries(path),
            allocatedMetadataReader: _ => Metadata(4096, fileId: 5, linkCount: 2));
        var options = new ScanOptions
        {
            MeasurementMode = StorageMeasurementMode.Allocated
        };

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory, options));

        var result = progress[^1];
        Assert.Multiple(() =>
        {
            Assert.That(result.FilesScanned, Is.EqualTo(2));
            Assert.That(result.BytesScanned, Is.EqualTo(8192));
            Assert.That(result.Root.SizeBytes, Is.EqualTo(8192));
            Assert.That(result.Root.MeasuredSizeBytes, Is.EqualTo(8192));
            Assert.That(
                result.Root.Children,
                Has.None.Matches<DiskItem>(item => item.IsSizeCountedElsewhere));
        });
    }

    [Test]
    public async Task ScanAsyncCountsRepeatedIdentityOnceInHardlinkAwareMode()
    {
        var firstFile = Path.Combine(_temporaryDirectory, "first.bin");
        var secondFile = Path.Combine(_temporaryDirectory, "second.bin");
        await File.WriteAllBytesAsync(firstFile, new byte[10]);
        await File.WriteAllBytesAsync(secondFile, new byte[10]);
        var scanner = new DiskScanner(
            path => path == _temporaryDirectory
                ? [firstFile, secondFile]
                : Directory.EnumerateFileSystemEntries(path),
            allocatedMetadataReader: _ => Metadata(4096, fileId: 5, linkCount: 2));
        var options = new ScanOptions
        {
            MeasurementMode = StorageMeasurementMode.HardlinkAwareAllocated
        };

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory, options));

        var result = progress[^1];
        var first = result.Root.Children.Single(item => item.Path == firstFile);
        var second = result.Root.Children.Single(item => item.Path == secondFile);
        Assert.Multiple(() =>
        {
            Assert.That(result.FilesScanned, Is.EqualTo(2));
            Assert.That(result.BytesScanned, Is.EqualTo(4096));
            Assert.That(result.Root.SizeBytes, Is.EqualTo(4096));
            Assert.That(result.Root.MeasuredSizeBytes, Is.EqualTo(8192));
            Assert.That(first.SizeBytes, Is.EqualTo(4096));
            Assert.That(first.MeasuredSizeBytes, Is.EqualTo(4096));
            Assert.That(first.IsSizeCountedElsewhere, Is.False);
            Assert.That(second.SizeBytes, Is.Zero);
            Assert.That(second.MeasuredSizeBytes, Is.EqualTo(4096));
            Assert.That(second.IsSizeCountedElsewhere, Is.True);
            Assert.That(
                progress.Select(item => item.MeasurementMode),
                Is.All.EqualTo(StorageMeasurementMode.HardlinkAwareAllocated));
        });
    }

    [Test]
    public async Task ScanAsyncKeepsDirectoryTotalsAdditiveAcrossHardlinkedSiblings()
    {
        var firstDirectory = Directory.CreateDirectory(
            Path.Combine(_temporaryDirectory, "first"));
        var secondDirectory = Directory.CreateDirectory(
            Path.Combine(_temporaryDirectory, "second"));
        var firstFile = Path.Combine(firstDirectory.FullName, "shared.bin");
        var secondFile = Path.Combine(secondDirectory.FullName, "shared.bin");
        await File.WriteAllBytesAsync(firstFile, new byte[10]);
        await File.WriteAllBytesAsync(secondFile, new byte[10]);
        var scanner = new DiskScanner(
            Directory.EnumerateFileSystemEntries,
            allocatedMetadataReader: _ => Metadata(4096, fileId: 7, linkCount: 2));
        var options = new ScanOptions
        {
            MeasurementMode = StorageMeasurementMode.HardlinkAwareAllocated
        };

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory, options));

        var root = progress[^1].Root;
        Assert.Multiple(() =>
        {
            Assert.That(root.SizeBytes, Is.EqualTo(4096));
            Assert.That(root.MeasuredSizeBytes, Is.EqualTo(8192));
            Assert.That(root.Children.Sum(item => item.SizeBytes), Is.EqualTo(root.SizeBytes));
            Assert.That(
                root.Children.Sum(item => item.MeasuredSizeBytes),
                Is.EqualTo(root.MeasuredSizeBytes));
            Assert.That(
                root.Children.SelectMany(item => item.Children).Count(
                    item => item.IsSizeCountedElsewhere),
                Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ScanAsyncDoesNotMergeSameFileIdAcrossDevices()
    {
        var firstFile = Path.Combine(_temporaryDirectory, "first.bin");
        var secondFile = Path.Combine(_temporaryDirectory, "second.bin");
        await File.WriteAllBytesAsync(firstFile, new byte[10]);
        await File.WriteAllBytesAsync(secondFile, new byte[10]);
        var scanner = new DiskScanner(
            Directory.EnumerateFileSystemEntries,
            allocatedMetadataReader: path => path == firstFile
                ? Metadata(4096, fileId: 9, linkCount: 2, deviceId: 1)
                : Metadata(8192, fileId: 9, linkCount: 2, deviceId: 2));
        var options = new ScanOptions
        {
            MeasurementMode = StorageMeasurementMode.HardlinkAwareAllocated
        };

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory, options));

        Assert.Multiple(() =>
        {
            Assert.That(progress[^1].Root.SizeBytes, Is.EqualTo(12_288));
            Assert.That(
                progress[^1].Root.Children,
                Has.None.Matches<DiskItem>(item => item.IsSizeCountedElsewhere));
        });
    }

    [Test]
    public async Task ScanAsyncCountsIncludedLinkWhenAnotherLinkIsOutsideScope()
    {
        var file = Path.Combine(_temporaryDirectory, "included.bin");
        await File.WriteAllBytesAsync(file, new byte[10]);
        var scanner = new DiskScanner(
            Directory.EnumerateFileSystemEntries,
            allocatedMetadataReader: _ => Metadata(4096, fileId: 11, linkCount: 2));
        var options = new ScanOptions
        {
            MeasurementMode = StorageMeasurementMode.HardlinkAwareAllocated
        };

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory, options));

        var item = progress[^1].Root.Children.Single();
        Assert.Multiple(() =>
        {
            Assert.That(item.SizeBytes, Is.EqualTo(4096));
            Assert.That(item.IsSizeCountedElsewhere, Is.False);
            Assert.That(progress[^1].BytesScanned, Is.EqualTo(4096));
        });
    }

    [Test]
    public async Task ScanAsyncMarksRepeatedZeroByteIdentityAsCountedElsewhere()
    {
        var firstFile = Path.Combine(_temporaryDirectory, "first.bin");
        var secondFile = Path.Combine(_temporaryDirectory, "second.bin");
        await File.WriteAllBytesAsync(firstFile, []);
        await File.WriteAllBytesAsync(secondFile, []);
        var scanner = new DiskScanner(
            path => path == _temporaryDirectory
                ? [firstFile, secondFile]
                : Directory.EnumerateFileSystemEntries(path),
            allocatedMetadataReader: _ => Metadata(0, fileId: 13, linkCount: 2));
        var options = new ScanOptions
        {
            MeasurementMode = StorageMeasurementMode.HardlinkAwareAllocated
        };

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory, options));

        var items = progress[^1].Root.Children;
        Assert.Multiple(() =>
        {
            Assert.That(items, Has.Count.EqualTo(2));
            Assert.That(items.Count(item => item.IsSizeCountedElsewhere), Is.EqualTo(1));
            Assert.That(progress[^1].BytesScanned, Is.Zero);
        });
    }

    [Test]
    public async Task ScanAsyncCountsFollowedFileAliasOnceInHardlinkAwareMode()
    {
        var targetFile = Path.Combine(_temporaryDirectory, "target.bin");
        var linkFile = Path.Combine(_temporaryDirectory, "link.bin");
        await File.WriteAllBytesAsync(targetFile, new byte[10]);
        File.CreateSymbolicLink(linkFile, targetFile);
        var scanner = new DiskScanner(
            path => path == _temporaryDirectory
                ? [targetFile, linkFile]
                : Directory.EnumerateFileSystemEntries(path),
            allocatedMetadataReader: _ => Metadata(4096, fileId: 15));
        var options = new ScanOptions
        {
            FollowSymbolicLinks = true,
            MeasurementMode = StorageMeasurementMode.HardlinkAwareAllocated
        };

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory, options));

        var result = progress[^1];
        Assert.Multiple(() =>
        {
            Assert.That(result.FilesScanned, Is.EqualTo(2));
            Assert.That(result.BytesScanned, Is.EqualTo(4096));
            Assert.That(result.Root.Children, Has.Count.EqualTo(2));
            Assert.That(
                result.Root.Children.Count(item => item.IsSizeCountedElsewhere),
                Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ScanAsyncDoesNotRetainSingleLinkIdentityWhenLinksAreNotFollowed()
    {
        var firstFile = Path.Combine(_temporaryDirectory, "first.bin");
        var secondFile = Path.Combine(_temporaryDirectory, "second.bin");
        await File.WriteAllBytesAsync(firstFile, new byte[10]);
        await File.WriteAllBytesAsync(secondFile, new byte[10]);
        var scanner = new DiskScanner(
            Directory.EnumerateFileSystemEntries,
            allocatedMetadataReader: _ => Metadata(4096, fileId: 21));
        var options = new ScanOptions
        {
            MeasurementMode = StorageMeasurementMode.HardlinkAwareAllocated
        };

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory, options));

        Assert.Multiple(() =>
        {
            Assert.That(progress[^1].BytesScanned, Is.EqualTo(8192));
            Assert.That(
                progress[^1].Root.Children,
                Has.None.Matches<DiskItem>(item => item.IsSizeCountedElsewhere));
        });
    }

    [Test]
    public async Task ScanAsyncCountsHardlinksWithinCollapsedPackageOnce()
    {
        var packageDirectory = Directory.CreateDirectory(
            Path.Combine(_temporaryDirectory, "Example.app"));
        var firstFile = Path.Combine(packageDirectory.FullName, "first.bin");
        var secondFile = Path.Combine(packageDirectory.FullName, "second.bin");
        await File.WriteAllBytesAsync(firstFile, new byte[10]);
        await File.WriteAllBytesAsync(secondFile, new byte[10]);
        var scanner = new DiskScanner(
            Directory.EnumerateFileSystemEntries,
            allocatedMetadataReader: _ => Metadata(4096, fileId: 23, linkCount: 2));
        var options = new ScanOptions
        {
            MeasurementMode = StorageMeasurementMode.HardlinkAwareAllocated,
            TreatPackagesAsDirectories = false
        };

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory, options));

        var package = progress[^1].Root.Children.Single();
        Assert.Multiple(() =>
        {
            Assert.That(progress[^1].BytesScanned, Is.EqualTo(4096));
            Assert.That(package.SizeBytes, Is.EqualTo(4096));
            Assert.That(package.MeasuredSizeBytes, Is.EqualTo(8192));
            Assert.That(package.Children, Is.Empty);
        });
    }

    [Test]
    public async Task ScanAsyncCountsHardlinkAcrossCollapsedPackageBoundaryOnce()
    {
        var packageDirectory = Directory.CreateDirectory(
            Path.Combine(_temporaryDirectory, "Example.app"));
        var packageFile = Path.Combine(packageDirectory.FullName, "inside.bin");
        var outsideFile = Path.Combine(_temporaryDirectory, "outside.bin");
        await File.WriteAllBytesAsync(packageFile, new byte[10]);
        await File.WriteAllBytesAsync(outsideFile, new byte[10]);
        var scanner = new DiskScanner(
            path =>
            {
                if (path == _temporaryDirectory)
                {
                    return [packageDirectory.FullName, outsideFile];
                }

                return path == packageDirectory.FullName
                    ? [packageFile]
                    : Directory.EnumerateFileSystemEntries(path);
            },
            allocatedMetadataReader: _ => Metadata(4096, fileId: 17, linkCount: 2));
        var options = new ScanOptions
        {
            MeasurementMode = StorageMeasurementMode.HardlinkAwareAllocated,
            TreatPackagesAsDirectories = false
        };

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory, options));

        var result = progress[^1];
        var package = result.Root.Children.Single(item => item.IsDirectory);
        var outside = result.Root.Children.Single(item => !item.IsDirectory);
        Assert.Multiple(() =>
        {
            Assert.That(result.BytesScanned, Is.EqualTo(4096));
            Assert.That(result.Root.MeasuredSizeBytes, Is.EqualTo(8192));
            Assert.That(package.Children, Is.Empty);
            Assert.That(package.SizeBytes, Is.EqualTo(4096));
            Assert.That(package.MeasuredSizeBytes, Is.EqualTo(4096));
            Assert.That(outside.SizeBytes, Is.Zero);
            Assert.That(outside.MeasuredSizeBytes, Is.EqualTo(4096));
            Assert.That(outside.IsSizeCountedElsewhere, Is.True);
        });
    }

    [Test]
    public async Task ScanAsyncKeepsSuccessfulContributionWhenRepeatedPathMetadataFails()
    {
        var firstFile = Path.Combine(_temporaryDirectory, "first.bin");
        var failedFile = Path.Combine(_temporaryDirectory, "failed.bin");
        await File.WriteAllBytesAsync(firstFile, new byte[10]);
        await File.WriteAllBytesAsync(failedFile, new byte[10]);
        var scanner = new DiskScanner(
            path => path == _temporaryDirectory
                ? [firstFile, failedFile]
                : Directory.EnumerateFileSystemEntries(path),
            allocatedMetadataReader: path =>
            {
                if (path == failedFile)
                {
                    throw new IOException("Allocated metadata unavailable.");
                }

                return Metadata(4096, fileId: 19, linkCount: 2);
            });
        var options = new ScanOptions
        {
            MeasurementMode = StorageMeasurementMode.HardlinkAwareAllocated
        };

        var progress = await CollectAsync(scanner.ScanAsync(_temporaryDirectory, options));

        var result = progress[^1];
        Assert.Multiple(() =>
        {
            Assert.That(result.FilesScanned, Is.EqualTo(1));
            Assert.That(result.BytesScanned, Is.EqualTo(4096));
            Assert.That(result.Root.SizeBytes, Is.EqualTo(4096));
            Assert.That(result.Root.Children.Single().Path, Is.EqualTo(firstFile));
            Assert.That(result.Errors.Single().Path, Is.EqualTo(failedFile));
        });
    }

    [TestCase(StorageMeasurementMode.Allocated)]
    [TestCase(StorageMeasurementMode.HardlinkAwareAllocated)]
    public async Task ScanAsyncCancellationPreservesConsistentAllocatedPartialProgress(
        StorageMeasurementMode measurementMode)
    {
        var firstFile = Path.Combine(_temporaryDirectory, "first.bin");
        var secondFile = Path.Combine(_temporaryDirectory, "second.bin");
        await File.WriteAllBytesAsync(firstFile, new byte[10]);
        await File.WriteAllBytesAsync(secondFile, new byte[10]);
        var scanner = new DiskScanner(
            Directory.EnumerateFileSystemEntries,
            allocatedMetadataReader: path =>
                Metadata(path == firstFile ? 4096 : 8192));
        var options = new ScanOptions
        {
            MeasurementMode = measurementMode
        };
        using var cancellation = new CancellationTokenSource();
        var published = new List<ScanProgress>();

        var action = async () =>
        {
            await foreach (var progress in scanner.ScanAsync(
                               _temporaryDirectory,
                               options,
                               cancellation.Token))
            {
                published.Add(progress);
                if (progress.FilesScanned == 1)
                {
                    cancellation.Cancel();
                }
            }
        };

        Assert.That(action, Throws.InstanceOf<OperationCanceledException>());
        var latest = published[^1];
        Assert.Multiple(() =>
        {
            Assert.That(latest.IsCompleted, Is.False);
            Assert.That(latest.FilesScanned, Is.EqualTo(1));
            Assert.That(latest.Root.Children, Has.Count.EqualTo(1));
            Assert.That(latest.BytesScanned, Is.EqualTo(latest.Root.SizeBytes));
            Assert.That(
                latest.Root.Children.Sum(item => item.SizeBytes),
                Is.EqualTo(latest.Root.SizeBytes));
            Assert.That(latest.MeasurementMode, Is.EqualTo(measurementMode));
        });
    }

    [Test]
    public void ScanAsyncHonorsCancellation()
    {
        var scanner = new DiskScanner();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var action = async () =>
            await CollectAsync(scanner.ScanAsync(_temporaryDirectory, cancellationToken: cancellation.Token));

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

    private static AllocatedFileMetadata Metadata(
        long allocatedSizeBytes,
        ulong fileId = 1,
        uint linkCount = 1,
        ulong deviceId = 1) =>
        new(
            allocatedSizeBytes,
            new FileIdentity(deviceId, fileId),
            linkCount);
}
