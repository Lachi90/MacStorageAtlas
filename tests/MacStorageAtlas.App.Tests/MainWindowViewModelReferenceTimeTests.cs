using System.Runtime.CompilerServices;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core.Filtering;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.App.Tests;

public class MainWindowViewModelReferenceTimeTests
{
    private static readonly DateTimeOffset Reference =
        new(2026, 7, 30, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task ARelativePresetMatchesMoreFilesAsTheClockAdvances()
    {
        var now = Reference;
        var viewModel = await CreateScannedViewModelAsync(() => now);
        var preset = viewModel.Filter.Presets.Single(
            candidate => candidate.Name == BuiltInFilterPresets.NotModifiedForOneYearName);

        viewModel.Filter.ApplyPresetCommand.Execute(preset);
        await viewModel.TreePreparation;
        var beforeAdvance = viewModel.Filter.MatchCount;

        now = Reference.AddYears(2);
        viewModel.Filter.ClearFilterCommand.Execute(null);
        await viewModel.TreePreparation;
        viewModel.Filter.ApplyPresetCommand.Execute(preset);
        await viewModel.TreePreparation;

        Assert.Multiple(() =>
        {
            Assert.That(beforeAdvance, Is.EqualTo(1));
            Assert.That(viewModel.Filter.MatchCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task OneFilterApplicationReadsTheClockOnce()
    {
        var reads = 0;
        var viewModel = await CreateScannedViewModelAsync(() =>
        {
            reads++;
            return Reference;
        });

        viewModel.Filter.ModifiedBefore.IsRelative = true;
        viewModel.Filter.ModifiedBefore.Count = 400;
        viewModel.Filter.ModifiedBefore.Unit = RelativeDateUnit.Days;
        await viewModel.TreePreparation;

        reads = 0;
        viewModel.Filter.ModifiedBefore.Count = 300;
        await viewModel.TreePreparation;

        Assert.That(reads, Is.EqualTo(1));
    }

    [Test]
    public async Task EveryBoundInOneApplicationUsesTheReportedReferenceTime()
    {
        var viewModel = await CreateScannedViewModelAsync(() => Reference);

        viewModel.Filter.ModifiedAfter.IsRelative = true;
        viewModel.Filter.ModifiedAfter.Count = 400;
        viewModel.Filter.ModifiedAfter.Unit = RelativeDateUnit.Days;
        viewModel.Filter.ModifiedBefore.IsRelative = true;
        viewModel.Filter.ModifiedBefore.Count = 400;
        viewModel.Filter.ModifiedBefore.Unit = RelativeDateUnit.Days;
        await viewModel.TreePreparation;

        var evaluated = viewModel.Filter.LastEvaluatedReferenceTime;
        var filter = viewModel.Filter.CurrentFilter;

        Assert.That(evaluated, Is.EqualTo(Reference));
        Assert.Multiple(() =>
        {
            Assert.That(
                filter.ModifiedAfter!.Resolve(evaluated!.Value),
                Is.EqualTo(Reference.AddDays(-400)));
            Assert.That(
                filter.ModifiedBefore!.Resolve(evaluated.Value),
                Is.EqualTo(Reference.AddDays(-400)));
            Assert.That(viewModel.Filter.MatchCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task TheResolvedDescriptionReportsTheEvaluatedReferenceTime()
    {
        var now = Reference;
        var viewModel = await CreateScannedViewModelAsync(() => now);

        viewModel.Filter.ModifiedBefore.IsRelative = true;
        viewModel.Filter.ModifiedBefore.Count = 1;
        viewModel.Filter.ModifiedBefore.Unit = RelativeDateUnit.Years;
        await viewModel.TreePreparation;

        now = Reference.AddYears(5);

        Assert.That(
            viewModel.Filter.ModifiedBefore.ResolvedDescription,
            Does.Contain("2025-07-30"));
    }

    [Test]
    public async Task ARelativeCriterionIsEvaluatedWithoutChangingTheScanRoot()
    {
        var viewModel = await CreateScannedViewModelAsync(() => Reference);
        var root = viewModel.CurrentPath;

        viewModel.Filter.ModifiedBefore.IsRelative = true;
        viewModel.Filter.ModifiedBefore.Count = 6;
        viewModel.Filter.ModifiedBefore.Unit = RelativeDateUnit.Months;
        await viewModel.TreePreparation;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.CurrentPath, Is.EqualTo(root));
            Assert.That(viewModel.Filter.MatchCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task AnInvalidRelativeCountDoesNotProduceAZeroMatchResult()
    {
        var viewModel = await CreateScannedViewModelAsync(() => Reference);

        viewModel.Filter.ModifiedBefore.IsRelative = true;
        viewModel.Filter.ModifiedBefore.Count = 0;
        await viewModel.TreePreparation;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.Filter.HasValidationError, Is.True);
            Assert.That(viewModel.Filter.HasNoMatches, Is.False);
        });
    }

    private static async Task<TestableMainWindowViewModel> CreateScannedViewModelAsync(
        Func<DateTimeOffset> referenceTimeProvider)
    {
        var viewModel = new TestableMainWindowViewModel(
            new StubDiskScanner(CreateTree()),
            referenceTimeProvider)
        {
            SelectedFolderPath = "/Users/test"
        };

        await viewModel.ScanFolderCommand.ExecuteAsync(null);
        return viewModel;
    }

    private static DiskItem CreateTree()
    {
        var root = new DiskItem("root", "/Users/test", isDirectory: true);

        root.AddChild(WithModified(
            new DiskItem("recent.txt", "/Users/test/recent.txt", isDirectory: false)
            {
                SizeBytes = 100
            },
            Reference.AddDays(-30)));

        root.AddChild(WithModified(
            new DiskItem("ancient.txt", "/Users/test/ancient.txt", isDirectory: false)
            {
                SizeBytes = 200
            },
            Reference.AddDays(-400)));

        root.SizeBytes = 300;
        return root;
    }

    private static DiskItem WithModified(DiskItem item, DateTimeOffset modified)
    {
        item.Metadata = item.Metadata with { ModifiedTimeUtc = modified };
        return item;
    }

    private sealed class TestableMainWindowViewModel : MainWindowViewModel
    {
        public TestableMainWindowViewModel(
            IDiskScanner scanner,
            Func<DateTimeOffset> referenceTimeProvider)
            : base(
                new NullFolderPickerService(),
                scanner,
                new ImmediateDispatcher(),
                searchDebounceInterval: TimeSpan.Zero,
                referenceTimeProvider: referenceTimeProvider)
        {
        }
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
                FilesScanned: 2,
                DirectoriesScanned: 1,
                BytesScanned: _root.SizeBytes,
                Root: _root,
                Errors: [],
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
}
