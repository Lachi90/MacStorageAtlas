using System.IO;
using MacStorageAtlas.App.Models;
using MacStorageAtlas.App.Services;

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
        // Arrange
        var service = new JsonSettingsService(_settingsFilePath);

        // Act
        var settings = service.Load();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(settings.IncludeHiddenFiles, Is.False);
            Assert.That(settings.FollowSymbolicLinks, Is.False);
            Assert.That(settings.TreatPackagesAsDirectories, Is.True);
            Assert.That(settings.RecentLocations, Is.Empty);
        });
    }

    [Test]
    public void SavedSettingsSurviveALoadFromANewService()
    {
        // Arrange
        var settings = new AppSettings
        {
            IncludeHiddenFiles = true,
            FollowSymbolicLinks = true,
            TreatPackagesAsDirectories = false,
            RecentLocations = ["/Users/test/A", "/Users/test/B"]
        };
        new JsonSettingsService(_settingsFilePath).Save(settings);

        // Act
        var loaded = new JsonSettingsService(_settingsFilePath).Load();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(File.Exists(_settingsFilePath), Is.True);
            Assert.That(loaded.IncludeHiddenFiles, Is.True);
            Assert.That(loaded.FollowSymbolicLinks, Is.True);
            Assert.That(loaded.TreatPackagesAsDirectories, Is.False);
            Assert.That(loaded.RecentLocations, Is.EqualTo(new[] { "/Users/test/A", "/Users/test/B" }));
        });
    }

    [Test]
    public void LoadRecoversFromAMalformedFile()
    {
        // Arrange
        Directory.CreateDirectory(_directory);
        File.WriteAllText(_settingsFilePath, "{ this is not valid json");
        var service = new JsonSettingsService(_settingsFilePath);

        // Act
        var settings = service.Load();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(settings.TreatPackagesAsDirectories, Is.True);
            Assert.That(settings.RecentLocations, Is.Empty);
        });
    }
}
