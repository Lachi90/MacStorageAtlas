using System.Xml.Linq;

namespace MacStorageAtlas.App.Tests;

public class MainWindowAccessibleNameTests
{
    private static readonly XNamespace AvaloniaNamespace = "https://github.com/avaloniaui";

    [Test]
    public void EveryButtonHasAnAccessibleName()
    {
        var unnamed = XDocument.Load(MainWindowPath())
            .Descendants(AvaloniaNamespace + "Button")
            .Where(button => !HasAttribute(button, "AutomationProperties.Name")
                             && !HasAttribute(button, "Content"))
            .Select(button => button.ToString().Split('\n')[0].Trim())
            .ToArray();

        Assert.That(unnamed, Is.Empty);
    }

    private static bool HasAttribute(XElement element, string name) =>
        element.Attributes().Any(attribute =>
            attribute.Name.LocalName == name && !string.IsNullOrWhiteSpace(attribute.Value));

    private static string MainWindowPath()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(
                directory.FullName,
                "src",
                "MacStorageAtlas.App",
                "Views",
                "MainWindow.axaml");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("MainWindow.axaml was not found above the test directory.");
    }
}
