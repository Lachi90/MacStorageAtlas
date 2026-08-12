namespace MacStorageAtlas.Core.Relocation;

public readonly record struct RelocationFreeSpace(bool IsKnown, long AvailableBytes)
{
    public static RelocationFreeSpace Unknown { get; } = new(false, 0);

    public static RelocationFreeSpace FromBytes(long availableBytes) =>
        new(true, availableBytes);
}
