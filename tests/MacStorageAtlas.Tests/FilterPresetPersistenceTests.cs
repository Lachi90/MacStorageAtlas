using System.Text.Json;
using MacStorageAtlas.App.Models;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.Tests;

public class FilterPresetPersistenceTests
{
    private string _directory = string.Empty;
    private string _settingsPath = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            $"macstorageatlas-presets-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_directory);
        _settingsPath = Path.Combine(_directory, "settings.json");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public void APresetRoundTripsThroughSettings()
    {
        var service = new JsonSettingsService(_settingsPath);
        var filter = new DiskItemFilter
        {
            TextTerm = "report",
            MinimumSizeBytes = 2048,
            MaximumSizeBytes = 8192,
            ModifiedBefore = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Extensions = [".mov"],
            Categories = [FileCategory.Video],
            SharedStorageOnly = true
        };

        service.Save(new AppSettings
        {
            FilterPresets = [FilterPresetSettings.FromPreset(new FilterPreset("Mine", filter))]
        });
        var restored = service.Load().FilterPresets.Single().TryCreatePreset();

        Assert.Multiple(() =>
        {
            Assert.That(restored, Is.Not.Null);
            Assert.That(restored!.Name, Is.EqualTo("Mine"));
            Assert.That(restored.Filter, Is.EqualTo(filter));
        });
    }

    [Test]
    public void SettingsWrittenWithoutPresetsLoadWithNone()
    {
        File.WriteAllText(
            _settingsPath,
            """{ "IncludeHiddenFiles": true, "RecentLocations": ["/tmp"] }""");

        var settings = new JsonSettingsService(_settingsPath).Load();

        Assert.Multiple(() =>
        {
            Assert.That(settings.FilterPresets, Is.Empty);
            Assert.That(settings.IncludeHiddenFiles, Is.True);
            Assert.That(settings.RecentLocations, Is.EqualTo(["/tmp"]));
        });
    }

    [Test]
    public void AnUnreadablePresetIsSkippedAndTheRestOfTheSettingsLoad()
    {
        File.WriteAllText(
            _settingsPath,
            """
            {
              "IncludeHiddenFiles": true,
              "RecentLocations": ["/tmp"],
              "FilterPresets": [
                { "SchemaVersion": 1, "Name": "Good", "MinimumSizeBytes": 1024 },
                { "SchemaVersion": 1, "Name": "Bad", "MinimumSizeBytes": "not-a-number" },
                { "SchemaVersion": 1, "Name": "AlsoGood", "MaximumSizeBytes": 4096 }
              ]
            }
            """);

        var settings = new JsonSettingsService(_settingsPath).Load();

        Assert.Multiple(() =>
        {
            Assert.That(
                settings.FilterPresets.Select(preset => preset.Name),
                Is.EqualTo(["Good", "AlsoGood"]));
            Assert.That(settings.IncludeHiddenFiles, Is.True);
            Assert.That(settings.RecentLocations, Is.EqualTo(["/tmp"]));
        });
    }

    [Test]
    public void APresetFromANewerSchemaIsNotMaterialized()
    {
        var settings = new FilterPresetSettings
        {
            SchemaVersion = FilterPresetSettings.CurrentSchemaVersion + 1,
            Name = "Future",
            MinimumSizeBytes = 1024
        };

        Assert.That(settings.TryCreatePreset(), Is.Null);
    }

    [Test]
    public void APresetWithoutANameIsNotMaterialized()
    {
        var settings = new FilterPresetSettings { Name = "   ", MinimumSizeBytes = 1024 };

        Assert.That(settings.TryCreatePreset(), Is.Null);
    }

    [Test]
    public void AContradictoryStoredPresetIsNotMaterialized()
    {
        var settings = new FilterPresetSettings
        {
            Name = "Contradictory",
            MinimumSizeBytes = 4096,
            MaximumSizeBytes = 1024
        };

        Assert.That(settings.TryCreatePreset(), Is.Null);
    }

    [Test]
    public void AnUnknownCategoryValueIsDiscarded()
    {
        var settings = new FilterPresetSettings
        {
            Name = "Odd",
            Categories = [(FileCategory)9999, FileCategory.Video]
        };

        var preset = settings.TryCreatePreset();

        Assert.Multiple(() =>
        {
            Assert.That(preset, Is.Not.Null);
            Assert.That(preset!.Filter.Categories, Is.EqualTo([FileCategory.Video]));
        });
    }

    [Test]
    public void AMalformedPresetsArrayLeavesTheRestOfTheSettingsLoadable()
    {
        File.WriteAllText(
            _settingsPath,
            """{ "IncludeHiddenFiles": true, "FilterPresets": "not-an-array" }""");

        var settings = new JsonSettingsService(_settingsPath).Load();

        Assert.Multiple(() =>
        {
            Assert.That(settings.FilterPresets, Is.Empty);
            Assert.That(settings.IncludeHiddenFiles, Is.True);
        });
    }

    [Test]
    public void SavedPresetsAreWrittenAsJsonObjects()
    {
        var service = new JsonSettingsService(_settingsPath);
        service.Save(new AppSettings
        {
            FilterPresets =
            [
                FilterPresetSettings.FromPreset(
                    new FilterPreset(
                        "Big",
                        new DiskItemFilter { MinimumSizeBytes = 1024 }))
            ]
        });

        using var document = JsonDocument.Parse(File.ReadAllText(_settingsPath));
        var presets = document.RootElement.GetProperty("FilterPresets");

        Assert.Multiple(() =>
        {
            Assert.That(presets.GetArrayLength(), Is.EqualTo(1));
            Assert.That(
                presets[0].GetProperty("Name").GetString(),
                Is.EqualTo("Big"));
            Assert.That(
                presets[0].GetProperty("SchemaVersion").GetInt32(),
                Is.EqualTo(FilterPresetSettings.CurrentSchemaVersion));
        });
    }
}
