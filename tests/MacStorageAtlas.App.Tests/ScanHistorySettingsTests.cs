using System.IO;
using MacStorageAtlas.App.Models;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.Tests;

public class ScanHistorySettingsTests
{
    private string _directory = null!;
    private string _settingsFilePath = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(
            Path.GetTempPath(),
            $"MacStorageAtlas-history-settings-{Guid.NewGuid():N}");
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
    public void ScanHistoryIsDisabledByDefault()
    {
        Assert.That(new AppSettings().ScanHistoryEnabled, Is.False);
    }

    [Test]
    public void AbsentLimitsFallBackToTheDefaults()
    {
        var limits = new AppSettings().EffectiveScanHistoryLimits;

        Assert.Multiple(() =>
        {
            Assert.That(
                limits.MaxSnapshotsPerRoot,
                Is.EqualTo(ScanHistoryLimits.DefaultMaxSnapshotsPerRoot));
            Assert.That(
                limits.MaxTotalSizeBytes,
                Is.EqualTo(ScanHistoryLimits.DefaultMaxTotalSizeBytes));
        });
    }

    [Test]
    public void ConfiguredLimitsAreUsed()
    {
        var limits = new AppSettings
        {
            MaxScanHistorySnapshotsPerRoot = 3,
            MaxScanHistoryStoreSizeBytes = 1024
        }.EffectiveScanHistoryLimits;

        Assert.Multiple(() =>
        {
            Assert.That(limits.MaxSnapshotsPerRoot, Is.EqualTo(3));
            Assert.That(limits.MaxTotalSizeBytes, Is.EqualTo(1024));
        });
    }

    [Test]
    public void UnusableLimitsFallBackToTheDefaults()
    {
        var limits = new AppSettings
        {
            MaxScanHistorySnapshotsPerRoot = 0,
            MaxScanHistoryStoreSizeBytes = -1
        }.EffectiveScanHistoryLimits;

        Assert.Multiple(() =>
        {
            Assert.That(
                limits.MaxSnapshotsPerRoot,
                Is.EqualTo(ScanHistoryLimits.DefaultMaxSnapshotsPerRoot));
            Assert.That(
                limits.MaxTotalSizeBytes,
                Is.EqualTo(ScanHistoryLimits.DefaultMaxTotalSizeBytes));
        });
    }

    [Test]
    public void ScanHistorySettingsSurviveALoadFromANewService()
    {
        new JsonSettingsService(_settingsFilePath).Save(new AppSettings
        {
            ScanHistoryEnabled = true,
            MaxScanHistorySnapshotsPerRoot = 4,
            MaxScanHistoryStoreSizeBytes = 2048
        });

        var loaded = new JsonSettingsService(_settingsFilePath).Load();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.ScanHistoryEnabled, Is.True);
            Assert.That(loaded.MaxScanHistorySnapshotsPerRoot, Is.EqualTo(4));
            Assert.That(loaded.MaxScanHistoryStoreSizeBytes, Is.EqualTo(2048));
        });
    }

    [Test]
    public void ASettingsFileWrittenBeforeScanHistoryLoadsWithHistoryOff()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(
            _settingsFilePath,
            """
            {
              "IncludeHiddenFiles": true,
              "FollowSymbolicLinks": false,
              "TreatPackagesAsDirectories": true,
              "MeasurementMode": "Logical",
              "RecentLocations": ["/Users/test/A"],
              "FilterPresets": [],
              "WindowWidth": 1280,
              "WindowHeight": 760
            }
            """);

        var loaded = new JsonSettingsService(_settingsFilePath).Load();

        Assert.Multiple(() =>
        {
            Assert.That(loaded.ScanHistoryEnabled, Is.False);
            Assert.That(loaded.MaxScanHistorySnapshotsPerRoot, Is.Null);
            Assert.That(loaded.MaxScanHistoryStoreSizeBytes, Is.Null);
            Assert.That(loaded.IncludeHiddenFiles, Is.True);
            Assert.That(loaded.RecentLocations, Is.EqualTo(new[] { "/Users/test/A" }));
            Assert.That(
                loaded.EffectiveMeasurementMode,
                Is.EqualTo(StorageMeasurementMode.Logical));
        });
    }
}
