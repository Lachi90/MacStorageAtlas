using MacStorageAtlas.Core;

namespace MacStorageAtlas.Core.Tests;

public class ScanHistoryRetentionPolicyTests
{
    private static readonly DateTimeOffset Origin =
        new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

    [Test]
    public void AStoreWithinItsLimitsPrunesNothing()
    {
        var decision = ScanHistoryRetentionPolicy.DecideForCapture(
            [Snapshot("a", "/home", 0, 100), Snapshot("b", "/home", 1, 100)],
            "/home",
            100,
            new ScanHistoryLimits(10, 10_000));

        Assert.Multiple(() =>
        {
            Assert.That(decision.IsAccepted, Is.True);
            Assert.That(decision.SnapshotsToPrune, Is.Empty);
        });
    }

    [Test]
    public void ExceedingTheCountLimitPrunesTheOldestOfThatRoot()
    {
        var decision = ScanHistoryRetentionPolicy.DecideForCapture(
            [
                Snapshot("oldest", "/home", 0, 100),
                Snapshot("middle", "/home", 1, 100),
                Snapshot("newest", "/home", 2, 100)
            ],
            "/home",
            100,
            new ScanHistoryLimits(3, 10_000));

        Assert.Multiple(() =>
        {
            Assert.That(decision.IsAccepted, Is.True);
            Assert.That(
                decision.SnapshotsToPrune.Select(snapshot => snapshot.SnapshotId),
                Is.EqualTo(new[] { "oldest" }));
        });
    }

    [Test]
    public void TheCountLimitNeverPrunesAnotherRoot()
    {
        var decision = ScanHistoryRetentionPolicy.DecideForCapture(
            [
                Snapshot("other-oldest", "/projects", 0, 100),
                Snapshot("home-oldest", "/home", 1, 100),
                Snapshot("home-newest", "/home", 2, 100)
            ],
            "/home",
            100,
            new ScanHistoryLimits(2, 10_000));

        Assert.That(
            decision.SnapshotsToPrune.Select(snapshot => snapshot.SnapshotId),
            Is.EqualTo(new[] { "home-oldest" }));
    }

    [Test]
    public void ExceedingTheTotalSizePrunesTheGloballyOldest()
    {
        var decision = ScanHistoryRetentionPolicy.DecideForCapture(
            [
                Snapshot("oldest", "/projects", 0, 400),
                Snapshot("middle", "/home", 1, 400),
                Snapshot("newest", "/home", 2, 400)
            ],
            "/home",
            400,
            new ScanHistoryLimits(10, 1000));

        Assert.That(
            decision.SnapshotsToPrune.Select(snapshot => snapshot.SnapshotId),
            Is.EqualTo(new[] { "oldest", "middle" }));
    }

    [Test]
    public void SizePruningRemovesNoMoreThanRequired()
    {
        var decision = ScanHistoryRetentionPolicy.DecideForCapture(
            [
                Snapshot("oldest", "/home", 0, 300),
                Snapshot("middle", "/home", 1, 300),
                Snapshot("newest", "/home", 2, 300)
            ],
            "/home",
            200,
            new ScanHistoryLimits(10, 1000));

        Assert.Multiple(() =>
        {
            Assert.That(decision.SnapshotsToPrune, Has.Count.EqualTo(1));
            Assert.That(
                decision.SnapshotsToPrune[0].SnapshotId,
                Is.EqualTo("oldest"));
        });
    }

    [Test]
    public void CountAndSizeLimitsApplyTogetherWithoutDoublePruning()
    {
        var decision = ScanHistoryRetentionPolicy.DecideForCapture(
            [
                Snapshot("oldest", "/home", 0, 500),
                Snapshot("middle", "/home", 1, 200),
                Snapshot("newest", "/home", 2, 200)
            ],
            "/home",
            200,
            new ScanHistoryLimits(3, 500));

        Assert.Multiple(() =>
        {
            Assert.That(
                decision.SnapshotsToPrune.Select(snapshot => snapshot.SnapshotId),
                Is.EqualTo(new[] { "oldest", "middle" }));
            Assert.That(
                decision.SnapshotsToPrune.Select(snapshot => snapshot.SnapshotId),
                Is.Unique);
        });
    }

