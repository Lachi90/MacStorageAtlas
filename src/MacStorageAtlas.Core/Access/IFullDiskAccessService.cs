namespace MacStorageAtlas.Core.Access;

public interface IFullDiskAccessService
{
    FullDiskAccessAssessment CheckAccess(string scanRootPath);

    FullDiskAccessSettingsResult OpenSettings();
}
