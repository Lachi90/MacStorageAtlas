using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Platform.Mac;

public sealed class MacFirmlinkEquivalentPathProvider : IEquivalentPathProvider
{
    internal const string DefinitionPath = "/usr/share/firmlinks";
    internal const string DataVolumeRoot = "/System/Volumes/Data";

    private readonly bool _isSupportedPlatform;
    private readonly Func<string?> _readDefinition;
    private readonly Func<string, FileIdentity?> _readDirectoryIdentity;

    public MacFirmlinkEquivalentPathProvider()
        : this(
            OperatingSystem.IsMacOS(),
            ReadDefinitionFile,
            new MacFileMetadataNative().TryReadDirectoryIdentity)
    {
    }

    internal MacFirmlinkEquivalentPathProvider(
        bool isSupportedPlatform,
        Func<string?> readDefinition,
        Func<string, FileIdentity?> readDirectoryIdentity)
    {
        _isSupportedPlatform = isSupportedPlatform;
        _readDefinition = readDefinition
            ?? throw new ArgumentNullException(nameof(readDefinition));
        _readDirectoryIdentity = readDirectoryIdentity
            ?? throw new ArgumentNullException(nameof(readDirectoryIdentity));
    }

    public IReadOnlyList<EquivalentPath> GetEquivalentPaths()
    {
        if (!_isSupportedPlatform)
        {
            return [];
        }

        string? definition;
        try
        {
            definition = _readDefinition();
        }
        catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException)
        {
            return [];
        }

        if (string.IsNullOrEmpty(definition))
        {
            return [];
        }

        return ParseDefinition(definition)
            .Where(IsVerified)
            .ToArray();
    }

    internal static IReadOnlyList<EquivalentPath> ParseDefinition(string definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var equivalentPaths = new List<EquivalentPath>();
        var seenPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rawLine in definition.Split('\n'))
        {
            var fields = rawLine.TrimEnd('\r').Split('\t');
            if (fields.Length != 2)
            {
                continue;
            }

            var visiblePath = fields[0];
            var relativeDataPath = fields[1];
            if (!IsValidVisiblePath(visiblePath)
                || !IsValidRelativeDataPath(relativeDataPath))
            {
                continue;
            }

            var alternatePath = Path.Join(DataVolumeRoot, relativeDataPath);
            if (!seenPaths.Add(visiblePath) || !seenPaths.Add(alternatePath))
            {
                continue;
            }

            equivalentPaths.Add(new EquivalentPath(visiblePath, alternatePath));
        }

        return equivalentPaths;
    }

    private bool IsVerified(EquivalentPath equivalentPath)
    {
        try
        {
            if (_readDirectoryIdentity(equivalentPath.VisiblePath) is not { } visibleIdentity
                || _readDirectoryIdentity(equivalentPath.AlternatePath) is not { } alternateIdentity)
            {
                return false;
            }

            return visibleIdentity == alternateIdentity;
        }
        catch (Exception exception) when (exception is IOException
                                              or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool IsValidVisiblePath(string path) =>
        path.Length > 1
        && path[0] == '/'
        && !path.EndsWith('/')
        && HasOnlyNamedSegments(path[1..]);

    private static bool IsValidRelativeDataPath(string path) =>
        path.Length > 0
        && path[0] != '/'
        && !path.EndsWith('/')
        && HasOnlyNamedSegments(path);

    private static bool HasOnlyNamedSegments(string path) =>
        path.Split('/').All(segment => segment.Length > 0
                                       && segment != "."
                                       && segment != ".."
                                       && !segment.Any(char.IsControl));

    private static string? ReadDefinitionFile() =>
        File.Exists(DefinitionPath)
            ? File.ReadAllText(DefinitionPath)
            : null;
}
