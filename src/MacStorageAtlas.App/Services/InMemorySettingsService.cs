using MacStorageAtlas.App.Models;

namespace MacStorageAtlas.App.Services;

/// <summary>
/// A non-persistent settings store used as a safe default for design-time and
/// tests, where touching the real on-disk settings file is undesirable.
/// </summary>
public sealed class InMemorySettingsService : ISettingsService
{
    private AppSettings _settings = new();

    public AppSettings Load() => _settings;

    public void Save(AppSettings settings) => _settings = settings;
}
