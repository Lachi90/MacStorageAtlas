using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using MacStorageAtlas.Benchmarks;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.Tests;

public class BenchmarkToolTests
{
    private string _temporaryDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"MacStorageAtlas-Benchmarks-{Guid.NewGuid():N}");
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
    public void ScanBenchmarkResultSerializesRequiredMetricFields()
    {
        var fixture = Fixture();
        var result = new ScanBenchmarkResult(
            IsCompleted: true,
            IsCanceled: false,
            CurrentPath: "/scan/root",
            ObservedFileCount: 3,
            ObservedDirectoryCount: 2,
            ObservedByteTotal: 4096,
            ProgressUpdateCount: 4,
            ErrorCount: 1,
            DurationMilliseconds: 12.5,
            EntriesPerSecond: 400,
            BytesPerSecond: 8192,
            PeakManagedMemoryBytes: 123456,
            StorageMeasurementMode.SharedAwareAllocated,
            IncludeHiddenFiles: true,
            FollowSymbolicLinks: false,
            TreatPackagesAsDirectories: true,
            CloneAccountingCoverage.Partial,
            fixture,
            RuntimeVersion: ".NET 10",
            ProcessArchitecture: "Arm64",
            OperatingSystemDescription: "macOS",
            Timestamp: DateTimeOffset.Parse("2026-07-29T00:00:00Z"));

        var json = JsonSerializer.Serialize(result, BenchmarkJson.Options);
        var roundTrip = JsonSerializer.Deserialize<ScanBenchmarkResult>(
            json,
            BenchmarkJson.Options);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("durationMilliseconds"));
            Assert.That(json, Does.Contain("progressUpdateCount"));
            Assert.That(json, Does.Contain("peakManagedMemoryBytes"));
            Assert.That(json, Does.Contain("sharedAwareAllocated"));
            Assert.That(roundTrip, Is.Not.Null);
            Assert.That(roundTrip!.ObservedFileCount, Is.EqualTo(3));
            Assert.That(roundTrip.Fixture.Kind, Is.EqualTo(BenchmarkFixtureKind.Existing));
        });
    }

    [Test]
    public async Task RunnerReportsCompletedMetricsFromScannerProgress()
    {
        var root = new DiskItem("root", "/scan/root", isDirectory: true)
        {
            SizeBytes = 4096
        };
        var scanner = new StubDiskScanner(
            cancellationToken => CompletedScanAsync(root, cancellationToken));
        var runner = new ScanBenchmarkRunner(scanner);

        var result = await runner.RunAsync(
            root.Path,
            new ScanOptions
            {
                MeasurementMode = StorageMeasurementMode.Logical,
                IncludeHiddenFiles = true
            },
            Fixture());

        Assert.Multiple(() =>
        {
            Assert.That(result.IsCompleted, Is.True);
            Assert.That(result.IsCanceled, Is.False);
            Assert.That(result.ObservedFileCount, Is.EqualTo(2));
            Assert.That(result.ObservedDirectoryCount, Is.EqualTo(1));
            Assert.That(result.ObservedByteTotal, Is.EqualTo(4096));
            Assert.That(result.ProgressUpdateCount, Is.EqualTo(2));
            Assert.That(result.ErrorCount, Is.Zero);
            Assert.That(result.MeasurementMode, Is.EqualTo(StorageMeasurementMode.Logical));
            Assert.That(result.IncludeHiddenFiles, Is.True);
            Assert.That(result.EntriesPerSecond, Is.GreaterThan(0));
        });
    }

    [Test]
    public async Task RunnerReportsRecoverableErrors()
    {
        var scanner = new StubDiskScanner(ErrorScanAsync);
        var runner = new ScanBenchmarkRunner(scanner);

        var result = await runner.RunAsync(
            "/scan/root",
            ScanOptions.Default,
            Fixture());

        Assert.Multiple(() =>
        {
            Assert.That(result.IsCompleted, Is.True);
            Assert.That(result.ErrorCount, Is.EqualTo(1));
            Assert.That(result.ObservedByteTotal, Is.EqualTo(1024));
        });
    }

    [Test]
    public async Task RunnerReportsCancellationWithoutCompletion()
    {
        var scanner = new StubDiskScanner(CancellableScanAsync);
        var runner = new ScanBenchmarkRunner(scanner);

        var result = await runner.RunAsync(
            "/scan/root",
            ScanOptions.Default,
            Fixture(),
            cancelAfterProgressUpdates: 1);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsCompleted, Is.False);
            Assert.That(result.IsCanceled, Is.True);
            Assert.That(result.ObservedFileCount, Is.EqualTo(1));
            Assert.That(result.ObservedByteTotal, Is.EqualTo(512));
        });
    }

    [Test]
    public async Task RepresentativeFixtureCreatesExpectedShapeUnderRequestedRoot()
    {
        var root = Path.Combine(_temporaryDirectory, "fixture");

        var fixture = await RepresentativeFixtureGenerator.CreateAsync(root);

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(Path.Combine(root, "ordinary", "small.txt")), Is.True);
            Assert.That(File.Exists(Path.Combine(root, "ordinary", "medium.bin")), Is.True);
            Assert.That(File.Exists(Path.Combine(root, "sparse.bin")), Is.True);
            Assert.That(Directory.Exists(Path.Combine(root, "Example.app")), Is.True);
            Assert.That(fixture.RootPath, Is.EqualTo(root));
            Assert.That(fixture.Kind, Is.EqualTo(BenchmarkFixtureKind.Representative));
            Assert.That(fixture.IsRealFileSystem, Is.True);
            Assert.That(fixture.OrdinaryFileCount, Is.EqualTo(4));
            Assert.That(fixture.SparseFileCount, Is.EqualTo(1));
            Assert.That(fixture.PackageCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SyntheticScannerStreamsProgressWithoutPrebuiltPathList()
    {
        var scanner = new SyntheticDiskScanner(fileCount: 10_000, filesPerDirectory: 1_000);
        var runner = new ScanBenchmarkRunner(scanner);

        var result = await runner.RunAsync(
            scanner.RootPath,
            new ScanOptions { MeasurementMode = StorageMeasurementMode.Logical },
            new BenchmarkFixtureInfo(
                BenchmarkFixtureKind.Synthetic,
                scanner.RootPath,
                "Synthetic test",
                IsRealFileSystem: false,
                OrdinaryFileCount: null,
                SparseFileCount: null,
                HardlinkCount: null,
                SymbolicLinkCount: null,
                PackageCount: null,
                SyntheticFileCount: 10_000,
                Limitations: []));

        Assert.Multiple(() =>
        {
            Assert.That(scanner.PathsMaterializedBeforeScan, Is.Zero);
            Assert.That(result.IsCompleted, Is.True);
            Assert.That(result.ObservedFileCount, Is.EqualTo(10_000));
            Assert.That(result.ProgressUpdateCount, Is.LessThan(10_000));
        });
    }

    [Test]
    public async Task CliRunWritesSyntheticBenchmarkJsonOutput()
    {
        var outputPath = Path.Combine(_temporaryDirectory, "result.json");
        using var output = new StringWriter();
        using var error = new StringWriter();
        var cli = new BenchmarkCli(output, error);

        var exitCode = await cli.RunAsync(
        [
            "run",
            "--fixture",
            "synthetic",
            "--synthetic-files",
            "20",
            "--mode",
            "logical",
            "--output",
            outputPath
        ]);

        var json = await File.ReadAllTextAsync(outputPath);
        var result = JsonSerializer.Deserialize<ScanBenchmarkResult>(
            json,
            BenchmarkJson.Options);
        Assert.Multiple(() =>
        {
            Assert.That(exitCode, Is.Zero);
            Assert.That(error.ToString(), Is.Empty);
            Assert.That(output.ToString(), Does.Contain("completed"));
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Fixture.Kind, Is.EqualTo(BenchmarkFixtureKind.Synthetic));
            Assert.That(result.ObservedFileCount, Is.EqualTo(20));
        });
    }

    private static BenchmarkFixtureInfo Fixture() =>
        new(
            BenchmarkFixtureKind.Existing,
            "/scan/root",
            "Test fixture",
            IsRealFileSystem: true,
            OrdinaryFileCount: 1,
            SparseFileCount: 0,
            HardlinkCount: 0,
            SymbolicLinkCount: 0,
            PackageCount: 0,
            SyntheticFileCount: null,
            Limitations: []);

    private static async IAsyncEnumerable<ScanProgress> CompletedScanAsync(
        DiskItem root,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        yield return new ScanProgress(
            "/scan/root/file.bin",
            FilesScanned: 1,
            DirectoriesScanned: 1,
            BytesScanned: 1024,
            root,
            Errors: [],
            MeasurementMode: StorageMeasurementMode.Logical);
        yield return new ScanProgress(
            root.Path,
            FilesScanned: 2,
            DirectoriesScanned: 1,
            BytesScanned: root.SizeBytes,
            root,
            Errors: [],
            IsCompleted: true,
            MeasurementMode: StorageMeasurementMode.Logical);
    }

    private static async IAsyncEnumerable<ScanProgress> ErrorScanAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        cancellationToken.ThrowIfCancellationRequested();
        var root = new DiskItem("root", "/scan/root", isDirectory: true)
        {
            SizeBytes = 1024
        };
        yield return new ScanProgress(
            root.Path,
            FilesScanned: 1,
            DirectoriesScanned: 1,
            BytesScanned: 1024,
            root,
            Errors:
            [
                new ScanError(
                    "/scan/root/restricted",
                    "Access denied.",
                    nameof(UnauthorizedAccessException))
            ],
            IsCompleted: true);
    }

    private static async IAsyncEnumerable<ScanProgress> CancellableScanAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var root = new DiskItem("root", "/scan/root", isDirectory: true)
        {
            SizeBytes = 512
        };
        yield return new ScanProgress(
            "/scan/root/file.bin",
            FilesScanned: 1,
            DirectoriesScanned: 1,
            BytesScanned: 512,
            root,
            Errors: []);
        await Task.Delay(Timeout.Infinite, cancellationToken);
    }

    private sealed class StubDiskScanner(
        Func<CancellationToken, IAsyncEnumerable<ScanProgress>> scan) : IDiskScanner
    {
        public IAsyncEnumerable<ScanProgress> ScanAsync(
            string rootPath,
            ScanOptions? options = null,
            CancellationToken cancellationToken = default) => scan(cancellationToken);
    }
}
