using System.IO;
using System.Runtime.InteropServices;

namespace MacStorageAtlas.Core;

/// <summary>
/// Reads the number of bytes a file actually occupies on disk. On macOS this is
/// <c>st_blocks × 512</c> from <c>stat(2)</c>, which reports the real allocated
/// storage — so sparse files and undownloaded cloud placeholders (iCloud Drive,
/// OneDrive, kDrive, …) count as roughly zero instead of their logical size.
/// On other platforms it falls back to the logical file length.
/// </summary>
internal static class NativeFileSize
{
    // st_blocks is always counted in 512-byte units, independent of the
    // filesystem's own block size (see the stat(2) man page).
    private const long BlockSizeBytes = 512;

    public static long GetAllocatedSizeBytes(string path)
    {
        if (OperatingSystem.IsMacOS() && TryGetAllocatedSizeMac(path, out var allocated))
        {
            return allocated;
        }

        // Fallback: platforms without a native implementation (or a stat failure)
        // report the logical length. Any IO/permission failure surfaces here and
        // is handled by the scanner's recoverable-error path.
        return new FileInfo(path).Length;
    }

    private static bool TryGetAllocatedSizeMac(string path, out long allocatedBytes)
    {
        allocatedBytes = 0;

        var result = RuntimeInformation.ProcessArchitecture == Architecture.X64
            ? stat_x64(path, out var buffer)
            : stat_arm64(path, out buffer);

        if (result != 0)
        {
            return false;
        }

        allocatedBytes = buffer.st_blocks * BlockSizeBytes;
        return true;
    }

    // On Apple Silicon the exported symbol is the 64-bit-inode "stat".
    [DllImport("libc", EntryPoint = "stat", SetLastError = true)]
    private static extern int stat_arm64(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        out MacStat buffer);

    // On x86_64 the C "stat" macro maps to the "$INODE64" variant, whose struct
    // layout matches <see cref="MacStat"/>. The legacy plain "stat" has a
    // different layout and must not be used here.
    [DllImport("libc", EntryPoint = "stat$INODE64", SetLastError = true)]
    private static extern int stat_x64(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        out MacStat buffer);

    // Mirrors "struct stat" for _DARWIN_FEATURE_64_BIT_INODE (sys/stat.h).
    // Only st_blocks is consumed, but every field is declared so the sequential
    // layout — and therefore the st_blocks offset — matches the native struct.
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
