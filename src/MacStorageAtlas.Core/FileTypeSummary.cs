namespace MacStorageAtlas.Core;

public sealed record FileTypeSummary(
    string Extension,
    long FileCount,
    long TotalSizeBytes)
{
    public string FormattedSize => FileSizeFormatter.Format(TotalSizeBytes);
}
