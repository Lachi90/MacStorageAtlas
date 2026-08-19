using System.Diagnostics;

namespace MacStorageAtlas.Platform.Mac.Tests;

internal sealed class RamDisk(string deviceName, string mountPoint) : IAsyncDisposable
{
    private const int SectorCount = 32768;

    public string MountPoint { get; } = mountPoint;

    public static async Task<RamDisk?> TryCreateAsync(string volumeName)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return null;
        }

        var device = await RunAsync(
            "/usr/bin/hdiutil",
            ["attach", "-nomount", $"ram://{SectorCount}"]);
        if (device is null)
        {
            return null;
        }

        var deviceName = device.Trim();
        var formatted = await RunAsync(
            "/usr/sbin/diskutil",
            ["erasevolume", "APFS", volumeName, deviceName]);
        if (formatted is null)
        {
            await DetachAsync(deviceName);
            return null;
        }

        var mountPoint = Path.Combine("/Volumes", volumeName);
        if (!Directory.Exists(mountPoint))
        {
            await DetachAsync(deviceName);
            return null;
        }

        return new RamDisk(deviceName, mountPoint);
    }

    public async ValueTask DisposeAsync() => await DetachAsync(deviceName);

    private static async Task DetachAsync(string deviceName) =>
        await RunAsync("/usr/bin/hdiutil", ["detach", deviceName, "-force"]);

    private static async Task<string?> RunAsync(string fileName, string[] arguments)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }

            if (!process.Start())
            {
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
