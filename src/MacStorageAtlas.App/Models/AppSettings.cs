using System.Collections.Generic;

namespace MacStorageAtlas.App.Models;

public sealed class AppSettings
{
    public const int MaxRecentLocations = 10;

    public bool IncludeHiddenFiles { get; set; }

    public bool FollowSymbolicLinks { get; set; }

    public bool TreatPackagesAsDirectories { get; set; } = true;

    public List<string> RecentLocations { get; set; } = [];
}
