using System.Diagnostics;
using MacStorageAtlas.Core.Access;

namespace MacStorageAtlas.Platform.Mac;

public sealed class MacFullDiskAccessService : IFullDiskAccessService
{
    internal const string FullDiskAccessSettingsUrl =
        "x-apple.systempreferences:com.apple.preference.security?Privacy_AllFiles";
    internal const string PrivacySettingsUrl =
        "x-apple.systempreferences:com.apple.preference.security";
    private const int MinimumSuccessfulProbesForLikelyGranted = 2;
    private readonly IFullDiskAccessProbe _probe;
    private readonly ISystemSettingsOpener _settingsOpener;
    private readonly IAppSandboxDetector _sandboxDetector;

    public MacFullDiskAccessService()
        : this(new FullDiskAccessProbe(), new SystemSettingsOpener(), new MacAppSandboxDetector())
    {
    }

    internal MacFullDiskAccessService(
        IFullDiskAccessProbe probe,
        ISystemSettingsOpener settingsOpener,
        IAppSandboxDetector sandboxDetector)
    {
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _settingsOpener = settingsOpener ?? throw new ArgumentNullException(nameof(settingsOpener));
        _sandboxDetector = sandboxDetector ?? throw new ArgumentNullException(nameof(sandboxDetector));
    }

    public FullDiskAccessAssessment CheckAccess(string scanRootPath)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return FullDiskAccessAssessment.NotApplicable;
        }

        if (_sandboxDetector.IsSandboxed)
        {
            return FullDiskAccessAssessment.SandboxRestricted;
        }

        try
        {
            var result = _probe.Probe();
            if (result.PermissionDenied)
            {
                return new FullDiskAccessAssessment(FullDiskAccessStatus.LikelyMissing);
            }

            return result.SuccessfulProbeCount >= MinimumSuccessfulProbesForLikelyGranted
                ? new FullDiskAccessAssessment(
                    FullDiskAccessStatus.LikelyGranted,
                    result.SuccessfulProbeCount)
                : new FullDiskAccessAssessment(
                    FullDiskAccessStatus.Indeterminate,
                    result.SuccessfulProbeCount);
        }
        catch (Exception)
        {
            return FullDiskAccessAssessment.Indeterminate;
        }
    }

    public FullDiskAccessSettingsResult OpenSettings()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return FullDiskAccessSettingsResult.Failed;
        }

        if (_settingsOpener.Open(FullDiskAccessSettingsUrl))
        {
            return FullDiskAccessSettingsResult.OpenedDirectly;
        }

        return _settingsOpener.Open(PrivacySettingsUrl)
            ? FullDiskAccessSettingsResult.OpenedFallback
            : FullDiskAccessSettingsResult.Failed;
    }
}

internal interface IFullDiskAccessProbe
{
    FullDiskAccessProbeResult Probe();
}

internal interface ISystemSettingsOpener
{
    bool Open(string url);
}

internal sealed record FullDiskAccessProbeResult(
    int SuccessfulProbeCount,
    bool PermissionDenied);

internal sealed class FullDiskAccessProbe : IFullDiskAccessProbe
{
    public FullDiskAccessProbeResult Probe()
    {
        var successfulProbeCount = 0;
        var permissionDenied = false;

        foreach (var path in ProbePaths())
        {
            if (!Directory.Exists(path))
            {
                continue;
            }

            try
            {
                Directory.EnumerateFileSystemEntries(path).Take(1).Any();
                successfulProbeCount++;
            }
            catch (Exception exception) when (IsPermissionDenied(exception))
            {
                permissionDenied = true;
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
            }
        }

        return new FullDiskAccessProbeResult(successfulProbeCount, permissionDenied);
    }

    private static IEnumerable<string> ProbePaths()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            yield break;
        }

        yield return Path.Combine(home, "Library", "Mail");
        yield return Path.Combine(home, "Library", "Messages");
        yield return Path.Combine(home, "Library", "Safari");
    }

    private static bool IsPermissionDenied(Exception exception) =>
        exception is UnauthorizedAccessException
        || exception.Message.Contains(
            "operation not permitted",
            StringComparison.OrdinalIgnoreCase)
        || exception.Message.Contains(
            "permission denied",
            StringComparison.OrdinalIgnoreCase)
        || exception.Message.Contains(
            "access denied",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsRecoverable(Exception exception) =>
        exception is IOException or DirectoryNotFoundException
            or FileNotFoundException;
}

internal sealed class SystemSettingsOpener : ISystemSettingsOpener
{
    public bool Open(string url)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "/usr/bin/open",
                ArgumentList = { url },
                UseShellExecute = false
            });

            return process is not null;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
