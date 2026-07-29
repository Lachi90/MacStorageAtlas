using System.Diagnostics;
using System.Runtime.InteropServices;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.Benchmarks;

public sealed class ScanBenchmarkRunner
{
    private readonly IDiskScanner _scanner;

    public ScanBenchmarkRunner(IDiskScanner scanner)
    {
        _scanner = scanner ?? throw new ArgumentNullException(nameof(scanner));
    }

    public async Task<ScanBenchmarkResult> RunAsync(
        string rootPath,
        ScanOptions options,
        BenchmarkFixtureInfo fixture,
        int? cancelAfterProgressUpdates = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(fixture);

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        var stopwatch = Stopwatch.StartNew();
        var progressUpdateCount = 0;
        var peakManagedMemory = GC.GetTotalMemory(forceFullCollection: false);
        var isCanceled = false;
        ScanProgress? lastProgress = null;

        try
        {
            await foreach (var progress in _scanner
                               .ScanAsync(
                                   rootPath,
                                   options,
                                   linkedCancellation.Token)
                               .ConfigureAwait(false))
            {
                progressUpdateCount++;
                lastProgress = progress;
                peakManagedMemory = Math.Max(
                    peakManagedMemory,
                    GC.GetTotalMemory(forceFullCollection: false));

                if (cancelAfterProgressUpdates is not null
                    && progressUpdateCount >= cancelAfterProgressUpdates.Value
                    && !progress.IsCompleted)
                {
                    isCanceled = true;
                    await linkedCancellation.CancelAsync();
                }
            }
        }
        catch (OperationCanceledException)
        {
            isCanceled = true;
        }

        stopwatch.Stop();
        peakManagedMemory = Math.Max(
            peakManagedMemory,
            GC.GetTotalMemory(forceFullCollection: false));

        var elapsedSeconds = Math.Max(stopwatch.Elapsed.TotalSeconds, double.Epsilon);
        var entries = (lastProgress?.FilesScanned ?? 0)
            + (lastProgress?.DirectoriesScanned ?? 0);
        var bytes = lastProgress?.BytesScanned ?? 0;

        return new ScanBenchmarkResult(
            IsCompleted: lastProgress?.IsCompleted == true && !isCanceled,
            IsCanceled: isCanceled,
            CurrentPath: lastProgress?.CurrentPath ?? rootPath,
            ObservedFileCount: lastProgress?.FilesScanned ?? 0,
            ObservedDirectoryCount: lastProgress?.DirectoriesScanned ?? 0,
            ObservedByteTotal: bytes,
            ProgressUpdateCount: progressUpdateCount,
            ErrorCount: lastProgress?.Errors.Count ?? 0,
            DurationMilliseconds: stopwatch.Elapsed.TotalMilliseconds,
            EntriesPerSecond: entries / elapsedSeconds,
            BytesPerSecond: bytes / elapsedSeconds,
            PeakManagedMemoryBytes: peakManagedMemory,
            MeasurementMode: options.MeasurementMode,
            IncludeHiddenFiles: options.IncludeHiddenFiles,
            FollowSymbolicLinks: options.FollowSymbolicLinks,
            TreatPackagesAsDirectories: options.TreatPackagesAsDirectories,
            CloneAccountingCoverage: lastProgress?.CloneAccountingCoverage
                ?? CloneAccountingCoverage.Unavailable,
            Fixture: fixture,
            RuntimeVersion: RuntimeInformation.FrameworkDescription,
            ProcessArchitecture: RuntimeInformation.ProcessArchitecture.ToString(),
            OperatingSystemDescription: RuntimeInformation.OSDescription,
            Timestamp: DateTimeOffset.UtcNow);
    }
}
