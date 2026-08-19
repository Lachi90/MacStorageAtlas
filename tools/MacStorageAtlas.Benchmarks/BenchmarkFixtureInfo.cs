namespace MacStorageAtlas.Benchmarks;

public sealed record BenchmarkFixtureInfo(
    BenchmarkFixtureKind Kind,
    string RootPath,
    string Description,
    bool IsRealFileSystem,
    int? OrdinaryFileCount,
    int? SparseFileCount,
    int? HardlinkCount,
    int? SymbolicLinkCount,
    int? PackageCount,
    long? SyntheticFileCount,
    IReadOnlyList<string> Limitations);
