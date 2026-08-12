using System.Globalization;
using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.Core.Export;

internal static class ScanExportValues
{
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'";

    public static string Timestamp(DateTimeOffset? value) =>
        value is { } instant
            ? instant.ToUniversalTime().ToString(TimestampFormat, CultureInfo.InvariantCulture)
            : string.Empty;

    public static DateTimeOffset? ParseTimestamp(string? value) =>
        string.IsNullOrEmpty(value)
            ? null
            : DateTimeOffset.ParseExact(
                value,
                TimestampFormat,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    public static string Number(long value) =>
        value.ToString(CultureInfo.InvariantCulture);

    public static string Number(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    public static string Boolean(bool value) => value ? "true" : "false";

    public static string Category(FileCategory? value) =>
        value is { } category ? category.ToString() : string.Empty;

    public static FileCategory? ParseCategory(string? value) =>
        string.IsNullOrEmpty(value) ? null : Enum.Parse<FileCategory>(value);
}
