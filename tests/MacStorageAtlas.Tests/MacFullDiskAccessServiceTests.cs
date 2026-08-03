using MacStorageAtlas.Core;
using MacStorageAtlas.Platform.Mac;

namespace MacStorageAtlas.Tests;

public class MacFullDiskAccessServiceTests
{
    [Test]
    public void CheckAccessReportsLikelyMissingWhenProbeIsDeniedOnMacOS()
    {
        var service = new MacFullDiskAccessService(
            new FakeProbe(new FullDiskAccessProbeResult(
                SuccessfulProbeCount: 0,
                PermissionDenied: true)),
            new FakeSettingsOpener());

        var assessment = service.CheckAccess("/scan/root");

        Assert.That(
            assessment.Status,
            OperatingSystem.IsMacOS()
                ? Is.EqualTo(FullDiskAccessStatus.LikelyMissing)
                : Is.EqualTo(FullDiskAccessStatus.NotApplicable));
    }

    [Test]
    public void CheckAccessDoesNotTreatOneSuccessfulProbeAsLikelyGrantedOnMacOS()
    {
        var service = new MacFullDiskAccessService(
            new FakeProbe(new FullDiskAccessProbeResult(
                SuccessfulProbeCount: 1,
                PermissionDenied: false)),
            new FakeSettingsOpener());

        var assessment = service.CheckAccess("/scan/root");

        Assert.That(
            assessment.Status,
            OperatingSystem.IsMacOS()
                ? Is.EqualTo(FullDiskAccessStatus.Indeterminate)
                : Is.EqualTo(FullDiskAccessStatus.NotApplicable));
    }

    [Test]
    public void CheckAccessReportsLikelyGrantedOnlyAfterMultipleSuccessfulProbesOnMacOS()
    {
        var service = new MacFullDiskAccessService(
            new FakeProbe(new FullDiskAccessProbeResult(
                SuccessfulProbeCount: 2,
                PermissionDenied: false)),
            new FakeSettingsOpener());

        var assessment = service.CheckAccess("/scan/root");

        Assert.That(
            assessment.Status,
            OperatingSystem.IsMacOS()
                ? Is.EqualTo(FullDiskAccessStatus.LikelyGranted)
                : Is.EqualTo(FullDiskAccessStatus.NotApplicable));
    }

    [Test]
    public void OpenSettingsReportsDirectOpenWhenFullDiskAccessUrlOpensOnMacOS()
    {
        var opener = new FakeSettingsOpener
        {
            DirectResult = true,
            FallbackResult = false
        };
        var service = new MacFullDiskAccessService(
            new FakeProbe(new FullDiskAccessProbeResult(0, PermissionDenied: false)),
            opener);

        var result = service.OpenSettings();

        if (!OperatingSystem.IsMacOS())
        {
            Assert.That(result, Is.EqualTo(FullDiskAccessSettingsResult.Failed));
            Assert.That(opener.Urls, Is.Empty);
            return;
        }

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(FullDiskAccessSettingsResult.OpenedDirectly));
            Assert.That(opener.Urls, Is.EqualTo(new[] { MacFullDiskAccessService.FullDiskAccessSettingsUrl }));
        });
    }

    [Test]
    public void OpenSettingsReportsFallbackWhenOnlyPrivacySettingsOpenOnMacOS()
    {
        var opener = new FakeSettingsOpener
        {
            DirectResult = false,
            FallbackResult = true
        };
        var service = new MacFullDiskAccessService(
            new FakeProbe(new FullDiskAccessProbeResult(0, PermissionDenied: false)),
            opener);

        var result = service.OpenSettings();

        if (!OperatingSystem.IsMacOS())
        {
            Assert.That(result, Is.EqualTo(FullDiskAccessSettingsResult.Failed));
            Assert.That(opener.Urls, Is.Empty);
            return;
        }

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.EqualTo(FullDiskAccessSettingsResult.OpenedFallback));
            Assert.That(
                opener.Urls,
                Is.EqualTo(new[]
                {
                    MacFullDiskAccessService.FullDiskAccessSettingsUrl,
                    MacFullDiskAccessService.PrivacySettingsUrl
                }));
        });
    }

    [Test]
    public void OpenSettingsReportsFailureWhenNoSettingsUrlOpensOnMacOS()
    {
        var opener = new FakeSettingsOpener
        {
            DirectResult = false,
            FallbackResult = false
        };
        var service = new MacFullDiskAccessService(
            new FakeProbe(new FullDiskAccessProbeResult(0, PermissionDenied: false)),
            opener);

        var result = service.OpenSettings();

        Assert.That(result, Is.EqualTo(FullDiskAccessSettingsResult.Failed));
    }

    private sealed class FakeProbe(FullDiskAccessProbeResult result) : IFullDiskAccessProbe
    {
        public FullDiskAccessProbeResult Probe() => result;
    }

    private sealed class FakeSettingsOpener : ISystemSettingsOpener
    {
        public bool DirectResult { get; init; }

        public bool FallbackResult { get; init; }

        public List<string> Urls { get; } = [];

        public bool Open(string url)
        {
            Urls.Add(url);

            return url == MacFullDiskAccessService.FullDiskAccessSettingsUrl
                ? DirectResult
                : FallbackResult;
        }
    }
}
