namespace MacStorageAtlas.Core;

public readonly record struct SharedDataIdentity(
    ulong VolumeId,
    ulong DataStreamId);
