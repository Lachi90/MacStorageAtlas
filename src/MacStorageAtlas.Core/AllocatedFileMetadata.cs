namespace MacStorageAtlas.Core;

public readonly record struct AllocatedFileMetadata(
    long AllocatedSizeBytes,
    FileIdentity Identity,
    uint LinkCount,
    long? DataAllocatedSizeBytes = null,
    SharedDataIdentity? SharedDataIdentity = null,
    CloneAccountingCoverage CloneAccountingCoverage =
        CloneAccountingCoverage.Unavailable);
