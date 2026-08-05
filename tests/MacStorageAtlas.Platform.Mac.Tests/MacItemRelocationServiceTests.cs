using MacStorageAtlas.Platform.Mac;

namespace MacStorageAtlas.Platform.Mac.Tests;

public class MacItemRelocationServiceTests
{
    private string _root = string.Empty;
    private string _source = string.Empty;
    private string _destination = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _root = Directory.CreateTempSubdirectory("MacStorageAtlas-Relocate-").FullName;
        _source = Path.Combine(_root, "source");
        _destination = Path.Combine(_root, "destination");
        Directory.CreateDirectory(_source);
        Directory.CreateDirectory(_destination);
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
    public async Task MoveAsyncRenamesAFileWithinTheSameVolume()
    {
        var path = await CreateFileAsync("clip.mov", "payload");
        var service = new MacItemRelocationService();

        await service.MoveAsync(path, _destination);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(path), Is.False);
            Assert.That(File.Exists(Path.Combine(_destination, "clip.mov")), Is.True);
        });
    }

    [Test]
    public async Task MoveAsyncRenamesADirectoryWithinTheSameVolume()
    {
        var path = await CreateDirectoryAsync("media");
        var service = new MacItemRelocationService();

        await service.MoveAsync(path, _destination);

        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(path), Is.False);
            Assert.That(
                File.Exists(Path.Combine(_destination, "media", "nested", "inner.bin")),
                Is.True);
        });
    }

    [Test]
    public void MoveAsyncRejectsAMissingSource()
    {
        var service = new MacItemRelocationService();
        var missing = Path.Combine(_source, "missing.bin");

        Assert.That(
            async () => await service.MoveAsync(missing, _destination),
            Throws.TypeOf<FileNotFoundException>());
    }

    [Test]
    public async Task MoveAsyncRejectsAMissingDestination()
    {
        var path = await CreateFileAsync("clip.mov", "payload");
        var service = new MacItemRelocationService();

        Assert.Multiple(() =>
        {
            Assert.That(
                async () => await service.MoveAsync(path, Path.Combine(_root, "gone")),
                Throws.TypeOf<DirectoryNotFoundException>());
            Assert.That(File.Exists(path), Is.True);
        });
    }

    [Test]
    public async Task MoveAsyncRefusesToOverwriteACollidingDestinationItem()
    {
        var path = await CreateFileAsync("clip.mov", "source payload");
        var existing = Path.Combine(_destination, "clip.mov");
        await File.WriteAllTextAsync(existing, "destination payload");
        var service = new MacItemRelocationService();

        Assert.That(
            async () => await service.MoveAsync(path, _destination),
            Throws.TypeOf<IOException>());

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(path), Is.True);
            Assert.That(File.ReadAllText(existing), Is.EqualTo("destination payload"));
        });
    }

    [Test]
    public async Task CopyAsyncRefusesToOverwriteACollidingDestinationItem()
    {
        var path = await CreateFileAsync("clip.mov", "source payload");
        var existing = Path.Combine(_destination, "clip.mov");
        await File.WriteAllTextAsync(existing, "destination payload");
        var service = new MacItemRelocationService();

        Assert.That(
            async () => await service.CopyAsync(path, _destination),
            Throws.TypeOf<IOException>());

        Assert.That(File.ReadAllText(existing), Is.EqualTo("destination payload"));
    }

    [Test]
    public async Task CopyAsyncKeepsTheSourceAndCopiesTheFileOnMacOs()
    {
        RequireMacOs();

        var path = await CreateFileAsync("clip.mov", "payload");
        var service = new MacItemRelocationService();

        await service.CopyAsync(path, _destination);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(path), Is.True);
            Assert.That(
                File.ReadAllText(Path.Combine(_destination, "clip.mov")),
                Is.EqualTo("payload"));
        });
    }

    [Test]
    public async Task CopyAsyncCopiesADirectoryTreeOnMacOs()
    {
        RequireMacOs();

        var path = await CreateDirectoryAsync("media");
        var service = new MacItemRelocationService();

        await service.CopyAsync(path, _destination);

        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(path), Is.True);
            Assert.That(
                File.ReadAllText(Path.Combine(_destination, "media", "nested", "inner.bin")),
                Is.EqualTo("inner"));
        });
    }

    [Test]
    public async Task CopyAsyncPreservesModificationTimeOnMacOs()
    {
        RequireMacOs();

        var path = await CreateFileAsync("clip.mov", "payload");
        var modifiedTime = new DateTime(2020, 3, 4, 5, 6, 7, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(path, modifiedTime);
        var service = new MacItemRelocationService();

        await service.CopyAsync(path, _destination);

        Assert.That(
            File.GetLastWriteTimeUtc(Path.Combine(_destination, "clip.mov")),
            Is.EqualTo(modifiedTime).Within(TimeSpan.FromSeconds(1)));
    }

    [Test]
    public async Task CopyAsyncStopsWhenCancelledBeforeStartingOnMacOs()
    {
        RequireMacOs();

        var path = await CreateFileAsync("clip.mov", "payload");
        var service = new MacItemRelocationService();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Assert.That(
            async () => await service.CopyAsync(path, _destination, cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(path), Is.True);
            Assert.That(File.Exists(Path.Combine(_destination, "clip.mov")), Is.False);
        });
    }

    [Test]
    public async Task MoveAsyncFallsBackToVerifiedCopyThenDeleteWhenRenameFails()
    {
        RequireMacOs();

        var path = await CreateDirectoryAsync("media");
        var service = new MacItemRelocationService((_, _) => false);

        await service.MoveAsync(path, _destination);

        Assert.Multiple(() =>
        {
            Assert.That(Directory.Exists(path), Is.False);
            Assert.That(
                File.ReadAllText(Path.Combine(_destination, "media", "nested", "inner.bin")),
                Is.EqualTo("inner"));
        });
    }

    [Test]
    public async Task MoveAsyncKeepsTheSourceWhenTheFallbackCopyFails()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Ignore("macOS relocation integration is only available on macOS.");
            return;
        }

        var path = await CreateFileAsync("clip.mov", "payload");
        var readOnlyDestination = Path.Combine(_root, "read-only");
        Directory.CreateDirectory(readOnlyDestination);
        File.SetUnixFileMode(
            readOnlyDestination,
            UnixFileMode.UserRead | UnixFileMode.UserExecute);
        var service = new MacItemRelocationService((_, _) => false);

        try
        {
            Assert.That(
                async () => await service.MoveAsync(path, readOnlyDestination),
                Throws.TypeOf<InvalidOperationException>());

            Assert.That(File.Exists(path), Is.True);
        }
        finally
        {
            File.SetUnixFileMode(
                readOnlyDestination,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Test]
    public async Task MoveAsyncKeepsTheSourceWhenCancelledBeforeTheCopyIsVerified()
    {
        RequireMacOs();

        var path = await CreateFileAsync("clip.mov", "payload");
        var service = new MacItemRelocationService((_, _) => false);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        Assert.That(
            async () => await service.MoveAsync(path, _destination, cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());

        Assert.That(File.Exists(path), Is.True);
    }

    [Test]
    public async Task MoveAsyncKeepsTheSourceWhenTheCopyCannotBeVerified()
    {
        RequireMacOs();

        var path = await CreateFileAsync("clip.mov", "payload");
        var service = new MacItemRelocationService((_, _) => false, (_, _) => false);

        Assert.That(
            async () => await service.MoveAsync(path, _destination),
            Throws.TypeOf<InvalidOperationException>()
                .With.Message.Contains("could not be verified"));

        Assert.That(File.Exists(path), Is.True);
    }

    [Test]
    public async Task MoveAsyncReportsThePartialCopyPathWhenVerificationFails()
    {
        RequireMacOs();

        var path = await CreateFileAsync("clip.mov", "payload");
        var service = new MacItemRelocationService((_, _) => false, (_, _) => false);

        Assert.That(
            async () => await service.MoveAsync(path, _destination),
            Throws.TypeOf<InvalidOperationException>()
                .With.Message.Contains(Path.Combine(_destination, "clip.mov")));
    }

    [Test]
    public async Task MoveAsyncCompletesACrossVolumeMoveOnARamDiskOnMacOs()
    {
        RequireMacOs();

        var ramDisk = await RamDisk.TryCreateAsync("MacStorageAtlasRelocate");
        if (ramDisk is null)
        {
            Assert.Ignore("A RAM disk could not be created for the cross-volume test.");
            return;
        }

        try
        {
            var path = await CreateFileAsync("clip.mov", "payload");
            var service = new MacItemRelocationService();

            await service.MoveAsync(path, ramDisk.MountPoint);

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(path), Is.False);
                Assert.That(
                    File.ReadAllText(Path.Combine(ramDisk.MountPoint, "clip.mov")),
                    Is.EqualTo("payload"));
            });
        }
        finally
        {
            await ramDisk.DisposeAsync();
        }
    }

    private static void RequireMacOs()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Ignore("macOS relocation integration is only available on macOS.");
        }
    }

    private async Task<string> CreateFileAsync(string name, string content)
    {
        var path = Path.Combine(_source, name);
        await File.WriteAllTextAsync(path, content);
        return path;
    }

    private async Task<string> CreateDirectoryAsync(string name)
    {
        var path = Path.Combine(_source, name);
        var nested = Path.Combine(path, "nested");
        Directory.CreateDirectory(nested);
        await File.WriteAllTextAsync(Path.Combine(path, "outer.bin"), "outer");
        await File.WriteAllTextAsync(Path.Combine(nested, "inner.bin"), "inner");
        return path;
    }
}
