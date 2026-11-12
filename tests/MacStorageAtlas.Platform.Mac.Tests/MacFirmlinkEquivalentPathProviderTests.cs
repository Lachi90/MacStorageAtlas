using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Platform.Mac.Tests;

public sealed class MacFirmlinkEquivalentPathProviderTests
{
    private const string StandardDefinition =
        "/Applications\tApplications\n/Users\tUsers\n/usr/local\tusr/local\n";

    [Test]
    public void ParseDefinitionMapsEntriesToDataVolumePaths()
    {
        var equivalentPaths = MacFirmlinkEquivalentPathProvider.ParseDefinition(
            StandardDefinition);

        Assert.That(
            equivalentPaths,
            Is.EqualTo(new[]
            {
                new EquivalentPath("/Applications", "/System/Volumes/Data/Applications"),
                new EquivalentPath("/Users", "/System/Volumes/Data/Users"),
                new EquivalentPath("/usr/local", "/System/Volumes/Data/usr/local")
            }));
    }

    [Test]
    public void ParseDefinitionIgnoresBlankMalformedRelativeAndDuplicateLines()
    {
        const string definition =
            "\n"
            + "/Users\tUsers\r\n"
            + "/Users\tOtherUsers\n"
            + "/Others\tUsers\n"
            + "Library\tLibrary\n"
            + "/Library\t/Library\n"
            + "/Library\n"
            + "/Library\tLibrary\textra\n"
            + "/opt\t../opt\n"
            + "/pkg/\tpkg\n"
            + "/\tUsers\n"
            + "/cores\tcores\n";

        var equivalentPaths = MacFirmlinkEquivalentPathProvider.ParseDefinition(definition);

        Assert.That(
            equivalentPaths,
            Is.EqualTo(new[]
            {
                new EquivalentPath("/Users", "/System/Volumes/Data/Users"),
                new EquivalentPath("/cores", "/System/Volumes/Data/cores")
            }));
    }

    [Test]
    public void GetEquivalentPathsReturnsOnlyPairsWithEqualDirectoryIdentity()
    {
        var identities = new Dictionary<string, FileIdentity?>
        {
            ["/Applications"] = new FileIdentity(1, 10),
            ["/System/Volumes/Data/Applications"] = new FileIdentity(1, 10),
            ["/Users"] = new FileIdentity(1, 20),
            ["/System/Volumes/Data/Users"] = new FileIdentity(1, 21),
            ["/usr/local"] = null,
            ["/System/Volumes/Data/usr/local"] = new FileIdentity(1, 30)
        };
        var provider = new MacFirmlinkEquivalentPathProvider(
            isSupportedPlatform: true,
            () => StandardDefinition,
            path => identities[path]);

        var equivalentPaths = provider.GetEquivalentPaths();

        Assert.That(
            equivalentPaths,
            Is.EqualTo(new[]
            {
                new EquivalentPath("/Applications", "/System/Volumes/Data/Applications")
            }));
    }

    [Test]
    public void GetEquivalentPathsSkipsPairsWhoseIdentityReadFails()
    {
        var provider = new MacFirmlinkEquivalentPathProvider(
            isSupportedPlatform: true,
            () => "/Users\tUsers\n/Library\tLibrary\n",
            path => path.EndsWith("Users", StringComparison.Ordinal)
                ? throw new UnauthorizedAccessException()
                : new FileIdentity(1, 5));

        var equivalentPaths = provider.GetEquivalentPaths();

        Assert.That(
            equivalentPaths,
            Is.EqualTo(new[]
            {
                new EquivalentPath("/Library", "/System/Volumes/Data/Library")
            }));
    }

    [Test]
    public void GetEquivalentPathsReturnsEmptyWhenDefinitionIsMissing()
    {
        var provider = new MacFirmlinkEquivalentPathProvider(
            isSupportedPlatform: true,
            () => null,
            _ => new FileIdentity(1, 1));

        Assert.That(provider.GetEquivalentPaths(), Is.Empty);
    }

    [Test]
    public void GetEquivalentPathsReturnsEmptyWhenDefinitionIsUnreadable()
    {
        var provider = new MacFirmlinkEquivalentPathProvider(
            isSupportedPlatform: true,
            () => throw new IOException("Definition is unreadable."),
            _ => new FileIdentity(1, 1));

        Assert.That(provider.GetEquivalentPaths(), Is.Empty);
    }

    [Test]
    public void GetEquivalentPathsReturnsEmptyOnUnsupportedPlatform()
    {
        var definitionReads = 0;
        var provider = new MacFirmlinkEquivalentPathProvider(
            isSupportedPlatform: false,
            () =>
            {
                definitionReads++;
                return StandardDefinition;
            },
            _ => new FileIdentity(1, 1));

        Assert.Multiple(() =>
        {
            Assert.That(provider.GetEquivalentPaths(), Is.Empty);
            Assert.That(definitionReads, Is.Zero);
        });
    }

    [Test]
    public void SystemDefinitionMapsUsersToDataVolumeOnMacOs()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Ignore("macOS-specific firmlink integration.");
        }

        if (!File.Exists(MacFirmlinkEquivalentPathProvider.DefinitionPath)
            || !Directory.Exists("/System/Volumes/Data/Users"))
        {
            Assert.Ignore("This macOS installation does not publish a firmlinked Data volume.");
        }

        var equivalentPaths = new MacFirmlinkEquivalentPathProvider().GetEquivalentPaths();

        Assert.That(
            equivalentPaths,
            Does.Contain(new EquivalentPath("/Users", "/System/Volumes/Data/Users")));
    }

    [Test]
    public void DirectoryIdentityIsNullForFilesSymbolicLinksAndMissingPaths()
    {
        if (!OperatingSystem.IsMacOS())
        {
            Assert.Ignore("macOS-specific directory identity integration.");
        }

        var root = Path.Combine(Path.GetTempPath(), $"MacStorageAtlas-{Guid.NewGuid():N}");
        try
        {
            var directory = Directory.CreateDirectory(Path.Combine(root, "directory"));
            var file = Path.Combine(root, "file.bin");
            File.WriteAllBytes(file, [1]);
            var link = Path.Combine(root, "link");
            Directory.CreateSymbolicLink(link, directory.FullName);
            var native = new MacFileMetadataNative();

            Assert.Multiple(() =>
            {
                Assert.That(native.TryReadDirectoryIdentity(directory.FullName), Is.Not.Null);
                Assert.That(native.TryReadDirectoryIdentity(file), Is.Null);
                Assert.That(native.TryReadDirectoryIdentity(link), Is.Null);
                Assert.That(
                    native.TryReadDirectoryIdentity(Path.Combine(root, "missing")),
                    Is.Null);
            });
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
