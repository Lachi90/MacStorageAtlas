using System.ComponentModel;
using System.Runtime.InteropServices;

namespace MacStorageAtlas.Benchmarks;

public static class RepresentativeFixtureGenerator
{
    public static async Task<BenchmarkFixtureInfo> CreateAsync(
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        var root = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(root);
        var limitations = new List<string>();

        var ordinaryDirectory = Directory.CreateDirectory(Path.Combine(root, "ordinary"));
        await File.WriteAllBytesAsync(
            Path.Combine(ordinaryDirectory.FullName, "small.txt"),
            new byte[4096],
            cancellationToken);
        await File.WriteAllBytesAsync(
            Path.Combine(ordinaryDirectory.FullName, "medium.bin"),
            new byte[16384],
            cancellationToken);

        var sparsePath = Path.Combine(root, "sparse.bin");
        await using (var sparse = new FileStream(
                         sparsePath,
                         FileMode.Create,
                         FileAccess.Write,
                         FileShare.None,
                         1,
                         useAsync: true))
        {
            sparse.SetLength(8L * 1024 * 1024);
        }

        var packageContents = Directory.CreateDirectory(
            Path.Combine(root, "Example.app", "Contents", "Resources"));
        await File.WriteAllBytesAsync(
            Path.Combine(packageContents.FullName, "payload.dat"),
            new byte[8192],
            cancellationToken);

        var hardlinkTarget = Path.Combine(root, "hardlink-target.bin");
        await File.WriteAllBytesAsync(hardlinkTarget, new byte[4096], cancellationToken);
        var hardlinkCount = TryCreateHardLink(
            Path.Combine(root, "hardlink-alias.bin"),
            hardlinkTarget,
            limitations)
            ? 1
            : 0;

        var symbolicLinkCount = TryCreateSymbolicLink(
            Path.Combine(root, "ordinary-link.txt"),
            Path.Combine(ordinaryDirectory.FullName, "small.txt"),
            limitations)
            ? 1
            : 0;

        return new BenchmarkFixtureInfo(
            BenchmarkFixtureKind.Representative,
            root,
            "Representative scan fixture with ordinary, sparse, hardlink, symbolic-link, and package shapes where supported",
            IsRealFileSystem: true,
            OrdinaryFileCount: 4,
            SparseFileCount: 1,
            HardlinkCount: hardlinkCount,
            SymbolicLinkCount: symbolicLinkCount,
            PackageCount: 1,
            SyntheticFileCount: null,
            Limitations: limitations);
    }

    private static bool TryCreateHardLink(
        string linkPath,
        string targetPath,
        ICollection<string> limitations)
    {
        try
        {
            if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux())
            {
                throw new PlatformNotSupportedException(
                    "Hardlink fixture creation is supported only on Unix-like platforms.");
            }

            if (link(targetPath, linkPath) != 0)
            {
                var errorCode = Marshal.GetLastPInvokeError();
                throw new IOException(
                    "Hardlink fixture creation failed.",
                    new Win32Exception(errorCode));
            }

            return true;
        }
        catch (Exception exception) when (IsFixtureLimitation(exception))
        {
            limitations.Add($"Hardlink fixture unavailable: {exception.GetType().Name}: {exception.Message}");
            return false;
        }
    }

    private static bool TryCreateSymbolicLink(
        string linkPath,
        string targetPath,
        ICollection<string> limitations)
    {
        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception exception) when (IsFixtureLimitation(exception))
        {
            limitations.Add($"Symbolic-link fixture unavailable: {exception.GetType().Name}: {exception.Message}");
            return false;
        }
    }

    private static bool IsFixtureLimitation(Exception exception) =>
        exception is IOException
            or UnauthorizedAccessException
            or PlatformNotSupportedException
            or NotSupportedException;

    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int link(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string existingPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string newPath);
}
