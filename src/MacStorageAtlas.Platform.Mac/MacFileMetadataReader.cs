using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.InteropServices;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Platform.Mac;

public sealed class MacFileMetadataReader : IAllocatedFileMetadataReader
{
    private const ulong SharesAllBlocks = 0x00000040;
    private readonly IMacFileMetadataNative _native;
    private readonly ConcurrentDictionary<MacVolumeIdentity, CloneCapability>
        _cloneCapabilities = new();

    public MacFileMetadataReader()
        : this(new MacFileMetadataNative())
    {
    }

    internal MacFileMetadataReader(IMacFileMetadataNative native)
    {
        _native = native ?? throw new ArgumentNullException(nameof(native));
    }

    public AllocatedFileMetadata Read(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException(
                "Allocated file metadata is available only on macOS.");
        }

        var volume = _native.TryGetVolume(path);
        if (volume is null)
        {
            return ReadFallback(path, CloneAccountingCoverage.Partial);
        }

        var capability = _cloneCapabilities.GetOrAdd(
            volume.Value.Identity,
            _ => _native.ProbeCloneCapability(volume.Value.MountPoint));

        return capability switch
        {
            CloneCapability.Supported => ReadExtendedOrFallback(path),
            CloneCapability.Unsupported => ReadFallback(
                path,
                CloneAccountingCoverage.Unavailable),
            CloneCapability.Degraded => ReadFallback(
                path,
                CloneAccountingCoverage.Partial),
            _ => throw new ArgumentOutOfRangeException(
                nameof(capability),
                capability,
                null)
        };
    }

    private AllocatedFileMetadata ReadExtendedOrFallback(string path)
    {
        var extended = _native.TryReadExtended(path);
        if (extended is null || !extended.Value.HasRequiredMetadata)
        {
            return ReadFallback(path, CloneAccountingCoverage.Partial);
        }

        var value = extended.Value;
        if (!value.HasCloneMetadata)
        {
            return new AllocatedFileMetadata(
                value.AllocatedSizeBytes,
                new FileIdentity(value.DeviceId, value.FileId),
                value.LinkCount,
                value.HasDataAllocatedSize ? value.DataAllocatedSizeBytes : null,
                CloneAccountingCoverage: CloneAccountingCoverage.Partial);
        }

        SharedDataIdentity? sharedDataIdentity =
            value.CloneReferenceCount > 1
            && value.CloneId != 0
            && (value.ExtendedFlags & SharesAllBlocks) != 0
                ? new SharedDataIdentity(value.DeviceId, value.CloneId)
                : null;

        return new AllocatedFileMetadata(
            value.AllocatedSizeBytes,
            new FileIdentity(value.DeviceId, value.FileId),
            value.LinkCount,
            value.DataAllocatedSizeBytes,
            sharedDataIdentity,
            CloneAccountingCoverage.Available);
    }

    private AllocatedFileMetadata ReadFallback(
        string path,
        CloneAccountingCoverage coverage)
    {
        var fallback = _native.ReadFallback(path);
        return new AllocatedFileMetadata(
            fallback.AllocatedSizeBytes,
            new FileIdentity(fallback.DeviceId, fallback.FileId),
            fallback.LinkCount,
            CloneAccountingCoverage: coverage);
    }
}

internal interface IMacFileMetadataNative
{
    MacVolumeInfo? TryGetVolume(string path);

    CloneCapability ProbeCloneCapability(string mountPoint);

    MacExtendedFileMetadata? TryReadExtended(string path);

    MacFallbackFileMetadata ReadFallback(string path);
}

internal enum CloneCapability
{
    Unsupported,

    Supported,

    Degraded
}

internal readonly record struct MacVolumeIdentity(int First, int Second);

internal readonly record struct MacVolumeInfo(
    MacVolumeIdentity Identity,
    string MountPoint);

internal readonly record struct MacFallbackFileMetadata(
    long AllocatedSizeBytes,
    ulong DeviceId,
    ulong FileId,
    uint LinkCount);

