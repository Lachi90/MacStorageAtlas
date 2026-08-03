namespace MacStorageAtlas.Core;

public interface IFullDiskAccessService
{
    FullDiskAccessAssessment CheckAccess(string scanRootPath);

    FullDiskAccessSettingsResult OpenSettings();
}
