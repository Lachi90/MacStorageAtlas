using System.IO;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core.Access;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.App.Tests;

public class AccessGuidanceClassifierTests
{
    [Test]
    public void ClassifyReturnsNoneWhenThereAreNoErrorsAndAccessIsNotApplicable()
    {
        var classifier = new AccessGuidanceClassifier();

        var guidance = classifier.Classify([], FullDiskAccessAssessment.NotApplicable);

        Assert.That(guidance.Status, Is.EqualTo(AccessGuidanceStatus.None));
    }

    [Test]
    public void ClassifyReportsLikelyMissingFullDiskAccessForPermissionErrorsAndMissingAccess()
    {
        var classifier = new AccessGuidanceClassifier();
        var errors = new[]
        {
            new ScanError(
                "/Users/test/Library/Mail",
                "Access denied.",
                nameof(UnauthorizedAccessException))
        };

        var guidance = classifier.Classify(
            errors,
            new FullDiskAccessAssessment(FullDiskAccessStatus.LikelyMissing));

        Assert.Multiple(() =>
        {
            Assert.That(
                guidance.Status,
                Is.EqualTo(AccessGuidanceStatus.LikelyMissingFullDiskAccess));
            Assert.That(guidance.InaccessiblePathCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void ClassifySandboxRestrictedPermissionErrorsAsRequiringSelection()
    {
        var classifier = new AccessGuidanceClassifier();
        var errors = new[]
        {
            new ScanError(
                "/Users/test/Documents",
                "Access denied.",
                nameof(UnauthorizedAccessException))
        };

        var guidance = classifier.Classify(
            errors,
            FullDiskAccessAssessment.SandboxRestricted);

        Assert.Multiple(() =>
        {
            Assert.That(
                guidance.Status,
                Is.EqualTo(AccessGuidanceStatus.SandboxedSelectionRequired));
            Assert.That(guidance.InaccessiblePathCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void ClassifySandboxRestrictedScanWithoutPermissionErrorsAsNoGuidance()
    {
        var classifier = new AccessGuidanceClassifier();
        var errors = new[]
        {
            new ScanError(
                "/Volumes/Disk/file.bin",
                "The device is not ready.",
                nameof(IOException))
        };

        var guidance = classifier.Classify(
            errors,
            FullDiskAccessAssessment.SandboxRestricted);

        Assert.That(guidance.Status, Is.EqualTo(AccessGuidanceStatus.None));
    }

    [Test]
    public void ClassifyDoesNotTreatOrdinaryIoErrorsAsFullDiskAccessFailures()
    {
        var classifier = new AccessGuidanceClassifier();
        var errors = new[]
        {
            new ScanError(
                "/Volumes/Disk/file.bin",
                "The device is not ready.",
                nameof(IOException))
        };

        var guidance = classifier.Classify(
            errors,
            new FullDiskAccessAssessment(FullDiskAccessStatus.LikelyMissing));

        Assert.That(guidance.Status, Is.EqualTo(AccessGuidanceStatus.None));
    }

    [Test]
    public void ClassifyReportsIncompleteScanForMixedErrorsWithoutReclassifyingAllErrors()
    {
        var classifier = new AccessGuidanceClassifier();
        var errors = new[]
        {
            new ScanError(
                "/scan/root/restricted",
                "Operation not permitted.",
                nameof(IOException)),
            new ScanError(
                "/scan/root/transient",
                "The network connection was lost.",
                nameof(IOException))
        };

        var guidance = classifier.Classify(
            errors,
            new FullDiskAccessAssessment(FullDiskAccessStatus.LikelyGranted, SuccessfulProbeCount: 2));

        Assert.Multiple(() =>
        {
            Assert.That(guidance.Status, Is.EqualTo(AccessGuidanceStatus.IncompleteScan));
            Assert.That(guidance.InaccessiblePathCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void ClassifyDoesNotReportGrantedAccessFromOneReadableProbe()
    {
        var classifier = new AccessGuidanceClassifier();

        var guidance = classifier.Classify(
            [],
            new FullDiskAccessAssessment(FullDiskAccessStatus.LikelyGranted, SuccessfulProbeCount: 1));

        Assert.That(guidance.Status, Is.EqualTo(AccessGuidanceStatus.None));
    }

    [Test]
    public void ClassifyReportsIndeterminateWhenAccessCannotBeDetermined()
    {
        var classifier = new AccessGuidanceClassifier();

        var guidance = classifier.Classify([], FullDiskAccessAssessment.Indeterminate);

        Assert.That(guidance.Status, Is.EqualTo(AccessGuidanceStatus.Indeterminate));
    }
}
