using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MacStorageAtlas.App.Models;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.Services;

public sealed class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    static JsonSettingsService()
    {
        SerializerOptions.Converters.Add(
            new NullableStorageMeasurementModeJsonConverter());
    }

    private readonly string _settingsFilePath;

    public JsonSettingsService()
        : this(GetDefaultSettingsFilePath())
    {
    }

    private sealed class NullableStorageMeasurementModeJsonConverter
        : JsonConverter<StorageMeasurementMode?>
    {
        public override StorageMeasurementMode? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonTokenType.String
                && Enum.TryParse<StorageMeasurementMode>(
                    reader.GetString(),
                    ignoreCase: true,
                    out var namedMode)
                && Enum.IsDefined(namedMode))
            {
                return namedMode;
            }

            if (reader.TokenType == JsonTokenType.Number
                && reader.TryGetInt32(out var numericMode)
                && Enum.IsDefined((StorageMeasurementMode)numericMode))
            {
                return (StorageMeasurementMode)numericMode;
            }

            return null;
        }

        public override void Write(
            Utf8JsonWriter writer,
            StorageMeasurementMode? value,
            JsonSerializerOptions options)
        {
            if (value is null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStringValue(value.Value.ToString());
        }
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
