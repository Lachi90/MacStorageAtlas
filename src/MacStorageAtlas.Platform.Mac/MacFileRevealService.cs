using System.Diagnostics;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.Platform.Mac;

public sealed class MacFileRevealService : IFileRevealService
{
    public bool Reveal(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            (!File.Exists(path) && !Directory.Exists(path)))
        {
            return false;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "/usr/bin/open",
                ArgumentList = { "-R", path },
                UseShellExecute = false
            });

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
