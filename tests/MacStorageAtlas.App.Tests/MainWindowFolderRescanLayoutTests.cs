using System.Xml.Linq;

namespace MacStorageAtlas.App.Tests;

public class MainWindowFolderRescanLayoutTests
{
    private static readonly XNamespace AvaloniaNamespace = "https://github.com/avaloniaui";

    [Test]
    public void TheSelectedItemTabOffersAnAccessibleFolderRescanButton()
    {
        var selectedItemTab = Root()
            .Descendants(AvaloniaNamespace + "TabItem")
            .Single(tab => AttributeValue(tab, "Header") == "Selected item");
        var button = selectedItemTab
            .Descendants(AvaloniaNamespace + "Button")
            .Single(element => AttributeValue(element, "Command") == "{Binding RescanFolderCommand}");

        Assert.Multiple(() =>
        {
            Assert.That(AttributeValue(button, "IsVisible"), Is.EqualTo("{Binding IsSelectedItemDirectory}"));
            Assert.That(
                AttributeValue(button, "AutomationProperties.Name"),
                Is.EqualTo("{Binding RescanSelectedFolderAccessibleName}"));
            Assert.That(
                button.Descendants(AvaloniaNamespace + "PathIcon")
                    .Select(icon => AttributeValue(icon, "Data")),
                Is.EqualTo(new[] { "{StaticResource IconRefresh}" }));
            Assert.That(
                button.Descendants(AvaloniaNamespace + "TextBlock")
                    .Select(text => AttributeValue(text, "Text")),
                Is.EqualTo(new[] { "Rescan this folder" }));
        });
    }

    [Test]
    public void TheScanOverlayStaysHiddenDuringAFolderRescan()
    {
        var overlays = Root()
            .Descendants(AvaloniaNamespace + "Border")
            .Where(border => AttributeValue(border, "Background") == "{DynamicResource ScrimBrush}")
            .ToArray();

        Assert.That(
            overlays.Select(border => AttributeValue(border, "IsVisible")),
            Is.EqualTo(new[] { "{Binding ShowScanOverlay}" }));
    }

    [Test]
    public void TheFolderRescanProgressCanBeStopped()
    {
        var banner = Root()
            .Descendants(AvaloniaNamespace + "Border")
            .Single(border => AttributeValue(border, "IsVisible") == "{Binding IsRescanningFolder}");

        Assert.That(
            banner.Descendants(AvaloniaNamespace + "Button")
                .Select(button => AttributeValue(button, "Command")),
            Is.EqualTo(new[] { "{Binding StopScanCommand}" }));
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
