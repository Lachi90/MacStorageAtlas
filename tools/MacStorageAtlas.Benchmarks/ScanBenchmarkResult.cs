using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Benchmarks;

public sealed record ScanBenchmarkResult(
    bool IsCompleted,
    bool IsCanceled,
    string CurrentPath,
    long ObservedFileCount,
    long ObservedDirectoryCount,
    long ObservedByteTotal,
    int ProgressUpdateCount,
    int ErrorCount,
    double DurationMilliseconds,
    double EntriesPerSecond,
    double BytesPerSecond,
    long PeakManagedMemoryBytes,
    StorageMeasurementMode MeasurementMode,
    bool IncludeHiddenFiles,
    bool FollowSymbolicLinks,
    bool TreatPackagesAsDirectories,
    CloneAccountingCoverage CloneAccountingCoverage,
    BenchmarkFixtureInfo Fixture,
    string RuntimeVersion,
    string ProcessArchitecture,
    string OperatingSystemDescription,
    DateTimeOffset Timestamp);
