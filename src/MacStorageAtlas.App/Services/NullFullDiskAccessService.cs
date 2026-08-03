using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.Services;

internal sealed class NullFullDiskAccessService : IFullDiskAccessService
{
    public FullDiskAccessAssessment CheckAccess(string scanRootPath) =>
        FullDiskAccessAssessment.NotApplicable;

    public FullDiskAccessSettingsResult OpenSettings() =>
        FullDiskAccessSettingsResult.Failed;
}
