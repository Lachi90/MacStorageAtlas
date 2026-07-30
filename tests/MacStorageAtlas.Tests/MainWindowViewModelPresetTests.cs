using System.Runtime.CompilerServices;
using MacStorageAtlas.App.Models;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core;
using NSubstitute;

namespace MacStorageAtlas.Tests;

public class MainWindowViewModelPresetTests
{
    [Test]
    public void ASavedPresetSurvivesARestart()
    {
        var settingsService = new RecordingSettingsService();

        var first = CreateViewModel(settingsService);
        first.Filter.MinimumSizeBytes = 4096;
        first.Filter.NewPresetName = "Big files";
        first.Filter.SavePresetCommand.Execute(null);

        var second = CreateViewModel(settingsService);
        var restored = second.Filter.Presets.SingleOrDefault(
            preset => preset.Name == "Big files");

        Assert.Multiple(() =>
        {
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored!.Filter.MinimumSizeBytes, Is.EqualTo(4096));
            Assert.That(restored.IsBuiltIn, Is.False);
        });
    }

    [Test]
    public void ApplyingARestoredPresetRestoresTheSavedCriteria()
    {
        var settingsService = new RecordingSettingsService();
        var first = CreateViewModel(settingsService);
        first.Filter.ExtensionsText = ".mov";
        first.Filter.MinimumSizeBytes = 2048;
        first.Filter.NewPresetName = "Big videos";
        first.Filter.SavePresetCommand.Execute(null);

        var second = CreateViewModel(settingsService);
        var restored = second.Filter.Presets.Single(preset => preset.Name == "Big videos");
        second.Filter.ApplyPresetCommand.Execute(restored);

        Assert.Multiple(() =>
        {
            Assert.That(second.Filter.MinimumSizeBytes, Is.EqualTo(2048));
            Assert.That(second.Filter.ExtensionsText, Does.Contain(".mov"));
        });
    }

    [Test]
    public void ADeletedPresetIsNotRestored()
    {
        var settingsService = new RecordingSettingsService();
        var first = CreateViewModel(settingsService);
        first.Filter.MinimumSizeBytes = 4096;
        first.Filter.NewPresetName = "Temporary";
        first.Filter.SavePresetCommand.Execute(null);

        var saved = first.Filter.Presets.Single(preset => preset.Name == "Temporary");
        first.Filter.DeletePresetCommand.Execute(saved);

        var second = CreateViewModel(settingsService);

        Assert.That(
            second.Filter.Presets.Any(preset => preset.Name == "Temporary"),
            Is.False);
    }

    [Test]
    public void ABuiltInPresetCannotBeDeleted()
    {
        var viewModel = CreateViewModel(new RecordingSettingsService());
        var builtIn = viewModel.Filter.Presets.First(preset => preset.IsBuiltIn);

        viewModel.Filter.DeletePresetCommand.Execute(builtIn);

        Assert.That(
            viewModel.Filter.Presets.Any(preset => preset.Name == builtIn.Name),
            Is.True);
    }

    [Test]
    public void APresetCanBeRenamed()
    {
        var settingsService = new RecordingSettingsService();
        var first = CreateViewModel(settingsService);
        first.Filter.MinimumSizeBytes = 4096;
        first.Filter.NewPresetName = "Original";
        first.Filter.SavePresetCommand.Execute(null);
        var saved = first.Filter.Presets.Single(preset => preset.Name == "Original");

        first.Filter.RenamePreset(saved, "Renamed");
        var second = CreateViewModel(settingsService);

        Assert.Multiple(() =>
        {
            Assert.That(second.Filter.Presets.Any(preset => preset.Name == "Renamed"), Is.True);
            Assert.That(second.Filter.Presets.Any(preset => preset.Name == "Original"), Is.False);
        });
    }

    [Test]
    public void AnInactiveFilterIsNotSavedAsAPreset()
    {
        var viewModel = CreateViewModel(new RecordingSettingsService());
        viewModel.Filter.NewPresetName = "Empty";

        viewModel.Filter.SavePresetCommand.Execute(null);

        Assert.That(viewModel.Filter.UserPresets, Is.Empty);
    }

    [Test]
    public void AnInvalidFilterIsNotSavedAsAPreset()
    {
        var viewModel = CreateViewModel(new RecordingSettingsService());
        viewModel.Filter.MinimumSizeBytes = 4096;
        viewModel.Filter.MaximumSizeBytes = 1024;
        viewModel.Filter.NewPresetName = "Contradictory";

        viewModel.Filter.SavePresetCommand.Execute(null);

        Assert.That(viewModel.Filter.UserPresets, Is.Empty);
    }

    [Test]
    public void SavingAPresetDoesNotDiscardOtherSettings()
    {
        var settingsService = new RecordingSettingsService();
        var first = CreateViewModel(settingsService);
        first.IncludeHiddenFiles = true;
        first.Filter.MinimumSizeBytes = 4096;
        first.Filter.NewPresetName = "Big files";
        first.Filter.SavePresetCommand.Execute(null);

        var second = CreateViewModel(settingsService);

        Assert.Multiple(() =>
        {
            Assert.That(second.IncludeHiddenFiles, Is.True);
            Assert.That(
                second.Filter.Presets.Any(preset => preset.Name == "Big files"),
                Is.True);
        });
    }

    [Test]
    public void EveryBuiltInPresetNameStatesAFactRatherThanARecommendation()
    {
        var viewModel = CreateViewModel(new RecordingSettingsService());
        string[] forbidden =
        [
            "safe", "delete", "remove", "junk", "clean", "useless", "unwanted", "trash"
        ];

        var names = viewModel.Filter.Presets
            .Where(preset => preset.IsBuiltIn)
            .Select(preset => preset.Name)
            .ToArray();

        Assert.That(
            names.Any(name => forbidden.Any(
                word => name.Contains(word, StringComparison.OrdinalIgnoreCase))),
            Is.False,
            $"Preset names must not imply an action: {string.Join(", ", names)}");
    }

    [Test]
    public async Task TrashConfirmationIsStillRequiredWithAFilterActive()
    {
        var root = CreateTree();
        var confirmation = Substitute.For<ITrashConfirmationService>();
        confirmation.ConfirmMoveToTrashAsync(Arg.Any<DiskItem>()).Returns(false);
        var trashService = Substitute.For<ITrashService>();

        var viewModel = new MainWindowViewModel(
            new NullFolderPickerService(),
            new SingleResultScanner(root),
            new ImmediateUiDispatcher(),
            trashService: trashService,
            trashConfirmationService: confirmation,
            settingsService: new RecordingSettingsService(),
            searchDebounceInterval: TimeSpan.Zero)
        {
            SelectedFolderPath = "/Users/test"
        };
        await viewModel.ScanFolderCommand.ExecuteAsync(null);

        viewModel.Filter.ExtensionsText = ".mov";
        await viewModel.TreePreparation;
        viewModel.SelectedLargeFile = viewModel.LargeFiles.Single();
        await viewModel.MoveToTrashCommand.ExecuteAsync(null);

        await confirmation.Received(1).ConfirmMoveToTrashAsync(Arg.Any<DiskItem>());
        await trashService.DidNotReceive().MoveToTrashAsync(Arg.Any<string>());
    }

    private static MainWindowViewModel CreateViewModel(ISettingsService settingsService) =>
        new(
            new NullFolderPickerService(),
            Substitute.For<IDiskScanner>(),
            new ImmediateUiDispatcher(),
            settingsService: settingsService,
            searchDebounceInterval: TimeSpan.Zero);

    private static DiskItem CreateTree()
    {
        var root = new DiskItem("root", "/Users/test", isDirectory: true);
        root.AddChild(new DiskItem("big.mov", "/Users/test/big.mov", isDirectory: false)
        {
            SizeBytes = 8192
        });
        root.AddChild(new DiskItem("notes.txt", "/Users/test/notes.txt", isDirectory: false)
        {
            SizeBytes = 128
        });
        root.SizeBytes = 8320;
        return root;
    }

    private sealed class RecordingSettingsService : ISettingsService
    {
        private AppSettings _settings = new();

        public AppSettings Load() => _settings;

        public void Save(AppSettings settings) => _settings = settings;
    }

    private sealed class ImmediateUiDispatcher : IUiDispatcher
    {
        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
    }

    private sealed class SingleResultScanner : IDiskScanner
    {
        private readonly DiskItem _root;

        public SingleResultScanner(DiskItem root)
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
                FilesScanned: 2,
                DirectoriesScanned: 1,
                BytesScanned: _root.SizeBytes,
                Root: _root,
                Errors: [],
                IsCompleted: true,
                MeasurementMode: (options ?? ScanOptions.Default).MeasurementMode);
        }
    }
}
