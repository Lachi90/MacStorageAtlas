using System.Xml.Linq;

namespace MacStorageAtlas.App.Tests;

[TestFixture]
public class AppMetadataTests
{
    private static readonly XNamespace AvaloniaNamespace = "https://github.com/avaloniaui";

    [Test]
    public void ApplicationNameIdentifiesTheMacOsMenuApplication()
    {
        var document = XDocument.Load(LocateAppMarkup());
        var application = document.Element(AvaloniaNamespace + "Application");

        Assert.That(application, Is.Not.Null);
        Assert.That(application!.Attribute("Name")?.Value, Is.EqualTo("MacStorageAtlas"));
    }

    private static string LocateAppMarkup()
    {
        var directory = TestContext.CurrentContext.TestDirectory;

        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory,
                "src",
                "MacStorageAtlas.App",
                "App.axaml");

            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        throw new FileNotFoundException("Could not locate App.axaml from the test directory.");
    }
}
