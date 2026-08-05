using System.Diagnostics;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.Platform.Mac;

public sealed class MacItemRelocationService : IItemRelocationService
{
    private readonly Func<string, string, bool> _fastMove;
    private readonly Func<string, string, bool> _verifyCopy;

    public MacItemRelocationService()
        : this(TryFastMove, IsCopyVerified)
    {
    }

    internal MacItemRelocationService(Func<string, string, bool> fastMove)
        : this(fastMove, IsCopyVerified)
    {
    }

    internal MacItemRelocationService(
        Func<string, string, bool> fastMove,
        Func<string, string, bool> verifyCopy)
    {
        ArgumentNullException.ThrowIfNull(fastMove);
        ArgumentNullException.ThrowIfNull(verifyCopy);

        _fastMove = fastMove;
        _verifyCopy = verifyCopy;
    }

    public async Task MoveAsync(
        string sourcePath,
        string destinationDirectoryPath,
        CancellationToken cancellationToken = default)
    {
        var destinationPath = ResolveDestinationPath(sourcePath, destinationDirectoryPath);
        cancellationToken.ThrowIfCancellationRequested();

        if (_fastMove(sourcePath, destinationPath))
        {
            return;
        }

        await CopyToPathAsync(sourcePath, destinationPath, cancellationToken)
            .ConfigureAwait(false);

        if (!_verifyCopy(sourcePath, destinationPath))
        {
            throw new InvalidOperationException(
                "The copy could not be verified, so the original item was kept. "
                + $"An incomplete copy may remain at “{destinationPath}”.");
        }

        DeleteSource(sourcePath);
    }

    public async Task CopyAsync(
        string sourcePath,
        string destinationDirectoryPath,
        CancellationToken cancellationToken = default)
    {
        var destinationPath = ResolveDestinationPath(sourcePath, destinationDirectoryPath);
        cancellationToken.ThrowIfCancellationRequested();

        await CopyToPathAsync(sourcePath, destinationPath, cancellationToken)
            .ConfigureAwait(false);
    }

    private static string ResolveDestinationPath(
        string sourcePath,
        string destinationDirectoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationDirectoryPath);

        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
        {
            throw new FileNotFoundException("The selected item no longer exists.", sourcePath);
        }

        if (!Directory.Exists(destinationDirectoryPath))
        {
            throw new DirectoryNotFoundException("The destination no longer exists.");
        }

        var name = Path.GetFileName(
            sourcePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var destinationPath = Path.Combine(destinationDirectoryPath, name);

        if (File.Exists(destinationPath) || Directory.Exists(destinationPath))
        {
            throw new IOException(
                $"An item named “{name}” already exists at the destination.");
        }

        return destinationPath;
    }

    private static async Task CopyToPathAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/bin/cp",
                ArgumentList = { "-R", "-p", "-c", sourcePath, destinationPath },
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException("macOS could not start the copy.");
            }

            await using var registration = cancellationToken.Register(() => TryKill(process));
            var errorTask = process.StandardError.ReadToEndAsync(CancellationToken.None);
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            var error = (await errorTask.ConfigureAwait(false)).Trim();

            cancellationToken.ThrowIfCancellationRequested();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error)
                        ? $"macOS could not copy the item to “{destinationPath}”."
                        : error);
            }
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException and not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"macOS could not copy the item to “{destinationPath}”.",
                exception);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception)
        {
        }
    }

    private static bool TryFastMove(string sourcePath, string destinationPath)
    {
        try
        {
            if (Directory.Exists(sourcePath))
            {
                Directory.Move(sourcePath, destinationPath);
            }
            else
            {
                File.Move(sourcePath, destinationPath);
            }

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsCopyVerified(string sourcePath, string destinationPath)
    {
        try
        {
            if (File.Exists(sourcePath))
            {
                return File.Exists(destinationPath)
                    && new FileInfo(sourcePath).Length == new FileInfo(destinationPath).Length;
            }

            if (!Directory.Exists(sourcePath) || !Directory.Exists(destinationPath))
            {
                return false;
            }

            var source = Measure(sourcePath);
            var destination = Measure(destinationPath);
            return source == destination;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static (int EntryCount, long TotalBytes) Measure(string path)
    {
        var entryCount = 0;
        var totalBytes = 0L;
        var pending = new Stack<string>();
        pending.Push(path);

        while (pending.Count > 0)
        {
            var current = pending.Pop();

            foreach (var entry in Directory.EnumerateFileSystemEntries(current))
            {
                entryCount++;
                var attributes = File.GetAttributes(entry);

                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    continue;
                }

                if (attributes.HasFlag(FileAttributes.Directory))
                {
                    pending.Push(entry);
                }
                else
                {
                    totalBytes += new FileInfo(entry).Length;
                }
            }
        }

        return (entryCount, totalBytes);
    }

    private static void DeleteSource(string sourcePath)
    {
        if (Directory.Exists(sourcePath))
        {
            Directory.Delete(sourcePath, recursive: true);
            return;
        }

        File.Delete(sourcePath);
    }
}
