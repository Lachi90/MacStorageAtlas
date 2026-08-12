namespace MacStorageAtlas.Core.Insights;

public sealed record DuplicateAnalysisProgress(
    DuplicateAnalysisStage Stage,
    string? CurrentPath,
    long CandidatesExamined,
    long CandidateCount,
    long BytesRead,
    int GroupsFound)
{
    public static DuplicateAnalysisProgress Start { get; } = new(
        DuplicateAnalysisStage.CollectingCandidates,
        CurrentPath: null,
        CandidatesExamined: 0,
        CandidateCount: 0,
        BytesRead: 0,
        GroupsFound: 0);
}
