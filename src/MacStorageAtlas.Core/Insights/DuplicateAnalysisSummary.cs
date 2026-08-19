namespace MacStorageAtlas.Core.Insights;

public sealed record DuplicateAnalysisSummary(
    int GroupCount,
    int ReclaimableCopyCount,
    long ReclaimableSizeBytes,
    int SkippedCandidateCount)
{
    public static DuplicateAnalysisSummary Empty { get; } = new(
        GroupCount: 0,
        ReclaimableCopyCount: 0,
        ReclaimableSizeBytes: 0,
        SkippedCandidateCount: 0);
}
