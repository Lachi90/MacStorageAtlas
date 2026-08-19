using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using MacStorageAtlas.App.Models;

namespace MacStorageAtlas.App.Services;

public sealed class TolerantFilterPresetListJsonConverter
    : JsonConverter<List<FilterPresetSettings>>
{
    public override List<FilterPresetSettings> Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var presets = new List<FilterPresetSettings>();

        if (reader.TokenType == JsonTokenType.Null)
        {
            return presets;
        }

        if (reader.TokenType != JsonTokenType.StartArray)
        {
            reader.Skip();
            return presets;
        }

        using var document = JsonDocument.ParseValue(ref reader);
        foreach (var element in document.RootElement.EnumerateArray())
        {
            try
            {
                if (element.Deserialize<FilterPresetSettings>(options) is { } preset)
                {
                    presets.Add(preset);
                }
            }
            catch (JsonException)
            {
            }
            catch (NotSupportedException)
            {
            }
        }

        return presets;
    }

    public override void Write(
        Utf8JsonWriter writer,
        List<FilterPresetSettings> value,
        JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        foreach (var preset in value)
        {
            JsonSerializer.Serialize(writer, preset, options);
        }

        writer.WriteEndArray();
    }
}
