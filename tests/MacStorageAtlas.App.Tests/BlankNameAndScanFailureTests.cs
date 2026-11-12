using System.Runtime.CompilerServices;
using MacStorageAtlas.App.Converters;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core.Export;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.App.Tests;

public class BlankNameAndScanFailureTests
{
    [TestCase("report.pdf", "report.pdf")]
    [TestCase(" leading space", " leading space")]
    [TestCase(" ", "⟨blank name: U+0020⟩")]
    [TestCase(" ​", "⟨blank name: U+00A0 U+200B⟩")]
    [TestCase("\t", "⟨blank name: U+0009⟩")]
    [TestCase("", DisplayNameConverter.EmptyNamePlaceholder)]
    public void DisplayShowsPlaceholderOnlyForBlankNames(string name, string expected)
    {
        Assert.That(DisplayNameConverter.Display(name), Is.EqualTo(expected));
    }

    [Test]
    public void DisplayShowsEmptyPlaceholderForNull()
    {
        Assert.That(
            DisplayNameConverter.Instance.Convert(null, typeof(string), null, System.Globalization.CultureInfo.InvariantCulture),
            Is.EqualTo(DisplayNameConverter.EmptyNamePlaceholder));
    }

    [Test]
    public void TreeNodeAndExportKeepTheRealBlankName()
    {
        var root = new DiskItem("root", "/root", isDirectory: true);
        var blank = new DiskItem(" ", "/root/ ", isDirectory: true);
        root.AddChild(blank);

        var node = new DiskItemTreeNodeViewModel(blank);
        var rows = ScanExportRowSource
            .EnumerateFull(root, StorageMeasurementMode.Logical)
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(node.Name, Is.EqualTo(" "));
            Assert.That(rows.Single(row => row.Path == "/root/ ").Name, Is.EqualTo(" "));
        });
    }

    [Test]
    public async Task UnexpectedScanFailureStopsScanningAndReportsIt()
    {
        var viewModel = CreateViewModel(new FailingDiskScanner());

        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsScanning, Is.False);
            Assert.That(viewModel.ScanCompletedAt, Is.Null);
            Assert.That(viewModel.TreeItems, Is.Empty);
            Assert.That(
                viewModel.ScanFailureMessage,
                Is.EqualTo("The scan stopped unexpectedly. Simulated scanner failure."));
        });
    }

    [Test]
    public async Task CancelledScanDoesNotReportAFailure()
    {
        var viewModel = CreateViewModel(new CancellingDiskScanner());

        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.That(viewModel.ScanFailureMessage, Is.Null);
    }

    [Test]
    public async Task StartingANewScanClearsTheFailureMessage()
    {
        var scanner = new FailingDiskScanner();
        var viewModel = CreateViewModel(scanner);
        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        scanner.ShouldFail = false;
        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ScanFailureMessage, Is.Null);
            Assert.That(viewModel.ScanCompletedAt, Is.Not.Null);
        });
    }

    private static MainWindowViewModel CreateViewModel(IDiskScanner scanner) =>
        new(
            new NullFolderPickerService(),
            scanner,
            new ImmediateDispatcher(),
            searchDebounceInterval: TimeSpan.Zero)
        {
            SelectedFolderPath = "/scan"
        };

    private static ScanProgress Progress(bool isCompleted) =>
        new(
            "/scan",
            FilesScanned: 0,
            DirectoriesScanned: 1,
            BytesScanned: 0,
            new DiskItem("scan", "/scan", isDirectory: true),
            Errors: [],
            IsCompleted: isCompleted);

    private sealed class FailingDiskScanner : IDiskScanner
    {
        public bool ShouldFail { get; set; } = true;

        public async IAsyncEnumerable<ScanProgress> ScanAsync(
            string rootPath,
            ScanOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return Progress(isCompleted: !ShouldFail);

            if (ShouldFail)
            {
                throw new InvalidOperationException("Simulated scanner failure.");
            }
        }
    }

    private sealed class CancellingDiskScanner : IDiskScanner
    {
        public async IAsyncEnumerable<ScanProgress> ScanAsync(
            string rootPath,
            ScanOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            yield return Progress(isCompleted: false);
            throw new OperationCanceledException(cancellationToken);
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
