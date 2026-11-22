namespace MacStorageAtlas.Core.Scanning;

public sealed record ScanResultProvenance
{
    public ScanResultProvenance(
        DateTimeOffset scanCompletedAt,
        IReadOnlyList<RefreshedFolder>? refreshedFolders = null)
    {
        ScanCompletedAt = scanCompletedAt;
        RefreshedFolders = refreshedFolders?.ToArray() ?? [];
    }

    public DateTimeOffset ScanCompletedAt { get; }

    public IReadOnlyList<RefreshedFolder> RefreshedFolders { get; }

    public bool HasRefreshedFolders => RefreshedFolders.Count > 0;

    public static ScanResultProvenance ForFullScan(DateTimeOffset scanCompletedAt) =>
        new(scanCompletedAt);

    public ScanResultProvenance WithRefreshedFolder(
        string folderPath,
        DateTimeOffset completedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        var normalizedPath = ScanPath.Normalize(folderPath);
        var kept = RefreshedFolders
            .Where(folder => !ScanPath.IsWithinNormalized(folder.Path, normalizedPath));

        return new ScanResultProvenance(
            ScanCompletedAt,
            kept.Append(new RefreshedFolder(normalizedPath, completedAt)).ToArray());
    }
}
