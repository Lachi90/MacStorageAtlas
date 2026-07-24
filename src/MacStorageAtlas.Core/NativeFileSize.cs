using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace MacStorageAtlas.Core;

internal static class NativeFileSize
{
    private const long BlockSizeBytes = 512;

    public static long GetAllocatedSizeBytes(string path)
    {
        if (OperatingSystem.IsMacOS())
        {
            if (TryGetAllocatedSizeMac(path, out var allocated, out var errorCode))
            {
                return allocated;
            }

            throw new IOException(
                $"Could not read allocated size metadata for '{path}'.",
                new Win32Exception(errorCode));
        }

        return new FileInfo(path).Length;
    }

    private static bool TryGetAllocatedSizeMac(
        string path,
        out long allocatedBytes,
        out int errorCode)
    {
        allocatedBytes = 0;

        var result = RuntimeInformation.ProcessArchitecture == Architecture.X64
            ? stat_x64(path, out var buffer)
            : stat_arm64(path, out buffer);

        if (result != 0)
        {
            errorCode = Marshal.GetLastPInvokeError();
            return false;
        }

        errorCode = 0;
        allocatedBytes = buffer.st_blocks * BlockSizeBytes;
        return true;
    }

    [DllImport("libc", EntryPoint = "stat", SetLastError = true)]
    private static extern int stat_arm64(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        out MacStat buffer);

    [DllImport("libc", EntryPoint = "stat$INODE64", SetLastError = true)]
    private static extern int stat_x64(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        out MacStat buffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct MacStat
    {
        public int st_dev;
        public ushort st_mode;
        public ushort st_nlink;
        public ulong st_ino;
        public uint st_uid;
        public uint st_gid;
        public int st_rdev;
        public long st_atime;
        public long st_atime_nsec;
        public long st_mtime;
        public long st_mtime_nsec;
        public long st_ctime;
        public long st_ctime_nsec;
        public long st_birthtime;
        public long st_birthtime_nsec;
        public long st_size;
        public long st_blocks;
        public int st_blksize;
        public uint st_flags;
        public uint st_gen;
        public int st_lspare;
        public long st_qspare0;
        public long st_qspare1;
    }
}
