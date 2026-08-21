using MacStorageAtlas.Platform.Mac;

namespace MacStorageAtlas.Platform.Mac.Tests;

public class MacAppSandboxDetectorTests
{
    [Test]
    public void IsSandboxedReportsSandboxWhenTheContainerVariableIsSetOnMacOs()
    {
        var detector = new MacAppSandboxDetector(new FakeSandboxEnvironmentReader
        {
            ContainerId = "de.ltsoftware.macstorageatlas",
            HomeDirectory = "/Users/example"
        });

        Assert.That(detector.IsSandboxed, Is.EqualTo(OperatingSystem.IsMacOS()));
    }

    [Test]
    public void IsSandboxedReportsSandboxForAContainerHomeDirectoryOnMacOs()
    {
        var detector = new MacAppSandboxDetector(new FakeSandboxEnvironmentReader
        {
            HomeDirectory =
                "/Users/example/Library/Containers/de.ltsoftware.macstorageatlas/Data"
        });

        Assert.That(detector.IsSandboxed, Is.EqualTo(OperatingSystem.IsMacOS()));
    }

    [Test]
    public void IsSandboxedReportsNoSandboxForAnOrdinaryHomeDirectory()
    {
        var detector = new MacAppSandboxDetector(new FakeSandboxEnvironmentReader
        {
            HomeDirectory = "/Users/example"
        });

        Assert.That(detector.IsSandboxed, Is.False);
    }

    [Test]
    public void IsSandboxedIgnoresABlankContainerVariable()
    {
        var detector = new MacAppSandboxDetector(new FakeSandboxEnvironmentReader
        {
            ContainerId = "   ",
            HomeDirectory = "/Users/example"
        });

        Assert.That(detector.IsSandboxed, Is.False);
    }

    [Test]
    public void IsSandboxedReadsTheEnvironmentOnlyOnce()
    {
        var reader = new FakeSandboxEnvironmentReader
        {
            HomeDirectory = "/Users/example"
        };
        var detector = new MacAppSandboxDetector(reader);

        _ = detector.IsSandboxed;
        _ = detector.IsSandboxed;

        Assert.That(reader.VariableReadCount, Is.LessThanOrEqualTo(1));
    }

    [Test]
    public void TheRunningTestHostIsNotSandboxed()
    {
        Assert.That(new MacAppSandboxDetector().IsSandboxed, Is.False);
    }

    private sealed class FakeSandboxEnvironmentReader : ISandboxEnvironmentReader
    {
        public string? ContainerId { get; init; }

        public string HomeDirectory { get; init; } = string.Empty;

        public int VariableReadCount { get; private set; }

        public string? GetVariable(string name)
        {
            VariableReadCount++;

            return name == MacAppSandboxDetector.ContainerIdVariable ? ContainerId : null;
        }

        public string GetHomeDirectory() => HomeDirectory;
    }
}