    [Test]
    public void ASnapshotLargerThanTheWholeStoreLimitIsRefused()
    {
        var decision = ScanHistoryRetentionPolicy.DecideForCapture(
            [Snapshot("existing", "/home", 0, 100)],
            "/home",
            2000,
            new ScanHistoryLimits(10, 1000));

        Assert.Multiple(() =>
        {
            Assert.That(decision.IsAccepted, Is.False);
            Assert.That(decision.SnapshotsToPrune, Is.Empty);
            Assert.That(decision.RefusalMessage, Does.Contain("1000"));
        });
    }

    [Test]
    public void ARefusedCaptureDoesNotPruneAnything()
    {
        var decision = ScanHistoryRetentionPolicy.DecideForCapture(
            [
                Snapshot("oldest", "/home", 0, 900),
                Snapshot("newest", "/home", 1, 900)
            ],
            "/home",
            5000,
            new ScanHistoryLimits(1, 1000));

        Assert.Multiple(() =>
        {
            Assert.That(decision.IsAccepted, Is.False);
            Assert.That(decision.SnapshotsToPrune, Is.Empty);
        });
    }

    [Test]
    public void ACaptureForANewRootDoesNotPruneExistingRootsForCount()
    {
        var decision = ScanHistoryRetentionPolicy.DecideForCapture(
            [Snapshot("existing", "/home", 0, 100)],
            "/projects",
            100,
            new ScanHistoryLimits(1, 10_000));

        Assert.That(decision.SnapshotsToPrune, Is.Empty);
    }

    [Test]
    public void LoweringTheCountLimitBringsAnExistingStoreWithinIt()
    {
        var pruned = ScanHistoryRetentionPolicy.DecideForLimitChange(
            [
                Snapshot("oldest", "/home", 0, 100),
                Snapshot("middle", "/home", 1, 100),
                Snapshot("newest", "/home", 2, 100)
            ],
            new ScanHistoryLimits(1, 10_000));

        Assert.That(
            pruned.Select(snapshot => snapshot.SnapshotId),
            Is.EqualTo(new[] { "oldest", "middle" }));
    }

    [Test]
    public void LoweringTheTotalSizeLimitBringsAnExistingStoreWithinIt()
    {
        var pruned = ScanHistoryRetentionPolicy.DecideForLimitChange(
            [
                Snapshot("oldest", "/home", 0, 400),
                Snapshot("newest", "/projects", 1, 400)
            ],
            new ScanHistoryLimits(10, 500));

        Assert.That(
            pruned.Select(snapshot => snapshot.SnapshotId),
            Is.EqualTo(new[] { "oldest" }));
    }

    [Test]
    public void AStoreAlreadyWithinChangedLimitsPrunesNothing()
    {
        var pruned = ScanHistoryRetentionPolicy.DecideForLimitChange(
            [Snapshot("only", "/home", 0, 100)],
            new ScanHistoryLimits(5, 10_000));

        Assert.That(pruned, Is.Empty);
    }

    [Test]
    public void LimitsRejectNonPositiveValues()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ScanHistoryLimits(0, 1000));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ScanHistoryLimits(10, 0));
        });
    }

    private static ScanSnapshotDescriptor Snapshot(
        string snapshotId,
        string rootPath,
        int ageOrder,
        long storedSizeBytes) =>
        new(
            new ScanSnapshotMetadata(
                snapshotId,
                Origin.AddHours(ageOrder),
                rootPath,
                Origin.AddHours(ageOrder),
                ScanOptions.Default,
                StorageMeasurementMode.SharedAwareAllocated,
                CloneAccountingCoverage.Available,
                10,
                storedSizeBytes,
                0,
                ScanCompleteness.Complete),
            storedSizeBytes);
}