internal readonly record struct MacExtendedFileMetadata(
    bool HasRequiredMetadata,
    bool HasDataAllocatedSize,
    bool HasCloneId,
    bool HasExtendedFlags,
    bool HasCloneReferenceCount,
    long AllocatedSizeBytes,
    long DataAllocatedSizeBytes,
    ulong DeviceId,
    ulong FileId,
    uint LinkCount,
    ulong CloneId,
    ulong ExtendedFlags,
    uint CloneReferenceCount)
{
    public bool HasCloneMetadata =>
        HasDataAllocatedSize
        && HasCloneId
        && HasExtendedFlags
        && HasCloneReferenceCount;
}

internal sealed class MacFileMetadataNative : IMacFileMetadataNative
{
    private const long BlockSizeBytes = 512;
    private const ushort AttributeBitmapCount = 5;
    private const uint CommonDeviceId = 0x00000002;
    private const uint CommonFileId = 0x02000000;
    private const uint CommonReturnedAttributes = 0x80000000;
    private const uint VolumeCapabilities = 0x00020000;
    private const uint VolumeInformation = 0x80000000;
    private const uint FileLinkCount = 0x00000001;
    private const uint FileAllocationSize = 0x00000004;
    private const uint FileDataAllocationSize = 0x00000400;
    private const uint CloneId = 0x00000100;
    private const uint ExtendedFlags = 0x00000200;
    private const uint CloneReferenceCount = 0x00001000;
    private const uint CloneMappingCapability = 0x04000000;
    private const uint ExtendedCommonAttributes = 0x00000020;
    private const int ExtendedBufferSize = 128;
    private const int VolumeBufferSize = 36;

    public MacVolumeInfo? TryGetVolume(string path)
    {
        var result = RuntimeInformation.ProcessArchitecture == Architecture.X64
            ? statfs_x64(path, out var buffer)
            : statfs_arm64(path, out buffer);

        if (result != 0 || string.IsNullOrWhiteSpace(buffer.MountPoint))
        {
            return null;
        }

        return new MacVolumeInfo(
            new MacVolumeIdentity(buffer.FileSystemIdFirst, buffer.FileSystemIdSecond),
            buffer.MountPoint);
    }

    public CloneCapability ProbeCloneCapability(string mountPoint)
    {
        if (!OperatingSystem.IsMacOSVersionAtLeast(14))
        {
            return CloneCapability.Unsupported;
        }

        var attributes = new MacAttributeList
        {
            BitmapCount = AttributeBitmapCount,
            VolumeAttributes = VolumeInformation | VolumeCapabilities
        };
        var buffer = new byte[VolumeBufferSize];
        if (getattrlist(
                mountPoint,
                in attributes,
                buffer,
                (nuint)buffer.Length,
                ExtendedCommonAttributes) != 0)
        {
            return CloneCapability.Degraded;
        }

        if (buffer.Length < VolumeBufferSize)
        {
            return CloneCapability.Degraded;
        }

        var returnedLength = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        if (returnedLength < VolumeBufferSize || returnedLength > buffer.Length)
        {
            return CloneCapability.Degraded;
        }

        var capability = BinaryPrimitives.ReadUInt32LittleEndian(
            buffer.AsSpan(4, sizeof(uint)));
        var valid = BinaryPrimitives.ReadUInt32LittleEndian(
            buffer.AsSpan(20, sizeof(uint)));

        return (valid & CloneMappingCapability) != 0
               && (capability & CloneMappingCapability) != 0
            ? CloneCapability.Supported
            : CloneCapability.Unsupported;
    }

