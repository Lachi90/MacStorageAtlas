using System;
using System.IO;
using System.Text.Json;
using MacStorageAtlas.App.Models;

namespace MacStorageAtlas.App.Services;

/// <summary>
/// Persists <see cref="AppSettings"/> to a JSON file in the user's macOS
/// application-data directory. Reads recover gracefully from a missing or
/// malformed file by falling back to default settings.
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _settingsFilePath;

    public JsonSettingsService()
        : this(GetDefaultSettingsFilePath())
    {
    }

    public JsonSettingsService(string settingsFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(settingsFilePath);
        _settingsFilePath = settingsFilePath;
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsFilePath))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions)
                   ?? new AppSettings();
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or JsonException)
        {
            // A missing, unreadable, or malformed settings file should never stop
            // the app from starting. Fall back to defaults instead.
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        try
        {
            var directory = Path.GetDirectoryName(_settingsFilePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            File.WriteAllText(_settingsFilePath, json);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            // Persisting preferences is best-effort; failing to write must not
            // crash the app.
        }
    }

    private static string GetDefaultSettingsFilePath()
    {
        var applicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.ApplicationData,
            Environment.SpecialFolderOption.Create);

        return Path.Combine(applicationData, "MacStorageAtlas", "settings.json");
    }
}
