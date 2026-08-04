using MacStorageAtlas.Core;

namespace MacStorageAtlas.Core.Tests;

public class FileCategoryMapTests
{
    [TestCase(".zip", FileCategory.Archive)]
    [TestCase(".mov", FileCategory.Video)]
    [TestCase(".heic", FileCategory.Image)]
    [TestCase(".flac", FileCategory.Audio)]
    [TestCase(".pdf", FileCategory.Document)]
    [TestCase(".dmg", FileCategory.DiskImage)]
    [TestCase(".pkg", FileCategory.DiskImage)]
    [TestCase(".iso", FileCategory.DiskImage)]
    [TestCase(".cs", FileCategory.Code)]
    public void KnownExtensionsResolveToTheirCategory(
        string extension,
        FileCategory expected)
    {
        Assert.That(FileCategoryMap.Find(extension), Is.EqualTo(expected));
    }

    [TestCase(".MOV")]
    [TestCase("MOV")]
    [TestCase("mov")]
    [TestCase("  .Mov  ")]
    public void ExtensionLookupIgnoresCaseAndLeadingDot(string extension)
    {
        Assert.That(FileCategoryMap.Find(extension), Is.EqualTo(FileCategory.Video));
    }

    [TestCase(".unknown-extension")]
    [TestCase(".qqq")]
    public void UnknownExtensionsBelongToNoCategory(string extension)
    {
        Assert.That(FileCategoryMap.Find(extension), Is.Null);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase(".")]
    public void AbsentExtensionsBelongToNoCategory(string? extension)
    {
        Assert.Multiple(() =>
        {
            Assert.That(FileCategoryMap.Find(extension), Is.Null);
            Assert.That(FileCategoryMap.NormalizeExtension(extension), Is.Null);
        });
    }

    [Test]
    public void FileWithoutAnExtensionBelongsToNoCategory()
    {
        Assert.That(FileCategoryMap.FindForFileName("README"), Is.Null);
    }

    [Test]
    public void FileNameLookupUsesTheTrailingExtension()
    {
        Assert.That(
            FileCategoryMap.FindForFileName("archive.tar.gz"),
            Is.EqualTo(FileCategory.Archive));
    }

    [TestCase("mov", ".mov")]
    [TestCase(".MOV", ".mov")]
    [TestCase("  Zip ", ".zip")]
    public void NormalizationProducesLowercaseDottedExtensions(
        string input,
        string expected)
    {
        Assert.That(FileCategoryMap.NormalizeExtension(input), Is.EqualTo(expected));
    }

    [Test]
    public void TheTaxonomyIsVersioned()
    {
        Assert.That(FileCategoryMap.Version, Is.GreaterThan(0));
    }
}
