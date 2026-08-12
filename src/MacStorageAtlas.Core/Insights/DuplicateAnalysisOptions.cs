namespace MacStorageAtlas.Core.Insights;

public sealed record DuplicateAnalysisOptions
{
    public static DuplicateAnalysisOptions Default { get; } = new();

    public bool IncludeZeroLengthFiles { get; init; }

    public int SampleSizeBytes { get; init; } = 64 * 1024;

    public int BufferSizeBytes { get; init; } = 128 * 1024;
}
