using System;
using System.Collections.Generic;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.App.ViewModels;

public static class ScanCompletenessClassifier
{
    public static ScanCompleteness Classify(
        AccessGuidance guidance,
        IReadOnlyList<ScanError> errors) =>
        Classify(guidance, errors, []);

    public static ScanCompleteness Classify(
        AccessGuidance guidance,
        IReadOnlyList<ScanError> errors,
        IReadOnlyList<string> unlistedCloudFolders)
    {
        ArgumentNullException.ThrowIfNull(guidance);
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(unlistedCloudFolders);

        return guidance.Status switch
        {
            AccessGuidanceStatus.LikelyMissingFullDiskAccess =>
                ScanCompleteness.IncompleteAccessRestricted,
            AccessGuidanceStatus.SandboxedSelectionRequired =>
                ScanCompleteness.IncompleteAccessRestricted,
            AccessGuidanceStatus.IncompleteScan =>
                ScanCompleteness.IncompleteRecoverableErrors,
            _ when errors.Count > 0 => ScanCompleteness.IncompleteRecoverableErrors,
            _ when unlistedCloudFolders.Count > 0 =>
                ScanCompleteness.IncompleteCloudFoldersNotListed,
            _ => ScanCompleteness.Complete
        };
    }
}
