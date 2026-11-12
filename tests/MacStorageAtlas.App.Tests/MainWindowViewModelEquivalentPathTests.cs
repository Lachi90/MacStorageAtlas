using System.Runtime.CompilerServices;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.App.Tests;

public class MainWindowViewModelEquivalentPathTests
{
    private static readonly EquivalentPath Users =
        new("/Users", "/System/Volumes/Data/Users");

    private static readonly EquivalentPath Applications =
        new("/Applications", "/System/Volumes/Data/Applications");

    [Test]
    public void NoOmissionIsShownBeforeTheFirstScan()
    {
        var viewModel = CreateViewModel(new ScriptedDiskScanner());

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasOmittedEquivalentPaths, Is.False);
            Assert.That(viewModel.OmittedEquivalentPathsSummary, Is.Null);
            Assert.That(viewModel.OmittedEquivalentPathsDetails, Is.Null);
        });
    }

    [Test]
    public async Task CompletedScanNamesTheSingleOmittedPathAndWhereItIsShown()
    {
        var scanner = new ScriptedDiskScanner();
        scanner.Enqueue(Completed([Users]));
        var viewModel = CreateViewModel(scanner);

        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasOmittedEquivalentPaths, Is.True);
            Assert.That(
                viewModel.OmittedEquivalentPathsSummary,
                Is.EqualTo("/System/Volumes/Data/Users is shown at /Users"));
            Assert.That(
                viewModel.OmittedEquivalentPathsDetails,
                Is.EqualTo("/System/Volumes/Data/Users is shown at /Users"));
        });
    }

    [Test]
    public async Task CompletedScanSummarizesSeveralOmittedPathsAndListsEachInDetails()
    {
        var scanner = new ScriptedDiskScanner();
        scanner.Enqueue(Completed([Applications, Users]));
        var viewModel = CreateViewModel(scanner);

        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(
                viewModel.OmittedEquivalentPathsSummary,
                Is.EqualTo("2 Data-volume paths are shown at their firmlinked locations"));
            Assert.That(
                viewModel.OmittedEquivalentPathsDetails,
                Is.EqualTo(
                    "/System/Volumes/Data/Applications is shown at /Applications"
                    + Environment.NewLine
                    + "/System/Volumes/Data/Users is shown at /Users"));
        });
    }

    [Test]
    public async Task CancelledScanKeepsTheOmissionsOfItsPartialResult()
    {
        var scanner = new ScriptedDiskScanner();
        scanner.Enqueue(Partial([Users]), cancelAfterward: true);
        var viewModel = CreateViewModel(scanner);

        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ScanCompletedAt, Is.Null);
            Assert.That(viewModel.OmittedEquivalentPaths, Is.EqualTo(new[] { Users }));
            Assert.That(viewModel.HasOmittedEquivalentPaths, Is.True);
        });
    }

    [Test]
    public async Task StartingANewScanClearsPreviousOmissions()
    {
        var scanner = new ScriptedDiskScanner();
        scanner.Enqueue(Completed([Users]));
        scanner.Enqueue(Completed([]));
        var viewModel = CreateViewModel(scanner);

        await viewModel.ScanFolderCommand.ExecuteAsync(null);
        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasOmittedEquivalentPaths, Is.False);
            Assert.That(viewModel.OmittedEquivalentPathsSummary, Is.Null);
        });
    }

    private static MainWindowViewModel CreateViewModel(IDiskScanner scanner) =>
        new(
            new NullFolderPickerService(),
            scanner,
            new ImmediateDispatcher(),
            searchDebounceInterval: TimeSpan.Zero)
        {
            SelectedFolderPath = "/"
        };

    private static ScanProgress Completed(IReadOnlyList<EquivalentPath> omitted) =>
        Progress(omitted, isCompleted: true);

    private static ScanProgress Partial(IReadOnlyList<EquivalentPath> omitted) =>
        Progress(omitted, isCompleted: false);

    private static ScanProgress Progress(
        IReadOnlyList<EquivalentPath> omitted,
        bool isCompleted)
    {
        var root = new DiskItem("/", "/", isDirectory: true);
        return new ScanProgress(
            "/",
            FilesScanned: 0,
            DirectoriesScanned: 1,
            BytesScanned: 0,
            root,
            Errors: [],
            IsCompleted: isCompleted)
        {
            OmittedEquivalentPaths = omitted
        };
    }

    private sealed class ScriptedDiskScanner : IDiskScanner
    {
        private readonly Queue<(ScanProgress Progress, bool CancelAfterward)> _scans = new();

        public void Enqueue(ScanProgress progress, bool cancelAfterward = false) =>
            _scans.Enqueue((progress, cancelAfterward));

        public async IAsyncEnumerable<ScanProgress> ScanAsync(
            string rootPath,
            ScanOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            var (progress, cancelAfterward) = _scans.Dequeue();
            yield return progress;

            if (cancelAfterward)
            {
                throw new OperationCanceledException(cancellationToken);
            }
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
