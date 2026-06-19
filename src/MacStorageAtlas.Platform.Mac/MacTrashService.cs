using System.Diagnostics;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.Platform.Mac;

public sealed class MacTrashService : ITrashService
{
    private const string MoveToTrashScript = """
        on run argv
            tell application "Finder" to delete POSIX file (item 1 of argv)
        end run
        """;

    public async Task MoveToTrashAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new FileNotFoundException("The selected item no longer exists.", path);
        }

        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "/usr/bin/osascript",
                ArgumentList = { "-e", MoveToTrashScript, path },
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
                throw new InvalidOperationException("macOS could not start the Trash operation.");
            }

            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var error = (await errorTask.ConfigureAwait(false)).Trim();

            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    string.IsNullOrWhiteSpace(error)
                        ? "macOS could not move the selected item to Trash."
                        : error);
            }
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException and not InvalidOperationException)
        {
            throw new InvalidOperationException(
                "macOS could not move the selected item to Trash.",
                exception);
        }
    }
}
