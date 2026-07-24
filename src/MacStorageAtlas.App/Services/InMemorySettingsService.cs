using MacStorageAtlas.App.Models;

namespace MacStorageAtlas.App.Services;

public sealed class InMemorySettingsService : ISettingsService
{
    private AppSettings _settings = new();

    public AppSettings Load() => _settings;

    public void Save(AppSettings settings) => _settings = settings;
}