    public MacExtendedFileMetadata? TryReadExtended(string path)
    {
        var attributes = new MacAttributeList
        {
            BitmapCount = AttributeBitmapCount,
            CommonAttributes =
                CommonReturnedAttributes | CommonDeviceId | CommonFileId,
            FileAttributes =
                FileLinkCount | FileAllocationSize | FileDataAllocationSize,
            ForkAttributes = CloneId | ExtendedFlags | CloneReferenceCount
        };
        var buffer = new byte[ExtendedBufferSize];
        if (getattrlist(
                path,
                in attributes,
                buffer,
                (nuint)buffer.Length,
                ExtendedCommonAttributes) != 0)
        {
            return null;
        }

        return TryParseExtendedBuffer(buffer, out var metadata)
            ? metadata
            : null;
    }

    public MacFallbackFileMetadata ReadFallback(string path)
    {
        var result = RuntimeInformation.ProcessArchitecture == Architecture.X64
            ? stat_x64(path, out var buffer)
            : stat_arm64(path, out buffer);

        if (result != 0)
        {
            var errorCode = Marshal.GetLastPInvokeError();
            throw new IOException(
                $"Could not read allocated file metadata for '{path}'.",
                new Win32Exception(errorCode));
        }

        return new MacFallbackFileMetadata(
            buffer.BlockCount * BlockSizeBytes,
            unchecked((uint)buffer.DeviceId),
            buffer.FileId,
            buffer.LinkCount);
    }

    internal static bool TryParseExtendedBuffer(
        byte[] buffer,
        out MacExtendedFileMetadata metadata)
    {
        metadata = default;
        if (buffer.Length < 24)
        {
            return false;
        }

        var returnedLength = BinaryPrimitives.ReadUInt32LittleEndian(buffer);
        if (returnedLength < 24 || returnedLength > buffer.Length)
        {
            return false;
        }

        var commonAttributes = BinaryPrimitives.ReadUInt32LittleEndian(
            buffer.AsSpan(4, sizeof(uint)));
        var fileAttributes = BinaryPrimitives.ReadUInt32LittleEndian(
            buffer.AsSpan(16, sizeof(uint)));
        var forkAttributes = BinaryPrimitives.ReadUInt32LittleEndian(
            buffer.AsSpan(20, sizeof(uint)));
        var offset = 24;

        var hasDeviceId = TryReadUInt32(
            buffer,
            returnedLength,
            ref offset,
            (commonAttributes & CommonDeviceId) != 0,
            out var deviceId);
        var hasFileId = TryReadUInt64(
            buffer,
            returnedLength,
            ref offset,
            (commonAttributes & CommonFileId) != 0,
            out var fileId);
        var hasLinkCount = TryReadUInt32(
            buffer,
            returnedLength,
            ref offset,
            (fileAttributes & FileLinkCount) != 0,
            out var linkCount);
        var hasAllocationSize = TryReadInt64(
            buffer,
            returnedLength,
            ref offset,
            (fileAttributes & FileAllocationSize) != 0,
            out var allocatedSize);
        var hasDataAllocationSize = TryReadInt64(
            buffer,
            returnedLength,
            ref offset,
            (fileAttributes & FileDataAllocationSize) != 0,
            out var dataAllocatedSize);
        var hasCloneId = TryReadUInt64(
            buffer,
            returnedLength,
            ref offset,
            (forkAttributes & CloneId) != 0,
            out var cloneId);
        var hasExtendedFlags = TryReadUInt64(
            buffer,
            returnedLength,
            ref offset,
            (forkAttributes & ExtendedFlags) != 0,
            out var extendedFlags);
        var hasCloneReferenceCount = TryReadUInt32(
            buffer,
            returnedLength,
            ref offset,
            (forkAttributes & CloneReferenceCount) != 0,
            out var cloneReferenceCount);

        if (offset > returnedLength)
        {
            return false;
        }

        metadata = new MacExtendedFileMetadata(
            hasDeviceId && hasFileId && hasLinkCount && hasAllocationSize,
            hasDataAllocationSize,
            hasCloneId,
            hasExtendedFlags,
            hasCloneReferenceCount,
            allocatedSize,
            dataAllocatedSize,
            deviceId,
            fileId,
            linkCount,
            cloneId,
            extendedFlags,
            cloneReferenceCount);
        return true;
    }

