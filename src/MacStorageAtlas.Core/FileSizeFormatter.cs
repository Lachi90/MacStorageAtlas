using System.Globalization;

namespace MacStorageAtlas.Core;

public static class FileSizeFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string Format(long sizeBytes)
    {
        var value = (double)sizeBytes;
        var unitIndex = 0;

        while (Math.Abs(value) >= 1024 && unitIndex < Units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? string.Format(CultureInfo.CurrentCulture, "{0:N0} {1}", value, Units[unitIndex])
            : string.Format(CultureInfo.CurrentCulture, "{0:N1} {1}", value, Units[unitIndex]);
    }
}
