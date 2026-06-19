using MacStorageAtlas.Core;

namespace MacStorageAtlas.Tests;

public class FileSizeFormatterTests
{
    [TestCase(0, "0 B")]
    [TestCase(512, "512 B")]
    [TestCase(1_024, "1.0 KB")]
    [TestCase(1_048_576, "1.0 MB")]
    [TestCase(1_073_741_824, "1.0 GB")]
    [TestCase(1_099_511_627_776, "1.0 TB")]
    public void FormatReturnsReadableBinarySize(long sizeBytes, string expected)
    {
        Assert.That(FileSizeFormatter.Format(sizeBytes), Is.EqualTo(expected));
    }
}
