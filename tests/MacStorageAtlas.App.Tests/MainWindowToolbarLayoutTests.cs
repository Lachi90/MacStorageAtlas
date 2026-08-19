using System.Xml.Linq;
using MacStorageAtlas.App.Views;

namespace MacStorageAtlas.App.Tests;

public class MainWindowToolbarLayoutTests
{
    private static readonly XNamespace AvaloniaNamespace = "https://github.com/avaloniaui";

    [Test]
    public void MainToolbarUsesASingleGridRow()
    {
        var toolbar = MainToolbar();
        var layout = toolbar.Elements().Single();

        Assert.Multiple(() =>
        {
            Assert.That(layout.Name, Is.EqualTo(AvaloniaNamespace + "Grid"));
            Assert.That(layout.Attribute("ColumnDefinitions")?.Value, Is.EqualTo("Auto,Auto,Auto,*,Auto,Auto"));
            Assert.That(toolbar.Attribute("BorderThickness")?.Value, Is.EqualTo("0,0,0,1"));
            Assert.That(toolbar.Attribute("BorderBrush")?.Value, Is.EqualTo("{DynamicResource DividerBrush}"));
            Assert.That(layout.Elements(AvaloniaNamespace + "WrapPanel"), Is.Empty);
        });
    }

    [Test]
    public void MainToolbarPreservesPrimaryCommandsAndSearch()
    {
        var toolbar = MainToolbar();
        var commands = CommandValues(toolbar).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(commands, Does.Contain("{Binding SelectFolderCommand}"));
            Assert.That(commands, Does.Contain("{Binding ScanFolderCommand}"));
            Assert.That(commands, Does.Contain("{Binding RescanCommand}"));
            Assert.That(commands, Does.Contain("{Binding StopScanCommand}"));
            Assert.That(ElementsWithAttribute(toolbar, "Name", "FilterButton"), Is.Not.Empty);
            Assert.That(ElementsWithAttribute(toolbar, "Name", "SearchBox"), Is.Not.Empty);
        });
    }

    [Test]
    public void ActionsFlyoutPreservesSelectedItemAndExportCommands()
    {
        var toolbar = MainToolbar();
        var actionsButton = ElementsWithAttribute(
                toolbar,
                "AutomationProperties.Name",
                "Selected item and export actions")
            .Single();
        var commands = CommandValues(actionsButton).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(commands, Does.Contain("{Binding RevealInFinderCommand}"));
            Assert.That(commands, Does.Contain("{Binding QuickLookCommand}"));
            Assert.That(commands, Does.Contain("{Binding MoveToTrashCommand}"));
            Assert.That(commands, Does.Contain("{Binding AddSelectedItemToCleanupBasketCommand}"));
            Assert.That(commands, Does.Contain("{Binding RemoveSelectedItemFromCleanupBasketCommand}"));
            Assert.That(commands, Does.Contain("{Binding ExportCsvCommand}"));
            Assert.That(commands, Does.Contain("{Binding ExportJsonCommand}"));
        });
    }

    [Test]
    public void FullActionGroupPreservesSelectedItemAndExportCommands()
    {
        var toolbar = MainToolbar();
        var fullActionGroup = ElementsWithAttribute(toolbar, "Name", "FullActionGroup")
            .Single();
        var commands = CommandValues(fullActionGroup).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(fullActionGroup.Attribute("IsVisible")?.Value, Is.EqualTo("False"));
            Assert.That(commands, Does.Contain("{Binding RevealInFinderCommand}"));
            Assert.That(commands, Does.Contain("{Binding QuickLookCommand}"));
            Assert.That(commands, Does.Contain("{Binding MoveToTrashCommand}"));
            Assert.That(commands, Does.Contain("{Binding AddSelectedItemToCleanupBasketCommand}"));
            Assert.That(commands, Does.Contain("{Binding RemoveSelectedItemFromCleanupBasketCommand}"));
            Assert.That(commands, Does.Contain("{Binding ExportCsvCommand}"));
            Assert.That(commands, Does.Contain("{Binding ExportJsonCommand}"));
        });
    }

    [Test]
    public void FullActionGroupRequiresWideWindowBeforeReplacingCompactActions()
    {
        Assert.That(MainWindow.FullToolbarActionThreshold, Is.GreaterThanOrEqualTo(2_200));
    }

    [Test]
    public void UtilityFlyoutsUseRightEdgePlacement()
    {
        var toolbar = MainToolbar();
        var optionsButton = ElementsWithAttribute(toolbar, "ToolTip.Tip", "Scan options")
            .Single();
        var filterButton = ElementsWithAttribute(toolbar, "Name", "FilterButton")
            .Single();

        Assert.Multiple(() =>
        {
            Assert.That(FlyoutPlacement(optionsButton), Is.EqualTo("BottomEdgeAlignedRight"));
            Assert.That(FlyoutPlacement(filterButton), Is.EqualTo("BottomEdgeAlignedRight"));
        });
    }

    private static XElement MainToolbar()
    {
        var document = XDocument.Load(MainWindowPath());
        return document
            .Root!
            .Element(AvaloniaNamespace + "DockPanel")!
            .Elements(AvaloniaNamespace + "Border")
            .First(element => AttributeValue(element, "DockPanel.Dock") == "Top");
    }

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

    private static IEnumerable<string> CommandValues(XElement root) =>
        root
            .DescendantsAndSelf()
            .Select(element => AttributeValue(element, "Command"))
            .Where(value => value is not null)
            .Select(value => value!);

    private static IEnumerable<XElement> ElementsWithAttribute(
        XElement root,
        string name,
        string value) =>
        root
            .DescendantsAndSelf()
            .Where(element => AttributeValue(element, name) == value);

    private static string? FlyoutPlacement(XElement button) =>
        button
            .Descendants(AvaloniaNamespace + "Flyout")
            .Select(element => AttributeValue(element, "Placement"))
            .SingleOrDefault();

    private static string? AttributeValue(XElement element, string name) =>
        element
            .Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName == name)
            ?.Value;
}
