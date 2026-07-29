namespace MacStorageAtlas.Core;

public readonly record struct DiskItemMetadata(
    DiskItemKind Kind,
    FileAttributes? Attributes,
    DateTimeOffset? CreatedTimeUtc,
    DateTimeOffset? ModifiedTimeUtc,
    DateTimeOffset? LastAccessTimeUtc)
{
    public static DiskItemMetadata Unknown(DiskItemKind kind) =>
        new(
            kind,
            Attributes: null,
            CreatedTimeUtc: null,
            ModifiedTimeUtc: null,
            LastAccessTimeUtc: null);
}
