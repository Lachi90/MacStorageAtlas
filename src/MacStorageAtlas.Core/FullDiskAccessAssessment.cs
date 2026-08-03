namespace MacStorageAtlas.Core;

public sealed record FullDiskAccessAssessment(
    FullDiskAccessStatus Status,
    int SuccessfulProbeCount = 0)
{
    public static FullDiskAccessAssessment NotApplicable { get; } =
        new(FullDiskAccessStatus.NotApplicable);

    public static FullDiskAccessAssessment Indeterminate { get; } =
        new(FullDiskAccessStatus.Indeterminate);
}
