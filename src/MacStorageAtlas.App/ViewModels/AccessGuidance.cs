namespace MacStorageAtlas.App.ViewModels;

public sealed record AccessGuidance(
    AccessGuidanceStatus Status,
    int InaccessiblePathCount,
    bool ShowsManualSettingsFallback = false)
{
    public static AccessGuidance None { get; } =
        new(AccessGuidanceStatus.None, InaccessiblePathCount: 0);
}
