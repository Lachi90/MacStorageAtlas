namespace MacStorageAtlas.Core.Insights;

public sealed record DuplicateAnalysisResult
{
    public DuplicateAnalysisResult(
        IReadOnlyList<DuplicateGroup> Groups,
        IReadOnlyList<DuplicateSkippedCandidate> SkippedCandidates)
    {
        ArgumentNullException.ThrowIfNull(Groups);
        ArgumentNullException.ThrowIfNull(SkippedCandidates);

        this.Groups = Groups.ToArray();
        this.SkippedCandidates = SkippedCandidates.ToArray();
        Summary = CreateSummary(this.Groups, this.SkippedCandidates);
    }

    public static DuplicateAnalysisResult Empty { get; } = new([], []);

    public IReadOnlyList<DuplicateGroup> Groups { get; }

    public IReadOnlyList<DuplicateSkippedCandidate> SkippedCandidates { get; }

    public DuplicateAnalysisSummary Summary { get; }

    private static DuplicateAnalysisSummary CreateSummary(
        IReadOnlyList<DuplicateGroup> groups,
        IReadOnlyList<DuplicateSkippedCandidate> skippedCandidates) =>
        groups.Count == 0 && skippedCandidates.Count == 0
            ? DuplicateAnalysisSummary.Empty
            : new DuplicateAnalysisSummary(
                groups.Count,
                groups.Sum(group => group.ReclaimableCopyCount),
                groups.Sum(group => group.ReclaimableSizeBytes),
                skippedCandidates.Count);
}
