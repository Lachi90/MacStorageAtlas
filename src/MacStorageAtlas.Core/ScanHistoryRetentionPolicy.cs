namespace MacStorageAtlas.Core;

public static class ScanHistoryRetentionPolicy
{
    public static ScanHistoryRetentionDecision DecideForCapture(
        IReadOnlyList<ScanSnapshotDescriptor> existing,
        string incomingRootPath,
        long incomingSizeBytes,
        ScanHistoryLimits limits)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentException.ThrowIfNullOrWhiteSpace(incomingRootPath);
        ArgumentOutOfRangeException.ThrowIfNegative(incomingSizeBytes);
        ArgumentNullException.ThrowIfNull(limits);

        if (incomingSizeBytes > limits.MaxTotalSizeBytes)
        {
            return ScanHistoryRetentionDecision.Refuse(
                $"The snapshot needs {incomingSizeBytes} bytes, which exceeds the "
                + $"{limits.MaxTotalSizeBytes} byte scan history store limit on its own.");
        }

        var remaining = Ordered(existing).ToList();
        var pruned = new List<ScanSnapshotDescriptor>();

        PruneToCountLimit(remaining, pruned, limits, incomingRootPath);
        PruneToSizeLimit(remaining, pruned, limits, incomingSizeBytes);

        return ScanHistoryRetentionDecision.Accept(pruned);
    }

    public static IReadOnlyList<ScanSnapshotDescriptor> DecideForLimitChange(
        IReadOnlyList<ScanSnapshotDescriptor> existing,
        ScanHistoryLimits limits)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(limits);

        var remaining = Ordered(existing).ToList();
        var pruned = new List<ScanSnapshotDescriptor>();

        PruneToCountLimit(remaining, pruned, limits, incomingRootPath: null);
        PruneToSizeLimit(remaining, pruned, limits, incomingSizeBytes: 0);

        return pruned;
    }

    private static void PruneToCountLimit(
        List<ScanSnapshotDescriptor> remaining,
        List<ScanSnapshotDescriptor> pruned,
        ScanHistoryLimits limits,
        string? incomingRootPath)
    {
        var roots = remaining
            .Select(snapshot => snapshot.RootPath)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (incomingRootPath is not null
            && !roots.Contains(incomingRootPath, StringComparer.Ordinal))
        {
            roots.Add(incomingRootPath);
        }

        foreach (var root in roots)
        {
            var inRoot = remaining
                .Where(snapshot => string.Equals(
                    snapshot.RootPath,
                    root,
                    StringComparison.Ordinal))
                .ToList();

            var projectedCount = inRoot.Count
                + (string.Equals(root, incomingRootPath, StringComparison.Ordinal)
                    ? 1
                    : 0);

            var index = 0;

            while (projectedCount > limits.MaxSnapshotsPerRoot && index < inRoot.Count)
            {
                Prune(remaining, pruned, inRoot[index]);
                index++;
                projectedCount--;
            }
        }
    }

    private static void PruneToSizeLimit(
        List<ScanSnapshotDescriptor> remaining,
        List<ScanSnapshotDescriptor> pruned,
        ScanHistoryLimits limits,
        long incomingSizeBytes)
    {
        var total = remaining.Sum(snapshot => snapshot.StoredSizeBytes)
            + incomingSizeBytes;

        while (total > limits.MaxTotalSizeBytes && remaining.Count > 0)
        {
            var oldest = remaining[0];
            total -= oldest.StoredSizeBytes;
            Prune(remaining, pruned, oldest);
        }
    }

    private static void Prune(
        List<ScanSnapshotDescriptor> remaining,
        List<ScanSnapshotDescriptor> pruned,
        ScanSnapshotDescriptor snapshot)
    {
        remaining.Remove(snapshot);
        pruned.Add(snapshot);
    }

    private static IEnumerable<ScanSnapshotDescriptor> Ordered(
        IReadOnlyList<ScanSnapshotDescriptor> existing) =>
        existing
            .OrderBy(snapshot => snapshot.ScanCompletedAt)
            .ThenBy(snapshot => snapshot.SnapshotId, StringComparer.Ordinal);
}
