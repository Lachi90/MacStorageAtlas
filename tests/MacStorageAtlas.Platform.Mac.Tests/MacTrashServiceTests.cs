using MacStorageAtlas.Platform.Mac;

namespace MacStorageAtlas.Platform.Mac.Tests;

public class MacTrashServiceTests
{
    [Test]
    public void MoveToTrashAsyncRejectsAMissingPath()
    {
        var mover = new FakeTrashItemMover();
        var service = new MacTrashService(mover);
        var missingPath = Path.Combine(
            Path.GetTempPath(),
            $"MacStorageAtlas-{Guid.NewGuid():N}");

        Assert.That(
            async () => await service.MoveToTrashAsync(missingPath),
            Throws.TypeOf<FileNotFoundException>());
        Assert.That(mover.MovedPaths, Is.Empty);
    }

    [Test]
    public void MoveToTrashAsyncRejectsABlankPath()
    {
        var service = new MacTrashService(new FakeTrashItemMover());

        Assert.That(
            async () => await service.MoveToTrashAsync("   "),
            Throws.TypeOf<ArgumentException>());
    }

    [Test]
    public async Task MoveToTrashAsyncPassesTheExistingPathToThePlatformMover()
    {
        var directory = Directory.CreateTempSubdirectory("MacStorageAtlas-Trash-");
        var path = Path.Combine(directory.FullName, "trash-me.txt");
        await File.WriteAllTextAsync(path, "temporary");
        var mover = new FakeTrashItemMover();
        var service = new MacTrashService(mover);

        try
        {
            await service.MoveToTrashAsync(path);

            Assert.That(mover.MovedPaths, Is.EqualTo(new[] { path }));
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Test]
    public async Task MoveToTrashAsyncReportsTheReasonMacOsReturned()
    {
        var directory = Directory.CreateTempSubdirectory("MacStorageAtlas-Trash-");
        var path = Path.Combine(directory.FullName, "trash-me.txt");
        await File.WriteAllTextAsync(path, "temporary");
        var mover = new FakeTrashItemMover
        {
            Result = TrashItemMoveResult.Failure("The item could not be moved to the Trash.")
        };
        var service = new MacTrashService(mover);

        try
        {
            Assert.That(
                async () => await service.MoveToTrashAsync(path),
                Throws.TypeOf<InvalidOperationException>()
                    .With.Message.EqualTo("The item could not be moved to the Trash."));
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Test]
    public async Task MoveToTrashAsyncReportsAGenericFailureWhenMacOsGivesNoReason()
    {
        var directory = Directory.CreateTempSubdirectory("MacStorageAtlas-Trash-");
        var path = Path.Combine(directory.FullName, "trash-me.txt");
        await File.WriteAllTextAsync(path, "temporary");
        var mover = new FakeTrashItemMover
        {
            Result = TrashItemMoveResult.Failure(null)
        };
        var service = new MacTrashService(mover);

        try
        {
            Assert.That(
                async () => await service.MoveToTrashAsync(path),
                Throws.TypeOf<InvalidOperationException>()
                    .With.Message.EqualTo(
                        "macOS could not move the selected item to Trash."));
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Test]
    public async Task MoveToTrashAsyncObservesCancellationBeforeTouchingTheFilesystem()
    {
        var directory = Directory.CreateTempSubdirectory("MacStorageAtlas-Trash-");
        var path = Path.Combine(directory.FullName, "trash-me.txt");
        await File.WriteAllTextAsync(path, "temporary");
        var mover = new FakeTrashItemMover();
        var service = new MacTrashService(mover);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        try
        {
            Assert.That(
                async () => await service.MoveToTrashAsync(path, cancellation.Token),
                Throws.InstanceOf<OperationCanceledException>());
            Assert.That(mover.MovedPaths, Is.Empty);
        }
        finally
        {
            Directory.Delete(directory.FullName, recursive: true);
        }
    }

    [Test]
    public async Task MoveToTrashAsyncMovesTemporaryFileToTrashOnMacOs()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Ignore("macOS Trash integration is only available on macOS.");
        }

        var directory = Directory.CreateTempSubdirectory("MacStorageAtlas-Trash-");
        var path = Path.Combine(directory.FullName, "trash-me.txt");
        await File.WriteAllTextAsync(path, "temporary");
        var service = new MacTrashService();

        try
        {
            await service.MoveToTrashAsync(path);

            Assert.That(File.Exists(path), Is.False);
        }
        finally
        {
            if (Directory.Exists(directory.FullName))
            {
                Directory.Delete(directory.FullName, recursive: true);
            }
        }
    }

    [Test]
    public async Task NativeTrashItemMoverMovesAnExistingItemOnMacOs()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Ignore("The native Trash API is only available on macOS.");
        }

        var directory = Directory.CreateTempSubdirectory("MacStorageAtlas-Trash-");
        var path = Path.Combine(directory.FullName, "native-trash-me.txt");
        await File.WriteAllTextAsync(path, "temporary");

        try
        {
            var result = new NativeTrashItemMover().Move(path);

            Assert.Multiple(() =>
            {
                Assert.That(result.Succeeded, Is.True, result.FailureReason);
                Assert.That(File.Exists(path), Is.False);
            });
        }
        finally
        {
            if (Directory.Exists(directory.FullName))
            {
                Directory.Delete(directory.FullName, recursive: true);
            }
        }
    }

    [Test]
    public void NativeTrashItemMoverReportsAReasonForAMissingItemOnMacOs()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Ignore("The native Trash API is only available on macOS.");
        }

        var missingPath = Path.Combine(
            Path.GetTempPath(),
            $"MacStorageAtlas-{Guid.NewGuid():N}");

        var result = new NativeTrashItemMover().Move(missingPath);

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.FailureReason, Is.Not.Null.And.Not.Empty);
        });
    }

    private sealed class FakeTrashItemMover : ITrashItemMover
    {
        public TrashItemMoveResult Result { get; init; } = TrashItemMoveResult.Success;

        public List<string> MovedPaths { get; } = [];

        public TrashItemMoveResult Move(string path)
        {
            MovedPaths.Add(path);

            return Result;
        }
    }
}
