namespace MacStorageAtlas.Core;

public readonly record struct AllocatedFileMetadata(
    long AllocatedSizeBytes,
    FileIdentity Identity,
    uint LinkCount);
