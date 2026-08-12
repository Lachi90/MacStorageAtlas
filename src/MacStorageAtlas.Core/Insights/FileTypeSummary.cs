using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.Core.Insights;

public sealed record FileTypeSummary(
    string Extension,
    long FileCount,
    long TotalSizeBytes)
{
    public string FormattedSize => FileSizeFormatter.Format(TotalSizeBytes);
}
