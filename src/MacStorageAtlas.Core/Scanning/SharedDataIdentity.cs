namespace MacStorageAtlas.Core.Scanning;

public readonly record struct SharedDataIdentity(
    ulong VolumeId,
    ulong DataStreamId);
