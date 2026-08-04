using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.Tests;

public class MainWindowViewModelExportTests
{
    private static readonly DateTimeOffset Reference =
        new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    private string _directory = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            $"MacStorageAtlasExport-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public async Task ExportIsUnavailableUntilAScanCompletes()
    {
        var viewModel = CreateViewModel(new RecordingSaveFilePicker(null));

        var beforeScan = viewModel.ExportCsvCommand.CanExecute(null);
        await ScanAsync(viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(beforeScan, Is.False);
            Assert.That(viewModel.ExportCsvCommand.CanExecute(null), Is.True);
            Assert.That(viewModel.ExportJsonCommand.CanExecute(null), Is.True);
        });
    }

    [Test]
    public async Task DismissingThePickerWritesNothingAndReportsNoFailure()
    {
        var picker = new RecordingSaveFilePicker(null);
        var viewModel = CreateViewModel(picker);
        await ScanAsync(viewModel);

        await viewModel.ExportCsvCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(picker.RequestCount, Is.EqualTo(1));
            Assert.That(viewModel.ExportStatusMessage, Is.Null);
            Assert.That(viewModel.IsExporting, Is.False);
            Assert.That(Directory.GetFiles(_directory), Is.Empty);
        });
    }

    [Test]
    public async Task ExportingAsCsvWritesEveryItemAndReportsCompletion()
    {
        var destination = Path.Combine(_directory, "result.csv");
        var viewModel = CreateViewModel(new RecordingSaveFilePicker(destination));
        await ScanAsync(viewModel);

        await viewModel.ExportCsvCommand.ExecuteAsync(null);

        var lines = File.ReadAllLines(destination);

        Assert.Multiple(() =>
        {
            Assert.That(lines[0], Does.EndWith("Path,Name,Kind,Depth,MeasurementMode,"
                + "MeasuredSizeBytes,CountedSizeBytes,SharedSizeBytes,IsSharedStorage,"
                + "Extension,Category,CreatedUtc,ModifiedUtc,LastAccessedUtc"));
            Assert.That(lines, Has.Length.EqualTo(6));
            Assert.That(
                viewModel.ExportStatusMessage,
                Is.EqualTo("Exported 5 items to result.csv."));
            Assert.That(Directory.GetFiles(_directory), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public async Task ACsvExportStartsWithAByteOrderMark()
    {
        var destination = Path.Combine(_directory, "result.csv");
        var viewModel = CreateViewModel(new RecordingSaveFilePicker(destination));
        await ScanAsync(viewModel);

        await viewModel.ExportCsvCommand.ExecuteAsync(null);

        var bytes = File.ReadAllBytes(destination);

        Assert.That(bytes.Take(3).ToArray(), Is.EqualTo(new byte[] { 0xEF, 0xBB, 0xBF }));
    }

    [Test]
    public async Task ExportingAsJsonWritesTheEnvelopeAndItems()
    {
        var destination = Path.Combine(_directory, "result.json");
        var viewModel = CreateViewModel(new RecordingSaveFilePicker(destination));
        await ScanAsync(viewModel);

        await viewModel.ExportJsonCommand.ExecuteAsync(null);

        using var document = JsonDocument.Parse(File.ReadAllText(destination));

        Assert.Multiple(() =>
        {
            Assert.That(
                document.RootElement.GetProperty("schemaVersion").GetInt32(),
                Is.EqualTo(1));
            Assert.That(
                document.RootElement.GetProperty("scan").GetProperty("completedAt").GetString(),
                Is.EqualTo("2026-07-30T12:00:00.0000000Z"));
            Assert.That(
                document.RootElement.GetProperty("scan").GetProperty("scope").GetString(),
                Is.EqualTo("Full"));
            Assert.That(document.RootElement.GetProperty("items").GetArrayLength(), Is.EqualTo(5));
        });
    }

    [Test]
    public async Task TheSuggestedFileNameNamesTheScannedFolderAndFormat()
    {
        var picker = new RecordingSaveFilePicker(null);
        var viewModel = CreateViewModel(picker);
        await ScanAsync(viewModel);

        await viewModel.ExportCsvCommand.ExecuteAsync(null);
        var csvName = picker.LastSuggestedFileName;
        await viewModel.ExportJsonCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(csvName, Does.StartWith("MacStorageAtlas-test-").And.EndWith(".csv"));
            Assert.That(
                picker.LastSuggestedFileName,
                Does.StartWith("MacStorageAtlas-test-").And.EndWith(".json"));
        });
    }

    [Test]
    public async Task TheExportScopeFollowsTheActiveFilter()
    {
        var destination = Path.Combine(_directory, "filtered.json");
        var viewModel = CreateViewModel(new RecordingSaveFilePicker(destination));
        await ScanAsync(viewModel);

        viewModel.Filter.MinimumSizeBytes = 250;
        await viewModel.TreePreparation;

        await viewModel.ExportJsonCommand.ExecuteAsync(null);

        using var document = JsonDocument.Parse(File.ReadAllText(destination));
        var scan = document.RootElement.GetProperty("scan");
        var items = document.RootElement.GetProperty("items");

        Assert.Multiple(() =>
        {
            Assert.That(scan.GetProperty("scope").GetString(), Is.EqualTo("Filtered"));
            Assert.That(
                scan.GetProperty("filter").GetProperty("minimumSizeBytes").GetInt64(),
                Is.EqualTo(250));
            Assert.That(items.GetArrayLength(), Is.EqualTo(1));
            Assert.That(
                items[0].GetProperty("path").GetString(),
                Is.EqualTo("/Users/test/docs/big.txt"));
            Assert.That(scan.GetProperty("itemCount").GetInt64(), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task TheCompletionMessageReportsUnreadablePaths()
    {
        var destination = Path.Combine(_directory, "result.csv");
        var errors = new[]
        {
            new ScanError("/Users/test/private", "Denied.", "UnauthorizedAccessException"),
            new ScanError("/Users/test/other", "Denied.", "UnauthorizedAccessException")
        };
        var viewModel = CreateViewModel(new RecordingSaveFilePicker(destination), errors);
        await ScanAsync(viewModel);

        await viewModel.ExportCsvCommand.ExecuteAsync(null);

        Assert.That(
            viewModel.ExportStatusMessage,
            Is.EqualTo(
                "Exported 5 items to result.csv. 2 paths could not be read during the scan, "
                + "so the export does not describe them."));
    }

    [Test]
    public async Task AJsonExportCarriesTheRecoverableScanErrors()
    {
        var destination = Path.Combine(_directory, "result.json");
        var errors = new[]
        {
            new ScanError("/Users/test/private", "Denied.", "UnauthorizedAccessException")
        };
        var viewModel = CreateViewModel(new RecordingSaveFilePicker(destination), errors);
        await ScanAsync(viewModel);

        await viewModel.ExportJsonCommand.ExecuteAsync(null);

        using var document = JsonDocument.Parse(File.ReadAllText(destination));

        Assert.That(
            document.RootElement.GetProperty("errors")[0].GetProperty("path").GetString(),
            Is.EqualTo("/Users/test/private"));
    }

    [Test]
    public async Task AWriteFailureLeavesNoFileAndReportsTheFailure()
    {
        var destination = Path.Combine(_directory, "missing", "result.csv");
        var viewModel = CreateViewModel(new RecordingSaveFilePicker(destination));
        await ScanAsync(viewModel);

        await viewModel.ExportCsvCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(destination), Is.False);
            Assert.That(
                viewModel.ExportStatusMessage,
                Does.StartWith("The export failed and no file was written:"));
            Assert.That(viewModel.IsExporting, Is.False);
        });
    }

    [Test]
    public async Task AFailedPublishLeavesNoTemporaryFileBehind()
    {
        var destination = Path.Combine(_directory, "occupied");
        Directory.CreateDirectory(destination);
        var viewModel = CreateViewModel(new RecordingSaveFilePicker(destination));
        await ScanAsync(viewModel);

        await viewModel.ExportCsvCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(Directory.GetFiles(_directory), Is.Empty);
            Assert.That(Directory.Exists(destination), Is.True);
            Assert.That(
                viewModel.ExportStatusMessage,
                Does.StartWith("The export failed and no file was written:"));
        });
    }

    [Test]
    public async Task ASuccessfulExportReplacesAnExistingFile()
    {
        var destination = Path.Combine(_directory, "result.csv");
        await File.WriteAllTextAsync(destination, "previous export");
        var viewModel = CreateViewModel(new RecordingSaveFilePicker(destination));
        await ScanAsync(viewModel);

        await viewModel.ExportCsvCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllText(destination), Does.Contain("Path,Name,Kind,Depth"));
            Assert.That(Directory.GetFiles(_directory), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public async Task CancellingAnExportWritesNoFileAndReportsCancellation()
    {
        var destination = Path.Combine(_directory, "result.csv");
        var dispatcher = new HookableDispatcher();
        var viewModel = CreateViewModel(
            new RecordingSaveFilePicker(destination),
            dispatcher: dispatcher);
        await ScanAsync(viewModel);

        dispatcher.AfterInvoke = () => viewModel.CancelExportCommand.Execute(null);
        await viewModel.ExportCsvCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(destination), Is.False);
            Assert.That(Directory.GetFiles(_directory), Is.Empty);
            Assert.That(
                viewModel.ExportStatusMessage,
                Is.EqualTo(
                    "The export was cancelled. No file was written to the chosen location."));
            Assert.That(viewModel.IsExporting, Is.False);
        });
    }

    [Test]
    public async Task AnExistingFileSurvivesACancelledExport()
    {
        var destination = Path.Combine(_directory, "result.csv");
        await File.WriteAllTextAsync(destination, "previous export");
        var dispatcher = new HookableDispatcher();
        var viewModel = CreateViewModel(
            new RecordingSaveFilePicker(destination),
            dispatcher: dispatcher);
        await ScanAsync(viewModel);

        dispatcher.AfterInvoke = () => viewModel.CancelExportCommand.Execute(null);
        await viewModel.ExportCsvCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(File.ReadAllText(destination), Is.EqualTo("previous export"));
            Assert.That(Directory.GetFiles(_directory), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public async Task ExportIsUnavailableWhileExportingAndCancellationIsAvailable()
    {
        var viewModel = CreateViewModel(new RecordingSaveFilePicker(null));
        await ScanAsync(viewModel);

        viewModel.IsExporting = true;
        var canExportDuringExport = viewModel.ExportCsvCommand.CanExecute(null);
        var canCancelDuringExport = viewModel.CancelExportCommand.CanExecute(null);
        viewModel.IsExporting = false;

        Assert.Multiple(() =>
        {
            Assert.That(canExportDuringExport, Is.False);
            Assert.That(canCancelDuringExport, Is.True);
            Assert.That(viewModel.ExportCsvCommand.CanExecute(null), Is.True);
            Assert.That(viewModel.CancelExportCommand.CanExecute(null), Is.False);
        });
    }

    [Test]
    public async Task ExportIsUnavailableWhileScanning()
    {
        var viewModel = CreateViewModel(new RecordingSaveFilePicker(null));
        await ScanAsync(viewModel);

        viewModel.IsScanning = true;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.ExportCsvCommand.CanExecute(null), Is.False);
            Assert.That(viewModel.ExportJsonCommand.CanExecute(null), Is.False);
        });
    }

    private static async Task ScanAsync(MainWindowViewModel viewModel)
    {
        viewModel.SelectedFolderPath = "/Users/test";
        await viewModel.ScanFolderCommand.ExecuteAsync(null);
        await viewModel.TreePreparation;
    }

    private static MainWindowViewModel CreateViewModel(
        ISaveFilePickerService picker,
        IReadOnlyList<ScanError>? errors = null,
        IUiDispatcher? dispatcher = null) =>
        new(
            new NullFolderPickerService(),
            new StubDiskScanner(CreateTree(), errors ?? []),
            dispatcher ?? new ImmediateDispatcher(),
            saveFilePickerService: picker,
            searchDebounceInterval: TimeSpan.Zero,
            referenceTimeProvider: () => Reference);

    private static DiskItem CreateTree()
    {
        var root = new DiskItem("test", "/Users/test", isDirectory: true) { SizeBytes = 600 };

        var docs = new DiskItem("docs", "/Users/test/docs", isDirectory: true)
        {
            SizeBytes = 500
        };
        docs.AddChild(new DiskItem("big.txt", "/Users/test/docs/big.txt", isDirectory: false)
        {
            SizeBytes = 300
        });
        docs.AddChild(new DiskItem("mid.txt", "/Users/test/docs/mid.txt", isDirectory: false)
        {
            SizeBytes = 200
        });

        root.AddChild(docs);
        root.AddChild(new DiskItem("small.txt", "/Users/test/small.txt", isDirectory: false)
        {
            SizeBytes = 100
        });

        return root;
    }

    private sealed class RecordingSaveFilePicker : ISaveFilePickerService
    {
        private readonly string? _destination;

        public RecordingSaveFilePicker(string? destination)
        {
            _destination = destination;
        }

        public int RequestCount { get; private set; }

        public string? LastSuggestedFileName { get; private set; }

        public Task<string?> SelectSaveFileAsync(
            ScanExportFormat format,
            string suggestedFileName)
        {
            RequestCount++;
            LastSuggestedFileName = suggestedFileName;
            return Task.FromResult(_destination);
        }
    }

    private sealed class StubDiskScanner : IDiskScanner
    {
        private readonly DiskItem _root;
        private readonly IReadOnlyList<ScanError> _errors;

        public StubDiskScanner(DiskItem root, IReadOnlyList<ScanError> errors)
        {
            _root = root;
            _errors = errors;
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
                FilesScanned: 3,
                DirectoriesScanned: 2,
                BytesScanned: _root.SizeBytes,
                Root: _root,
                Errors: _errors,
                IsCompleted: true,
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

    private sealed class HookableDispatcher : IUiDispatcher
    {
        public Action? AfterInvoke { get; set; }

        public Task InvokeAsync(Action action)
        {
            action();
            AfterInvoke?.Invoke();
            return Task.CompletedTask;
        }
    }
}
