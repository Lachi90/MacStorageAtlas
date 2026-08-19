namespace MacStorageAtlas.Core.Insights;

public enum DuplicateSkipReason
{
    UniqueLogicalLength,

    ZeroLength,

    Missing,

    ReadFailed,

    Changed,

    ContentsNotLocal
}
