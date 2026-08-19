using System;
using System.Collections.Generic;

namespace MacStorageAtlas.App.ViewModels;

public sealed class ScanHistoryRootViewModel
{
    public ScanHistoryRootViewModel(
        string rootPath,
        IReadOnlyList<ScanHistoryEntryViewModel> snapshots)
    {
        ArgumentNullException.ThrowIfNull(snapshots);

        RootPath = rootPath;
        Snapshots = snapshots;
    }

    public string RootPath { get; }

    public IReadOnlyList<ScanHistoryEntryViewModel> Snapshots { get; }

    public string Header => string.IsNullOrEmpty(RootPath)
        ? "Unreadable snapshots"
        : RootPath;
}
