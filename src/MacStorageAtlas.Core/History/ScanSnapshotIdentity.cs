using System.Globalization;

namespace MacStorageAtlas.Core.History;

public static class ScanSnapshotIdentity
{
    private const string TimestampFormat = "yyyyMMdd'T'HHmmss'Z'";

    public static string Create(DateTimeOffset scanCompletedAt)
    {
        var timestamp = scanCompletedAt
            .ToUniversalTime()
            .ToString(TimestampFormat, CultureInfo.InvariantCulture);

        return $"{timestamp}-{Guid.NewGuid():N}"[..(timestamp.Length + 9)];
    }

    public static bool IsValid(string? snapshotId) =>
        !string.IsNullOrWhiteSpace(snapshotId)
        && snapshotId.All(character =>
            char.IsAsciiLetterOrDigit(character) || character is '-');
}
