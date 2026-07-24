using System.IO;
using MacStorageAtlas.App.Models;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.Tests;

public class JsonSettingsServiceTests
{
    private string _directory = null!;
    private string _settingsFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"MacStorageAtlas-settings-{Guid.NewGuid():N}");
        _settingsFilePath = Path.Combine(_directory, "settings.json");
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
    public void LoadReturnsDefaultsWhenNoFileExists()
    {
        var service = new JsonSettingsService(_settingsFilePath);

        var settings = service.Load();

        Assert.Multiple(() =>
        {
            Assert.That(settings.IncludeHiddenFiles, Is.False);
            Assert.That(settings.FollowSymbolicLinks, Is.False);
            Assert.That(settings.TreatPackagesAsDirectories, Is.True);
            Assert.That(
                settings.EffectiveMeasurementMode,
                Is.EqualTo(StorageMeasurementMode.HardlinkAwareAllocated));
            Assert.That(settings.RecentLocations, Is.Empty);
        });
    }

    [Test]
    public void SavedSettingsSurviveALoadFromANewService()
    {
        var settings = new AppSettings
        {
            IncludeHiddenFiles = true,
            FollowSymbolicLinks = true,
            TreatPackagesAsDirectories = false,
            MeasurementMode = StorageMeasurementMode.Allocated,
            RecentLocations = ["/Users/test/A", "/Users/test/B"]
        };
        new JsonSettingsService(_settingsFilePath).Save(settings);

        var loaded = new JsonSettingsService(_settingsFilePath).Load();

        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(_settingsFilePath), Is.True);
            Assert.That(loaded.IncludeHiddenFiles, Is.True);
            Assert.That(loaded.FollowSymbolicLinks, Is.True);
            Assert.That(loaded.TreatPackagesAsDirectories, Is.False);
            Assert.That(
                loaded.EffectiveMeasurementMode,
                Is.EqualTo(StorageMeasurementMode.Allocated));
            Assert.That(loaded.RecentLocations, Is.EqualTo(new[] { "/Users/test/A", "/Users/test/B" }));
            Assert.That(
                File.ReadAllText(_settingsFilePath),
                Does.Contain("\"MeasurementMode\": \"Allocated\""));
            Assert.That(
                File.ReadAllText(_settingsFilePath),
                Does.Not.Contain("MeasureAllocatedSize"));
        });
    }

    [TestCase(true, StorageMeasurementMode.HardlinkAwareAllocated)]
    [TestCase(false, StorageMeasurementMode.Logical)]
    public void LoadMigratesLegacyAllocatedPreferenceAndPreservesOtherSettings(
        bool measureAllocatedSize,
        StorageMeasurementMode expectedMode)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            _settingsFilePath,
            $$"""
              {
                "IncludeHiddenFiles": true,
                "FollowSymbolicLinks": true,
                "TreatPackagesAsDirectories": false,
                "MeasureAllocatedSize": {{measureAllocatedSize.ToString().ToLowerInvariant()}},
                "RecentLocations": ["/Users/test/Legacy"]
              }
              """);

        var settings = new JsonSettingsService(_settingsFilePath).Load();

        Assert.Multiple(() =>
        {
            Assert.That(settings.EffectiveMeasurementMode, Is.EqualTo(expectedMode));
            Assert.That(settings.IncludeHiddenFiles, Is.True);
            Assert.That(settings.FollowSymbolicLinks, Is.True);
            Assert.That(settings.TreatPackagesAsDirectories, Is.False);
            Assert.That(settings.RecentLocations, Is.EqualTo(new[] { "/Users/test/Legacy" }));
        });
    }

    [TestCase(StorageMeasurementMode.Logical)]
    [TestCase(StorageMeasurementMode.Allocated)]
    [TestCase(StorageMeasurementMode.HardlinkAwareAllocated)]
    public void LoadReadsNamedMeasurementModes(StorageMeasurementMode measurementMode)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            _settingsFilePath,
            $$"""
              {
                "MeasurementMode": "{{measurementMode}}"
              }
              """);

        var settings = new JsonSettingsService(_settingsFilePath).Load();

        Assert.That(settings.EffectiveMeasurementMode, Is.EqualTo(measurementMode));
    }

    [Test]
    public void LoadFallsBackForInvalidMeasurementModeWithoutDiscardingOtherSettings()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            _settingsFilePath,
            """
            {
              "IncludeHiddenFiles": true,
              "MeasurementMode": "FutureUnknownMode",
              "RecentLocations": ["/Users/test/Kept"]
            }
            """);

        var settings = new JsonSettingsService(_settingsFilePath).Load();

        Assert.Multiple(() =>
        {
            Assert.That(
                settings.EffectiveMeasurementMode,
                Is.EqualTo(StorageMeasurementMode.HardlinkAwareAllocated));
            Assert.That(settings.IncludeHiddenFiles, Is.True);
            Assert.That(settings.RecentLocations, Is.EqualTo(new[] { "/Users/test/Kept" }));
        });
    }

    [Test]
    public void LoadRecoversFromAMalformedFile()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_settingsFilePath, "{ this is not valid json");
        var service = new JsonSettingsService(_settingsFilePath);

        var settings = service.Load();

        Assert.Multiple(() =>
        {
            Assert.That(settings.TreatPackagesAsDirectories, Is.True);
            Assert.That(settings.RecentLocations, Is.Empty);
        });
    }
}
