using System.Runtime.CompilerServices;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.App.Tests;

public class MainWindowViewModelScanCompletionTests
{
    private static readonly DateTimeOffset Reference =
        new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void NoScanHasCompletedBeforeTheFirstScan()
    {
        var viewModel = CreateViewModel(new StubDiskScanner(CreateTree()), () => Reference);

        Assert.That(viewModel.ScanCompletedAt, Is.Null);
    }

    [Test]
    public async Task CompletingAScanStampsTheInjectedReferenceTime()
    {
        var viewModel = CreateViewModel(new StubDiskScanner(CreateTree()), () => Reference);
        viewModel.SelectedFolderPath = "/Users/test";

        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.That(viewModel.ScanCompletedAt, Is.EqualTo(Reference));
    }

    [Test]
    public async Task EachCompletedScanStampsItsOwnCompletionTime()
    {
        var now = Reference;
        var viewModel = CreateViewModel(new StubDiskScanner(CreateTree()), () => now);
        viewModel.SelectedFolderPath = "/Users/test";

        await viewModel.ScanFolderCommand.ExecuteAsync(null);
        var first = viewModel.ScanCompletedAt;

        now = Reference.AddHours(3);
        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(Reference));
            Assert.That(viewModel.ScanCompletedAt, Is.EqualTo(Reference.AddHours(3)));
        });
    }

    [Test]
    public async Task StartingAScanClearsThePreviousCompletionTime()
    {
        var viewModel = CreateViewModel(new StubDiskScanner(CreateTree()), () => Reference);
        viewModel.SelectedFolderPath = "/Users/test";
        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        var incomplete = CreateViewModel(
            new NeverCompletingDiskScanner(CreateTree()),
            () => Reference);
        incomplete.SelectedFolderPath = "/Users/test";
        await incomplete.ScanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ScanCompletedAt, Is.EqualTo(Reference));
            Assert.That(incomplete.ScanCompletedAt, Is.Null);
        });
    }

    private static MainWindowViewModel CreateViewModel(
        IDiskScanner scanner,
        Func<DateTimeOffset> referenceTimeProvider) =>
        new(
            new NullFolderPickerService(),
            scanner,
            new ImmediateDispatcher(),
            searchDebounceInterval: TimeSpan.Zero,
            referenceTimeProvider: referenceTimeProvider);

    private static DiskItem CreateTree()
    {
        var root = new DiskItem("root", "/Users/test", isDirectory: true);
        root.AddChild(new DiskItem("a.txt", "/Users/test/a.txt", isDirectory: false)
        {
            SizeBytes = 100
        });
        root.SizeBytes = 100;
        return root;
    }

    private sealed class StubDiskScanner : IDiskScanner
    {
        private readonly DiskItem _root;

        public StubDiskScanner(DiskItem root)
        {
            _root = root;
        }

        public async IAsyncEnumerable<ScanProgress> ScanAsync(
            string rootPath,
            ScanOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            yield return new ScanProgress(
                rootPath,
                FilesScanned: 1,
                DirectoriesScanned: 1,
                BytesScanned: _root.SizeBytes,
                Root: _root,
                Errors: [],
                IsCompleted: true,
                MeasurementMode: (options ?? ScanOptions.Default).MeasurementMode);
        }
    }

    private sealed class NeverCompletingDiskScanner : IDiskScanner
    {
        private readonly DiskItem _root;

        public NeverCompletingDiskScanner(DiskItem root)
        {
            _root = root;
        }

        public async IAsyncEnumerable<ScanProgress> ScanAsync(
            string rootPath,
            ScanOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();

            yield return new ScanProgress(
                rootPath,
                FilesScanned: 1,
                DirectoriesScanned: 1,
                BytesScanned: _root.SizeBytes,
                Root: _root,
                Errors: [],
                IsCompleted: false,
                MeasurementMode: (options ?? ScanOptions.Default).MeasurementMode);
        }
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
    }
}
