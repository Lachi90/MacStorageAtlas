using System.Runtime.InteropServices;
using MacStorageAtlas.Core.Insights;
using MacStorageAtlas.Platform.Mac;

namespace MacStorageAtlas.Platform.Mac.Tests;

public sealed class MacDuplicateCandidateReaderTests
{
    private string _temporaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"MacStorageAtlas-duplicates-{Guid.NewGuid():N}");
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
    public void ReadAsyncReportsMissingFileBeforePlatformMetadataRead()
    {
        var reader = new MacDuplicateCandidateReader();
        var path = Path.Combine(_temporaryDirectory, "missing.bin");

        Assert.That(
            async () => await reader.ReadAsync(path),
            Throws.InstanceOf<FileNotFoundException>());
    }

    [Test]
    public async Task ReadAsyncReportsCurrentLogicalLengthAndIdentityOnMacOs()
    {
        RequireMacOs();
        var path = Path.Combine(_temporaryDirectory, "file.bin");
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4, 5]);
        var reader = new MacDuplicateCandidateReader();

        var metadata = await reader.ReadAsync(path);

        Assert.Multiple(() =>
        {
            Assert.That(metadata.LogicalLengthBytes, Is.EqualTo(5));
            Assert.That(metadata.ContentAvailability, Is.EqualTo(DuplicateContentAvailability.Local));
            Assert.That(metadata.Identity, Is.Not.Null);
            Assert.That(metadata.LinkCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ReadAsyncReportsSameIdentityForHardlinksOnMacOs()
    {
        RequireMacOs();
        var originalPath = Path.Combine(_temporaryDirectory, "original.bin");
        var linkedPath = Path.Combine(_temporaryDirectory, "linked.bin");
        await File.WriteAllBytesAsync(originalPath, [1, 2, 3, 4]);
        Assert.That(link(originalPath, linkedPath), Is.Zero);
        var reader = new MacDuplicateCandidateReader();

        var original = await reader.ReadAsync(originalPath);
        var linked = await reader.ReadAsync(linkedPath);

        Assert.Multiple(() =>
        {
            Assert.That(linked.Identity, Is.EqualTo(original.Identity));
            Assert.That(original.LinkCount, Is.EqualTo(2));
            Assert.That(linked.LinkCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task ReadAsyncUsesCloudStatusReaderForNotLocalAvailabilityOnMacOs()
    {
        RequireMacOs();
        var path = Path.Combine(_temporaryDirectory, "cloud.bin");
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4]);
        var reader = new MacDuplicateCandidateReader(
            new MacFileMetadataReader(),
            new FixedCloudFileStatusReader(DuplicateContentAvailability.NotLocal));

        var metadata = await reader.ReadAsync(path);

        Assert.That(
            metadata.ContentAvailability,
            Is.EqualTo(DuplicateContentAvailability.NotLocal));
    }

    [Test]
    public async Task OpenReadAsyncOpensReadableStreamOnMacOs()
    {
        RequireMacOs();
        var path = Path.Combine(_temporaryDirectory, "file.bin");
        await File.WriteAllBytesAsync(path, [9, 8, 7]);
        var reader = new MacDuplicateCandidateReader();

        await using var stream = await reader.OpenReadAsync(path);
        var buffer = new byte[3];
        var read = await stream.ReadAsync(buffer);

        Assert.Multiple(() =>
        {
            Assert.That(read, Is.EqualTo(3));
            Assert.That(buffer, Is.EqualTo(new byte[] { 9, 8, 7 }));
        });
    }

    [Test]
    public void OpenReadAsyncHonorsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var reader = new MacDuplicateCandidateReader();

        Assert.That(
            async () => await reader.OpenReadAsync(
                Path.Combine(_temporaryDirectory, "file.bin"),
                cancellation.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    private static void RequireMacOs()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Ignore("macOS-specific duplicate candidate metadata.");
        }
    }

    private sealed class FixedCloudFileStatusReader(
        DuplicateContentAvailability? availability)
        : IMacCloudFileStatusReader
    {
        public DuplicateContentAvailability? GetContentAvailability(string path) =>
            availability;
    }

    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int link(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string existingPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string newPath);
}
