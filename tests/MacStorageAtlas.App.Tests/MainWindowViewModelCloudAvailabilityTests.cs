using System.Runtime.CompilerServices;
using MacStorageAtlas.App.Converters;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core.Cleanup;
using MacStorageAtlas.Core.Filtering;
using MacStorageAtlas.Core.History;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Platform;
using MacStorageAtlas.Core.Scanning;
using NSubstitute;

namespace MacStorageAtlas.App.Tests;

public class MainWindowViewModelCloudAvailabilityTests
{
    private const string RootPath = "/scan";
    private const string MixedPath = "/scan/Mixed";
    private const string CloudPath = "/scan/Mixed/Cloud";
    private const string UnlistedPath = "/scan/Unlisted";
    private const long Megabyte = 1024 * 1024;

    private static readonly DateTimeOffset Now = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public async Task AnAllocatedResultWithCloudOnlyFilesShowsTheLabeledSummary()
    {
        var context = await ScannedAsync(StorageMeasurementMode.Allocated);

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.HasCloudAvailabilitySummary, Is.True);
            Assert.That(
                context.ViewModel.CloudAvailabilitySummary,
                Is.EqualTo(
                    $"2 files local · 2 files only in the cloud ({FileSizeFormatter.Format(30 * Megabyte)} logical, not on this Mac)"));
            Assert.That(context.ViewModel.CloudAvailabilitySummaryDetails, Does.Contain("not part of the measured sizes"));
            Assert.That(context.ViewModel.BytesScanned, Is.EqualTo(Root(context).SizeBytes));
        });
    }

    [Test]
    public async Task ALogicalResultStatesThatTheTotalIncludesTheCloudOnlySize()
    {
        var context = await ScannedAsync(StorageMeasurementMode.Logical);

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.CloudAvailabilitySummary, Does.Contain("logical, not on this Mac"));
            Assert.That(
                context.ViewModel.CloudAvailabilitySummary,
                Does.EndWith("included in the logical total)"));
            Assert.That(context.ViewModel.CloudAvailabilitySummaryDetails, Does.Contain("includes it"));
        });
    }

    [Test]
    public async Task ALocalOnlyResultShowsNoSummary()
    {
        var context = await ScannedAsync(
            StorageMeasurementMode.Allocated,
            () => Folder(RootPath, File(RootPath + "/a.bin", 10, CloudAvailability.Local)));

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.HasCloudAvailabilitySummary, Is.False);
            Assert.That(context.ViewModel.CloudAvailabilitySummary, Is.Null);
            Assert.That(context.ViewModel.HasUnlistedCloudFolders, Is.False);
        });
    }

    [Test]
    public async Task TheDetailsStateThatAMixedFolderIsPartlyLocalWithItsCloudOnlySize()
    {
        var context = await ScannedAsync(StorageMeasurementMode.Allocated);

        SelectTreeNode(context, MixedPath);

        Assert.That(
            context.ViewModel.SelectedItemAvailability,
            Is.EqualTo(
                $"Partly local · 2 files only in the cloud ({FileSizeFormatter.Format(30 * Megabyte)} logical, not on this Mac)"));
    }

    [Test]
    public async Task TheDetailsStateACloudOnlyFolderAndFile()
    {
        var context = await ScannedAsync(StorageMeasurementMode.Allocated);

        SelectTreeNode(context, CloudPath);
        var folder = context.ViewModel.SelectedItemAvailability;
        SelectTreeNode(context, CloudPath + "/b.mov");
        var file = context.ViewModel.SelectedItemAvailability;

        Assert.Multiple(() =>
        {
            Assert.That(folder, Does.StartWith("Only in the cloud · 1 file only in the cloud ("));
            Assert.That(
                file,
                Is.EqualTo($"Only in the cloud ({FileSizeFormatter.Format(20 * Megabyte)} logical, not on this Mac)"));
        });
    }

    [Test]
    public async Task TheDetailsStateLocalAndUnknownFilesWithoutASize()
    {
        var context = await ScannedAsync(StorageMeasurementMode.Allocated);

        SelectTreeNode(context, RootPath + "/local.txt");
        var local = context.ViewModel.SelectedItemAvailability;
        SelectTreeNode(context, RootPath + "/unknown.txt");
        var unknown = context.ViewModel.SelectedItemAvailability;

        Assert.Multiple(() =>
        {
            Assert.That(local, Is.EqualTo("Local"));
            Assert.That(unknown, Is.EqualTo("Unknown"));
        });
    }

    [Test]
    public async Task AnUnlistedCloudFolderIsReportedAndDescribed()
    {
        var context = await ScannedAsync(StorageMeasurementMode.Allocated, unlisted: [UnlistedPath]);

        SelectTreeNode(context, UnlistedPath);

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.HasUnlistedCloudFolders, Is.True);
            Assert.That(context.ViewModel.UnlistedCloudFoldersSummary, Is.EqualTo("1 cloud folder was not listed"));
            Assert.That(context.ViewModel.UnlistedCloudFoldersDetails, Does.Contain(UnlistedPath));
            Assert.That(context.ViewModel.ScanErrors, Is.Empty);
            Assert.That(
                context.ViewModel.SelectedItemAvailability,
                Is.EqualTo("Only in the cloud · not listed, its contents were not scanned"));
        });
    }

    [Test]
    public async Task AScanWithAnUnlistedCloudFolderIsRecordedAsIncomplete()
    {
        var context = await ScannedAsync(
            StorageMeasurementMode.Allocated,
            unlisted: [UnlistedPath],
            historyEnabled: true);

        Assert.That(
            context.History.Completeness,
            Is.EqualTo(new[] { ScanCompleteness.IncompleteCloudFoldersNotListed }));
    }

    [Test]
    public async Task TheLocalOnlyFilterNarrowsLargestFilesToLocalFiles()
    {
        var context = await ScannedAsync(StorageMeasurementMode.Allocated);

        context.ViewModel.Filter.SelectedCloudAvailabilityOption =
            context.ViewModel.Filter.CloudAvailabilityOptions.Single(
                option => option.Criterion == CloudAvailabilityCriterion.LocalOnly);
        await context.ViewModel.TreePreparation;
        await context.ViewModel.InsightPreparation;

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.Filter.CloudAvailability, Is.EqualTo(CloudAvailabilityCriterion.LocalOnly));
            Assert.That(context.ViewModel.Filter.IsFilterActive, Is.True);
            Assert.That(
                context.ViewModel.LargeFiles.Select(file => file.Name),
                Is.EquivalentTo(new[] { "local.txt", "c.txt" }));
        });
    }

    [Test]
    public async Task TheCloudOnlyFilterCombinesWithASizeCriterion()
    {
        var context = await ScannedAsync(StorageMeasurementMode.Logical);

        context.ViewModel.Filter.CloudAvailability = CloudAvailabilityCriterion.CloudOnly;
        context.ViewModel.Filter.MinimumSizeBytes = 15;
        await context.ViewModel.TreePreparation;
        await context.ViewModel.InsightPreparation;

        Assert.Multiple(() =>
        {
            Assert.That(
                context.ViewModel.Filter.SelectedCloudAvailabilityOption.Criterion,
                Is.EqualTo(CloudAvailabilityCriterion.CloudOnly));
            Assert.That(context.ViewModel.LargeFiles.Select(file => file.Name), Is.EqualTo(new[] { "b.mov" }));
        });
    }

    [Test]
    public void ClearingTheFilterResetsTheAvailabilityCriterion()
    {
        var filter = new ResultFilterViewModel(() => Now)
        {
            CloudAvailability = CloudAvailabilityCriterion.CloudOnly
        };

        filter.ClearFilter();

        Assert.Multiple(() =>
        {
            Assert.That(filter.CloudAvailability, Is.EqualTo(CloudAvailabilityCriterion.Any));
            Assert.That(filter.SelectedCloudAvailabilityOption.DisplayName, Is.EqualTo("Local and cloud-only files"));
        });
    }

    [Test]
    public async Task TrashingACloudOnlyFileUpdatesTheSummary()
    {
        var context = await ScannedAsync(StorageMeasurementMode.Allocated);
        SelectTreeNode(context, CloudPath + "/b.mov");

        await context.ViewModel.MoveToTrashCommand.ExecuteAsync(null);

        Assert.That(
            context.ViewModel.CloudAvailabilitySummary,
            Is.EqualTo(
                $"2 files local · 1 file only in the cloud ({FileSizeFormatter.Format(10 * Megabyte)} logical, not on this Mac)"));
    }

    [Test]
    public async Task RescanningAFolderRecomputesTheSummaryAndUnlistedFolders()
    {
        var context = await ScannedAsync(StorageMeasurementMode.Allocated, unlisted: [UnlistedPath]);
        context.Scanner.SubtreeReplacement = () => Folder(
            MixedPath,
            File(MixedPath + "/c.txt", 1, CloudAvailability.Local),
            File(MixedPath + "/d.txt", 1, CloudAvailability.Local));
        context.Scanner.SubtreeUnlisted = [MixedPath + "/Gone"];
        SelectTreeNode(context, MixedPath);

        await context.ViewModel.RescanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.HasCloudAvailabilitySummary, Is.False);
            Assert.That(Root(context).CloudAvailability, Is.EqualTo(CloudAvailability.PartlyLocal));
            Assert.That(
                context.ViewModel.UnlistedCloudFolders,
                Is.EqualTo(new[] { UnlistedPath, MixedPath + "/Gone" }));
        });
    }

    [Test]
    public async Task AFreshScanClearsThePreviousCloudState()
    {
        var context = await ScannedAsync(StorageMeasurementMode.Allocated, unlisted: [UnlistedPath]);
        context.Scanner.CreateRoot = () => Folder(RootPath, File(RootPath + "/a.bin", 10, CloudAvailability.Local));
        context.Scanner.Unlisted = [];

        await context.ViewModel.ScanFolderCommand.ExecuteAsync(null);

        Assert.Multiple(() =>
        {
            Assert.That(context.ViewModel.HasCloudAvailabilitySummary, Is.False);
            Assert.That(context.ViewModel.HasUnlistedCloudFolders, Is.False);
        });
    }

    [Test]
    public async Task TheMarkerTooltipStatesTheSameAvailabilityAsTheDetails()
    {
        var context = await ScannedAsync(StorageMeasurementMode.Allocated);
        var culture = System.Globalization.CultureInfo.InvariantCulture;

        SelectTreeNode(context, MixedPath);
        var details = context.ViewModel.SelectedItemAvailability;
        var node = new DiskItemTreeNodeViewModel(context.ViewModel.SelectedItem!);
        SelectTreeNode(context, CloudPath + "/b.mov");
        var file = context.ViewModel.SelectedItem!;

        Assert.Multiple(() =>
        {
            Assert.That(node.AvailabilityDescription, Is.EqualTo(details));
            Assert.That(
                DiskItemAvailabilityDescriptionConverter.Instance.Convert(file, typeof(string), null, culture),
                Is.EqualTo(context.ViewModel.SelectedItemAvailability));
        });
    }

    [TestCase(CloudAvailability.CloudOnly, true, false, "Only in the cloud")]
    [TestCase(CloudAvailability.PartlyLocal, false, true, "Partly local")]
    [TestCase(CloudAvailability.Local, false, false, "Local")]
    [TestCase(CloudAvailability.Unknown, false, false, "Unknown")]
    public void TheMarkerAppearsOnlyForCloudOnlyAndPartlyLocalItems(
        CloudAvailability availability,
        bool cloudOnlyMarker,
        bool partlyLocalMarker,
        string label)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;

        Assert.Multiple(() =>
        {
            Assert.That(
                CloudAvailabilityConverter.IsCloudOnly.Convert(availability, typeof(bool), null, culture),
                Is.EqualTo(cloudOnlyMarker));
            Assert.That(
                CloudAvailabilityConverter.IsPartlyLocal.Convert(availability, typeof(bool), null, culture),
                Is.EqualTo(partlyLocalMarker));
            Assert.That(
                CloudAvailabilityConverter.Label.Convert(availability, typeof(string), null, culture),
                Is.EqualTo(label));
        });
    }

    private static async Task<Context> ScannedAsync(
        StorageMeasurementMode mode,
        Func<DiskItem>? createRoot = null,
        IReadOnlyList<string>? unlisted = null,
        bool historyEnabled = false)
    {
        var scanner = new CloudScanner(createRoot ?? CreateTree)
        {
            Unlisted = unlisted ?? []
        };
        var history = new RecordingHistoryStore();
        var confirmation = Substitute.For<ITrashConfirmationService>();
        confirmation.ConfirmMoveToTrashAsync(Arg.Any<DiskItem>()).Returns(true);
        var viewModel = new MainWindowViewModel(
            new NullFolderPickerService(),
            scanner,
            new ImmediateDispatcher(),
            Substitute.For<IFileRevealService>(),
            Substitute.For<ITrashService>(),
            confirmation,
            quickLookService: Substitute.For<IQuickLookService>(),
            scanHistoryStore: history,
            searchDebounceInterval: TimeSpan.Zero,
            referenceTimeProvider: () => Now)
        {
            SelectedFolderPath = RootPath,
            MeasurementMode = mode,
            IncludeHiddenFiles = false,
            FollowSymbolicLinks = false,
            ScanHistoryEnabled = historyEnabled
        };

        await viewModel.ScanFolderCommand.ExecuteAsync(null);
        return new Context(viewModel, scanner, history);
    }

    private static DiskItem CreateTree() =>
        Folder(
            RootPath,
            Folder(
                MixedPath,
                File(MixedPath + "/a.pdf", 5, CloudAvailability.CloudOnly, 10 * Megabyte),
                File(MixedPath + "/c.txt", 1, CloudAvailability.Local),
                Folder(CloudPath, File(CloudPath + "/b.mov", 5, CloudAvailability.CloudOnly, 20 * Megabyte))),
            File(RootPath + "/local.txt", 2, CloudAvailability.Local),
            File(RootPath + "/unknown.txt", 3, CloudAvailability.Unknown),
            UnlistedFolder(UnlistedPath));

    private static DiskItem Folder(string path, params DiskItem[] children)
    {
        var folder = new DiskItem(Path.GetFileName(path) is { Length: > 0 } name ? name : path, path, isDirectory: true);
        foreach (var child in children)
        {
            folder.AddChild(child);
            folder.AddCloudTotals(child.CloudTotals);
        }

        folder.SizeBytes = children.Sum(child => child.SizeBytes);
        return folder;
    }

    private static DiskItem UnlistedFolder(string path)
    {
        var folder = new DiskItem(Path.GetFileName(path), path, isDirectory: true)
        {
            Metadata = DiskItemMetadata.Unknown(DiskItemKind.Directory) with
            {
                CloudAvailability = CloudAvailability.CloudOnly
            }
        };
        folder.SetCloudTotals(CloudAvailabilityTotals.ForUnlistedCloudFolder());
        return folder;
    }

    private static DiskItem File(
        string path,
        long size,
        CloudAvailability availability,
        long cloudOnlyLogicalSizeBytes = 0)
    {
        var file = new DiskItem(Path.GetFileName(path), path, isDirectory: false)
        {
            SizeBytes = availability == CloudAvailability.CloudOnly ? cloudOnlyLogicalSizeBytes / Megabyte : size,
            Metadata = DiskItemMetadata.Unknown(DiskItemKind.File) with
            {
                CloudAvailability = availability
            }
        };
        file.SetCloudTotals(CloudAvailabilityTotals.ForFile(availability, cloudOnlyLogicalSizeBytes));
        return file;
    }

    private static DiskItem Root(Context context) =>
        context.ViewModel.TreeItems.Single().Item;

    private static void SelectTreeNode(Context context, string path)
    {
        var node = context.ViewModel.TreeItems.Single();
        while (!string.Equals(node.Item.Path, path, StringComparison.Ordinal))
        {
            node = node.Children.Single(child =>
                string.Equals(child.Item.Path, path, StringComparison.Ordinal)
                || path.StartsWith(child.Item.Path + "/", StringComparison.Ordinal));
        }

        context.ViewModel.SelectedTreeItem = node;
    }

    private sealed record Context(
        MainWindowViewModel ViewModel,
        CloudScanner Scanner,
        RecordingHistoryStore History);

    private sealed class CloudScanner(Func<DiskItem> createRoot) : IDiskScanner
    {
        public Func<DiskItem> CreateRoot { get; set; } = createRoot;

        public IReadOnlyList<string> Unlisted { get; set; } = [];

        public Func<DiskItem?>? SubtreeReplacement { get; set; }

        public IReadOnlyList<string> SubtreeUnlisted { get; set; } = [];

        public async IAsyncEnumerable<ScanProgress> ScanAsync(
            string rootPath,
            ScanOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            var root = CreateRoot();
            yield return new ScanProgress(
                rootPath,
                FilesScanned: 5,
                DirectoriesScanned: 4,
                BytesScanned: root.SizeBytes,
                root,
                Errors: [],
                IsCompleted: true,
                options?.MeasurementMode ?? StorageMeasurementMode.Logical)
            {
                UnlistedCloudFolders = Unlisted
            };
        }

        public async IAsyncEnumerable<ScanProgress> ScanSubtreeAsync(
            string scanRootPath,
            string subtreePath,
            ScanOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Yield();
            var replacement = SubtreeReplacement?.Invoke();
            var placeholder = new DiskItem(Path.GetFileName(subtreePath), subtreePath, isDirectory: true);
            yield return new ScanProgress(
                subtreePath,
                FilesScanned: 2,
                DirectoriesScanned: 1,
                BytesScanned: replacement?.SizeBytes ?? 0,
                replacement ?? placeholder,
                Errors: [],
                IsCompleted: true,
                options?.MeasurementMode ?? StorageMeasurementMode.Logical)
            {
                IsRootPresent = replacement is not null,
                UnlistedCloudFolders = SubtreeUnlisted
            };
        }
    }

    private sealed class RecordingHistoryStore : IScanHistoryStore
    {
        public string Location => "/tmp/history";

        public List<ScanCompleteness> Completeness { get; } = [];

        public Task<ScanHistoryCaptureResult> CaptureAsync(
            ScanSnapshotRequest request,
            ScanHistoryLimits limits,
            CancellationToken cancellationToken = default)
        {
            Completeness.Add(request.Metadata.Completeness);
            _ = request.Rows.Count();
            return Task.FromResult(ScanHistoryCaptureResult.Captured(
                new ScanSnapshotDescriptor(request.Metadata, 1024),
                []));
        }

        public Task<IReadOnlyList<ScanHistoryEntry>> ListAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ScanHistoryEntry>>([]);

        public Task<long> GetTotalSizeBytesAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0L);

        public Task<ScanSnapshotReadResult<ScanSnapshotDocument>> ReadAsync(
            string snapshotId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                ScanSnapshotReadResult<ScanSnapshotDocument>.Unreadable("none"));

        public Task<bool> DeleteAsync(
            string snapshotId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task ClearAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<ScanSnapshotDescriptor>> ApplyLimitsAsync(
            ScanHistoryLimits limits,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ScanSnapshotDescriptor>>([]);
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
