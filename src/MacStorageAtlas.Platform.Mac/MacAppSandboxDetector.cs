using MacStorageAtlas.Core.Access;

namespace MacStorageAtlas.Platform.Mac;

public sealed class MacAppSandboxDetector : IAppSandboxDetector
{
    internal const string ContainerIdVariable = "APP_SANDBOX_CONTAINER_ID";
    internal const string ContainerHomeSegment = "/Library/Containers/";
    private readonly Lazy<bool> _isSandboxed;

    public MacAppSandboxDetector()
        : this(new SandboxEnvironmentReader())
    {
    }

    internal MacAppSandboxDetector(ISandboxEnvironmentReader environmentReader)
    {
        ArgumentNullException.ThrowIfNull(environmentReader);

        _isSandboxed = new Lazy<bool>(() => Detect(environmentReader));
    }

    public bool IsSandboxed => _isSandboxed.Value;

    private static bool Detect(ISandboxEnvironmentReader environmentReader)
    {
        if (!OperatingSystem.IsMacOS())
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(environmentReader.GetVariable(ContainerIdVariable)))
        {
            return true;
        }

        var home = environmentReader.GetHomeDirectory();
        return !string.IsNullOrWhiteSpace(home)
            && home.Contains(ContainerHomeSegment, StringComparison.Ordinal);
    }
}

internal interface ISandboxEnvironmentReader
{
    string? GetVariable(string name);

    string GetHomeDirectory();
}

internal sealed class SandboxEnvironmentReader : ISandboxEnvironmentReader
{
    public string? GetVariable(string name) => Environment.GetEnvironmentVariable(name);

    public string GetHomeDirectory() =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}
