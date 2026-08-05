using System;
using System.Collections.Generic;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.ViewModels;

public static class ScanCompletenessClassifier
{
    public static ScanCompleteness Classify(
        AccessGuidance guidance,
        IReadOnlyList<ScanError> errors)
    {
        ArgumentNullException.ThrowIfNull(guidance);
        ArgumentNullException.ThrowIfNull(errors);

        return guidance.Status switch
        {
            AccessGuidanceStatus.LikelyMissingFullDiskAccess =>
                ScanCompleteness.IncompleteAccessRestricted,
            AccessGuidanceStatus.Indeterminate => ScanCompleteness.Undetermined,
            AccessGuidanceStatus.IncompleteScan =>
                ScanCompleteness.IncompleteRecoverableErrors,
            _ => errors.Count > 0
                ? ScanCompleteness.IncompleteRecoverableErrors
                : ScanCompleteness.Complete
        };
    }
}
