using System.Xml.Linq;

namespace MacStorageAtlas.App.Tests;

public class MainWindowInsightScopeLayoutTests
{
    private static readonly XNamespace AvaloniaNamespace = "https://github.com/avaloniaui";

    [TestCase("File types", "{Binding ShowFileTypesEmptyMessage}")]
    [TestCase("Largest files", "{Binding ShowLargeFilesEmptyMessage}")]
    public void InsightTabsStateTheScopeAndOfferTheWholeScan(string header, string emptyVisibility)
    {
        var tab = Root()
            .Descendants(AvaloniaNamespace + "TabItem")
            .Single(element => AttributeValue(element, "Header") == header);
        var caption = tab
            .Descendants(AvaloniaNamespace + "TextBlock")
            .Single(element => AttributeValue(element, "Text") == "{Binding InsightScopeCaption}");
        var reset = tab
            .Descendants(AvaloniaNamespace + "Button")
            .Single(element => AttributeValue(element, "Command") == "{Binding ResetInsightScopeCommand}");
        var empty = tab
            .Descendants(AvaloniaNamespace + "TextBlock")
            .Single(element => AttributeValue(element, "Text") == "{Binding InsightScopeEmptyMessage}");

        Assert.Multiple(() =>
        {
            Assert.That(AttributeValue(caption, "ToolTip.Tip"), Is.EqualTo("{Binding InsightScopePath}"));
            Assert.That(
                AttributeValue(caption, "AutomationProperties.HelpText"),
                Is.EqualTo("{Binding InsightScopePath}"));
            Assert.That(AttributeValue(caption, "TextTrimming"), Is.EqualTo("CharacterEllipsis"));
            Assert.That(AttributeValue(caption.Parent!, "IsVisible"), Is.EqualTo("{Binding HasInsightScope}"));
            Assert.That(AttributeValue(reset, "IsVisible"), Is.EqualTo("{Binding IsInsightScopeNarrowed}"));
            Assert.That(AttributeValue(reset, "AutomationProperties.Name"), Is.EqualTo("Show whole scan"));
            Assert.That(
                reset.Descendants(AvaloniaNamespace + "TextBlock").Select(text => AttributeValue(text, "Text")),
                Is.EqualTo(new[] { "Show whole scan" }));
            Assert.That(AttributeValue(empty, "IsVisible"), Is.EqualTo(emptyVisibility));
        });
    }

    private static XElement Root() => XDocument.Load(MainWindowPath()).Root!;

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

        throw new FileNotFoundException("Could not locate MainWindow.axaml from the test directory.");
    }

    private static string? AttributeValue(XElement element, string name) =>
        element
            .Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName == name)
            ?.Value;
}
