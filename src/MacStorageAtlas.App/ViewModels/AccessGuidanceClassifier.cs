using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MacStorageAtlas.Core.Access;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.App.ViewModels;

internal sealed class AccessGuidanceClassifier
{
    public AccessGuidance Classify(
        IReadOnlyList<ScanError> errors,
        FullDiskAccessAssessment assessment)
    {
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(assessment);

        var inaccessiblePathCount = errors
            .Where(IsPermissionRelated)
            .Select(error => error.Path)
            .Distinct(StringComparer.Ordinal)
            .Count();

        if (inaccessiblePathCount > 0)
        {
            return assessment.Status == FullDiskAccessStatus.LikelyMissing
                ? new AccessGuidance(
                    AccessGuidanceStatus.LikelyMissingFullDiskAccess,
                    inaccessiblePathCount)
                : new AccessGuidance(
                    AccessGuidanceStatus.IncompleteScan,
                    inaccessiblePathCount);
        }

        return assessment.Status == FullDiskAccessStatus.Indeterminate
            ? new AccessGuidance(AccessGuidanceStatus.Indeterminate, InaccessiblePathCount: 0)
            : AccessGuidance.None;
    }

    private static bool IsPermissionRelated(ScanError error) =>
        string.Equals(
            error.ExceptionType,
            nameof(UnauthorizedAccessException),
            StringComparison.Ordinal)
        || (string.Equals(error.ExceptionType, nameof(IOException), StringComparison.Ordinal)
            && IsPermissionMessage(error.Message));

    private static bool IsPermissionMessage(string message) =>
        message.Contains("operation not permitted", StringComparison.OrdinalIgnoreCase)
        || message.Contains("permission denied", StringComparison.OrdinalIgnoreCase)
        || message.Contains("access denied", StringComparison.OrdinalIgnoreCase);
}
