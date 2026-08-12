namespace MacStorageAtlas.Core.Insights;

public enum DuplicateAnalysisStage
{
    CollectingCandidates,

    SamplingCandidates,

    HashingCandidates,

    ConfirmingEquality,

    Completed
}
