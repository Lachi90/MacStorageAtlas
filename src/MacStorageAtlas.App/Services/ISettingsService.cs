using MacStorageAtlas.App.Models;

namespace MacStorageAtlas.App.Services;

public interface ISettingsService
{
    AppSettings Load();

    void Save(AppSettings settings);

    bool TrySave(AppSettings settings)
    {
        Save(settings);
        return true;
    }
}
