using System.Collections.Generic;

namespace MacStorageAtlas.App.Models;

public sealed class AppSettings
{
    public const int MaxRecentLocations = 10;

    public bool IncludeHiddenFiles { get; set; }

    public bool FollowSymbolicLinks { get; set; }

    public bool TreatPackagesAsDirectories { get; set; } = true;

    // Default to the real on-disk footprint so undownloaded cloud placeholders
    // (iCloud, OneDrive, kDrive) are not counted at their full logical size.
    public bool MeasureAllocatedSize { get; set; } = true;

    public List<string> RecentLocations { get; set; } = [];
}
