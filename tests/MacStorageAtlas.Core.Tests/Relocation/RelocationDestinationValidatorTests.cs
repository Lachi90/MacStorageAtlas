using MacStorageAtlas.Core.Relocation;

namespace MacStorageAtlas.Core.Tests.Relocation;

public class RelocationDestinationValidatorTests
{
    [Test]
    public void ValidateReturnsReadyWhenDestinationAcceptsTheRequiredBytes()
    {
        var probe = new Probe { FreeSpace = RelocationFreeSpace.FromBytes(2_000) };
        var validator = new RelocationDestinationValidator(probe);

        var result = validator.Validate(Destination("/Volumes/Archive"), 1_000);

        Assert.Multiple(() =>
        {
            Assert.That(result.CanExecute, Is.True);
            Assert.That(result.Kind, Is.EqualTo(RelocationDestinationStatusKind.Ready));
            Assert.That(result.FreeSpace.IsKnown, Is.True);
            Assert.That(result.FreeSpace.AvailableBytes, Is.EqualTo(2_000));
        });
    }

    [Test]
    public void ValidateBlocksMissingDestination()
    {
        var probe = new Probe { Exists = false };
        var validator = new RelocationDestinationValidator(probe);

        var result = validator.Validate(Destination("/Volumes/Archive"), 1_000);

        Assert.Multiple(() =>
        {
            Assert.That(result.CanExecute, Is.False);
            Assert.That(result.Kind, Is.EqualTo(RelocationDestinationStatusKind.Missing));
            Assert.That(result.Message, Does.Contain("no longer exists"));
        });
    }

    [Test]
    public void ValidateBlocksDestinationThatIsNotADirectory()
    {
        var probe = new Probe { IsDirectory = false };
        var validator = new RelocationDestinationValidator(probe);

        var result = validator.Validate(Destination("/Volumes/Archive/file.bin"), 1_000);

        Assert.Multiple(() =>
        {
            Assert.That(result.CanExecute, Is.False);
            Assert.That(result.Kind, Is.EqualTo(RelocationDestinationStatusKind.NotADirectory));
            Assert.That(result.Message, Does.Contain("not a folder"));
        });
    }

    [Test]
    public void ValidateBlocksReadOnlyDestination()
    {
        var probe = new Probe { IsWritable = false };
        var validator = new RelocationDestinationValidator(probe);

        var result = validator.Validate(Destination("/Volumes/Archive"), 1_000);

        Assert.Multiple(() =>
        {
            Assert.That(result.CanExecute, Is.False);
            Assert.That(result.Kind, Is.EqualTo(RelocationDestinationStatusKind.NotWritable));
            Assert.That(result.Message, Does.Contain("cannot be written"));
        });
    }

    [Test]
    public void ValidateBlocksInsufficientFreeSpace()
    {
        var probe = new Probe { FreeSpace = RelocationFreeSpace.FromBytes(999) };
        var validator = new RelocationDestinationValidator(probe);

        var result = validator.Validate(Destination("/Volumes/Archive"), 1_000);

        Assert.Multiple(() =>
        {
            Assert.That(result.CanExecute, Is.False);
            Assert.That(
                result.Kind,
                Is.EqualTo(RelocationDestinationStatusKind.InsufficientFreeSpace));
            Assert.That(result.Message, Does.Contain("free space"));
        });
    }

    [Test]
    public void ValidateAllowsExactlyMatchingFreeSpace()
    {
        var probe = new Probe { FreeSpace = RelocationFreeSpace.FromBytes(1_000) };
        var validator = new RelocationDestinationValidator(probe);

        var result = validator.Validate(Destination("/Volumes/Archive"), 1_000);

        Assert.That(result.CanExecute, Is.True);
    }

    [Test]
    public void ValidateDoesNotBlockWhenFreeSpaceIsUnknown()
    {
        var probe = new Probe { FreeSpace = RelocationFreeSpace.Unknown };
        var validator = new RelocationDestinationValidator(probe);

        var result = validator.Validate(Destination("/Volumes/Archive"), long.MaxValue);

        Assert.Multiple(() =>
        {
            Assert.That(result.CanExecute, Is.True);
            Assert.That(result.FreeSpace.IsKnown, Is.False);
        });
    }

    [Test]
    public void ValidateProbesTheNormalizedDestinationPath()
    {
        var probe = new Probe { FreeSpace = RelocationFreeSpace.FromBytes(2_000) };
        var validator = new RelocationDestinationValidator(probe);

        validator.Validate(Destination("/Volumes/Archive/"), 1_000);

        Assert.That(probe.ProbedPaths, Has.All.EqualTo("/Volumes/Archive"));
    }

    [Test]
    public void ValidateRejectsNegativeRequiredBytes()
    {
        var validator = new RelocationDestinationValidator(new Probe());

        Assert.Throws<ArgumentOutOfRangeException>(
            () => validator.Validate(Destination("/Volumes/Archive"), -1));
    }

    private static RelocationDestination Destination(string path) =>
        RelocationDestination.FromPath(path);

    private sealed class Probe : IRelocationDestinationProbe
    {
        private readonly List<string> _probedPaths = [];

        public bool Exists { get; init; } = true;

        public bool IsDirectory { get; init; } = true;

        public bool IsWritable { get; init; } = true;

        public RelocationFreeSpace FreeSpace { get; init; } =
            RelocationFreeSpace.FromBytes(long.MaxValue);

        public IReadOnlyList<string> ProbedPaths => _probedPaths;

        bool IRelocationDestinationProbe.Exists(string path)
        {
            _probedPaths.Add(path);
            return Exists;
        }

        bool IRelocationDestinationProbe.IsDirectory(string path)
        {
            _probedPaths.Add(path);
            return IsDirectory;
        }

        bool IRelocationDestinationProbe.IsWritable(string path)
        {
            _probedPaths.Add(path);
            return IsWritable;
        }

        RelocationFreeSpace IRelocationDestinationProbe.GetFreeSpace(string path)
        {
            _probedPaths.Add(path);
            return FreeSpace;
        }
    }
}
