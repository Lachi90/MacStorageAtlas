namespace MacStorageAtlas.Core;

public sealed record ScanOptions
{
    public static ScanOptions Default { get; } = new();

    public bool IncludeHiddenFiles { get; init; }

    public bool FollowSymbolicLinks { get; init; }

    public bool TreatPackagesAsDirectories { get; init; } = true;

    /// <summary>
    /// When <see langword="true"/>, files are measured by the space actually
    /// allocated on disk (macOS: <c>st_blocks × 512</c>) instead of their logical
    /// length. This makes cloud placeholders that are not downloaded locally
    /// (iCloud Drive, OneDrive, kDrive, …) count as roughly zero, matching the
    /// numbers reported by the Finder and <c>du</c>.
    /// The default is <see langword="false"/> (logical length) so the core library
    /// stays portable and deterministic; the application enables it by default.
    /// </summary>
    public bool MeasureAllocatedSize { get; init; }
}