    private static bool TryReadUInt32(
        byte[] buffer,
        uint returnedLength,
        ref int offset,
        bool isReturned,
        out uint value)
    {
        value = 0;
        if (!isReturned)
        {
            return false;
        }

        if (offset + sizeof(uint) > returnedLength)
        {
            offset = checked((int)returnedLength + 1);
            return false;
        }

        value = BinaryPrimitives.ReadUInt32LittleEndian(
            buffer.AsSpan(offset, sizeof(uint)));
        offset += sizeof(uint);
        return true;
    }

    private static bool TryReadUInt64(
        byte[] buffer,
        uint returnedLength,
        ref int offset,
        bool isReturned,
        out ulong value)
    {
        value = 0;
        if (!isReturned)
        {
            return false;
        }

        if (offset + sizeof(ulong) > returnedLength)
        {
            offset = checked((int)returnedLength + 1);
            return false;
        }

        value = BinaryPrimitives.ReadUInt64LittleEndian(
            buffer.AsSpan(offset, sizeof(ulong)));
        offset += sizeof(ulong);
        return true;
    }

    private static bool TryReadInt64(
        byte[] buffer,
        uint returnedLength,
        ref int offset,
        bool isReturned,
        out long value)
    {
        value = 0;
        if (!isReturned)
        {
            return false;
        }

        if (offset + sizeof(long) > returnedLength)
        {
            offset = checked((int)returnedLength + 1);
            return false;
        }

        value = BinaryPrimitives.ReadInt64LittleEndian(
            buffer.AsSpan(offset, sizeof(long)));
        offset += sizeof(long);
        return true;
    }

    [DllImport("libc", EntryPoint = "getattrlist", SetLastError = true)]
    private static extern int getattrlist(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        in MacAttributeList attributes,
        [Out] byte[] attributeBuffer,
        nuint attributeBufferSize,
        uint options);

    [DllImport("libc", EntryPoint = "stat", SetLastError = true)]
    private static extern int stat_arm64(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        out MacStat buffer);

    [DllImport("libc", EntryPoint = "stat$INODE64", SetLastError = true)]
    private static extern int stat_x64(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        out MacStat buffer);

    [DllImport("libc", EntryPoint = "statfs", SetLastError = true)]
    private static extern int statfs_arm64(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        out MacStatFs buffer);

    [DllImport("libc", EntryPoint = "statfs$INODE64", SetLastError = true)]
    private static extern int statfs_x64(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        out MacStatFs buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MacAttributeList
    {
        public ushort BitmapCount;
        public ushort Reserved;
        public uint CommonAttributes;
        public uint VolumeAttributes;
        public uint DirectoryAttributes;
        public uint FileAttributes;
        public uint ForkAttributes;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MacStat
    {
        public int DeviceId;
        public ushort Mode;
        public ushort LinkCount;
        public ulong FileId;
        public uint UserId;
        public uint GroupId;
        public int SpecialDeviceId;
        public long AccessTime;
        public long AccessTimeNanoseconds;
        public long ModificationTime;
        public long ModificationTimeNanoseconds;
        public long ChangeTime;
        public long ChangeTimeNanoseconds;
        public long BirthTime;
        public long BirthTimeNanoseconds;
        public long Size;
        public long BlockCount;
        public int BlockSize;
        public uint Flags;
        public uint Generation;
        public int Spare;
        public long QuadSpare0;
        public long QuadSpare1;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    private struct MacStatFs
    {
        public uint BlockSize;
        public int IoSize;
        public ulong Blocks;
        public ulong BlocksFree;
        public ulong BlocksAvailable;
        public ulong Files;
        public ulong FilesFree;
        public int FileSystemIdFirst;
        public int FileSystemIdSecond;
        public uint Owner;
        public uint Type;
        public uint Flags;
        public uint Subtype;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 16)]
        public string FileSystemType;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
        public string MountPoint;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 1024)]
        public string MountedFrom;

        public uint ExtendedFlags;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 7)]
        public uint[] Reserved;
    }
}
