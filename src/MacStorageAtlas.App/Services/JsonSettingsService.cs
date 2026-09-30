using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using MacStorageAtlas.App.Models;
using MacStorageAtlas.Core.Scanning;

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
        SerializerOptions.Converters.Add(
            new TolerantFilterPresetListJsonConverter());
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
                && string.Equals(
                    reader.GetString(),
                    "HardlinkAwareAllocated",
                    StringComparison.OrdinalIgnoreCase))
            {
                return StorageMeasurementMode.SharedAwareAllocated;
            }

            if (reader.TokenType == JsonTokenType.String
                && Enum.TryParse(
                    reader.GetString(),
                    ignoreCase: true,
                    out StorageMeasurementMode namedMode)
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
        => TrySave(settings);

    public bool TrySave(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string? temporaryPath = null;
        var ownsTemporaryFile = false;

        try
        {
            var destination = Path.GetFullPath(_settingsFilePath);
            var directory = Path.GetDirectoryName(destination)!;
            Directory.CreateDirectory(directory);
            temporaryPath = Path.Combine(directory,
                $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None
            };
            if (!OperatingSystem.IsWindows())
            {
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            }

            using (var stream = new FileStream(temporaryPath, options))
            {
                ownsTemporaryFile = true;
                JsonSerializer.Serialize(stream, settings, SerializerOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, destination, overwrite: true);
            ownsTemporaryFile = false;
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            if (ownsTemporaryFile && temporaryPath is not null)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                }
            }
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
