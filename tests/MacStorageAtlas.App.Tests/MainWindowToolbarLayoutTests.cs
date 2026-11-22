using System.Globalization;
using System.Xml.Linq;
using MacStorageAtlas.App.Views;

namespace MacStorageAtlas.App.Tests;

public class MainWindowToolbarLayoutTests
{
    private static readonly XNamespace AvaloniaNamespace = "https://github.com/avaloniaui";

    private static readonly double MeasuredWidestSingleRow = 1_041.5;

    [Test]
    public void MainToolbarUsesASingleGridRow()
    {
        var toolbar = MainToolbar();
        var layout = toolbar.Elements().Single();
        var columns = layout
            .Element(AvaloniaNamespace + "Grid.ColumnDefinitions")!
            .Elements(AvaloniaNamespace + "ColumnDefinition")
            .ToArray();
        var searchColumn = columns[^1];

        Assert.Multiple(() =>
        {
            Assert.That(layout.Name, Is.EqualTo(AvaloniaNamespace + "Grid"));
            Assert.That(layout.Attribute("RowDefinitions"), Is.Null);
            Assert.That(
                columns.Select(column => AttributeValue(column, "Width")),
                Is.EqualTo(new[] { "Auto", "Auto", "Auto", "*", "Auto", "Auto", "100*" }));
            Assert.That(AttributeValue(searchColumn, "MinWidth"), Is.EqualTo("140"));
            Assert.That(AttributeValue(searchColumn, "MaxWidth"), Is.EqualTo("220"));
            Assert.That(toolbar.Attribute("BorderThickness")?.Value, Is.EqualTo("0,0,0,1"));
            Assert.That(toolbar.Attribute("BorderBrush")?.Value, Is.EqualTo("{DynamicResource DividerBrush}"));
            Assert.That(layout.Descendants(AvaloniaNamespace + "WrapPanel"), Is.Empty);
        });
    }

    [Test]
    public void MainToolbarPreservesPrimaryCommandsAndSearch()
    {
        var toolbar = MainToolbar();
        var commands = CommandValues(toolbar).ToArray();
        var searchBox = ElementsWithAttribute(toolbar, "Name", "SearchBox").Single();

        Assert.Multiple(() =>
        {
            Assert.That(commands, Does.Contain("{Binding SelectFolderCommand}"));
            Assert.That(commands, Does.Contain("{Binding PrimaryScanActionCommand}"));
            Assert.That(ElementsWithAttribute(toolbar, "Name", "FilterButton"), Is.Not.Empty);
            Assert.That(ElementsWithAttribute(toolbar, "Name", "ScanHistoryButton"), Is.Not.Empty);
            Assert.That(ElementsWithAttribute(toolbar, "ToolTip.Tip", "Scan options"), Is.Not.Empty);
            Assert.That(AttributeValue(searchBox, "Grid.Column"), Is.EqualTo("6"));
            Assert.That(AttributeValue(searchBox, "Text"), Is.EqualTo("{Binding SearchText, Mode=TwoWay}"));
        });
    }

    [Test]
    public void ScanRescanAndStopShareOnePrimaryControl()
    {
        var toolbar = MainToolbar();
        var primary = ElementsWithAttribute(toolbar, "Name", "PrimaryScanButton").Single();
        var commands = CommandValues(toolbar).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(AttributeValue(primary, "Command"), Is.EqualTo("{Binding PrimaryScanActionCommand}"));
            Assert.That(AttributeValue(primary, "Classes.accent"), Is.EqualTo("{Binding !IsScanning}"));
            Assert.That(AttributeValue(primary, "Classes.danger"), Is.EqualTo("{Binding IsScanning}"));
            Assert.That(AttributeValue(primary, "ToolTip.Tip"), Is.EqualTo("{Binding PrimaryScanActionToolTip}"));
            Assert.That(
                AttributeValue(primary, "AutomationProperties.Name"),
                Is.EqualTo("{Binding PrimaryScanActionLabel}"));
            Assert.That(
                primary.Descendants(AvaloniaNamespace + "TextBlock").Select(text => AttributeValue(text, "Text")),
                Is.EqualTo(new[] { "{Binding PrimaryScanActionLabel}" }));
            Assert.That(
                primary.Descendants(AvaloniaNamespace + "PathIcon").Select(icon => AttributeValue(icon, "IsVisible")),
                Is.EqualTo(new[]
                {
                    "{Binding IsPrimaryScanActionScan}",
                    "{Binding IsPrimaryScanActionRescan}",
                    "{Binding IsPrimaryScanActionStop}"
                }));
            Assert.That(commands, Does.Not.Contain("{Binding ScanFolderCommand}"));
            Assert.That(commands, Does.Not.Contain("{Binding RescanCommand}"));
            Assert.That(commands, Does.Not.Contain("{Binding StopScanCommand}"));
        });
    }

    [TestCase("QuickLookButton", "{Binding QuickLookCommand}", "Quick Look", "Quick Look (Space or Command-Y)")]
    [TestCase("RevealInFinderButton", "{Binding RevealInFinderCommand}", "Reveal in Finder", "Reveal in Finder (Option-Command-R)")]
    [TestCase("MoveToTrashButton", "{Binding MoveToTrashCommand}", "Move to Trash", "Move to Trash (Command-Delete)")]
    [TestCase("ExportMenuButton", null, "Export", "Export as CSV (Command-E) or JSON (Shift-Command-E)")]
    public void IconControlsNameTheirActionAndShortcut(
        string name,
        string? command,
        string accessibleName,
        string toolTip)
    {
        var fullActionGroup = FullActionGroup();
        var button = ElementsWithAttribute(fullActionGroup, "Name", name).Single();

        Assert.Multiple(() =>
        {
            Assert.That(AttributeValue(button, "Classes")?.Split(' '), Does.Contain("icon"));
            Assert.That(AttributeValue(button, "Command"), Is.EqualTo(command));
            Assert.That(AttributeValue(button, "AutomationProperties.Name"), Is.EqualTo(accessibleName));
            Assert.That(AttributeValue(button, "ToolTip.Tip"), Is.EqualTo(toolTip));
            Assert.That(button.Descendants(AvaloniaNamespace + "TextBlock"), Is.Empty);
        });
    }

    [Test]
    public void MoveToTrashKeepsTheDangerStyle()
    {
        var button = ElementsWithAttribute(FullActionGroup(), "Name", "MoveToTrashButton").Single();

        Assert.That(AttributeValue(button, "Classes")?.Split(' '), Does.Contain("danger"));
    }

    [Test]
    public void TheBasketToggleStatesItsActionAndShowsTheCount()
    {
        var toggle = ElementsWithAttribute(FullActionGroup(), "Name", "CleanupBasketToggleButton").Single();
        var badge = ElementsWithAttribute(toggle, "Name", "CleanupBasketCountBadge").Single();

        Assert.Multiple(() =>
        {
            Assert.That(AttributeValue(toggle, "Classes")?.Split(' '), Does.Contain("icon"));
            Assert.That(
                AttributeValue(toggle, "Command"),
                Is.EqualTo("{Binding ToggleSelectedItemInCleanupBasketCommand}"));
            Assert.That(
                AttributeValue(toggle, "AutomationProperties.Name"),
                Is.EqualTo("{Binding CleanupBasketCommandLabel}"));
            Assert.That(AttributeValue(toggle, "ToolTip.Tip"), Is.EqualTo("{Binding CleanupBasketToggleToolTip}"));
            Assert.That(
                toggle.Descendants(AvaloniaNamespace + "PathIcon").Select(icon => AttributeValue(icon, "IsVisible")),
                Is.EqualTo(new[]
                {
                    "{Binding !CleanupBasketToggleRemovesItem}",
                    "{Binding CleanupBasketToggleRemovesItem}"
                }));
            Assert.That(AttributeValue(badge, "Classes"), Is.EqualTo("count-badge"));
            Assert.That(AttributeValue(badge, "IsVisible"), Is.EqualTo("{Binding HasCleanupBasketItems}"));
            Assert.That(
                AttributeValue(badge.Elements().Single(), "Text"),
                Is.EqualTo("{Binding CleanupBasketItemCount}"));
        });
    }

    [Test]
    public void TheExportControlOffersBothFormats()
    {
        var export = ElementsWithAttribute(FullActionGroup(), "Name", "ExportMenuButton").Single();
        var menu = export.Descendants(AvaloniaNamespace + "MenuFlyout").Single();

        Assert.Multiple(() =>
        {
            Assert.That(AttributeValue(export, "IsEnabled"), Is.EqualTo("{Binding IsExportAvailable}"));
            Assert.That(
                CommandValues(menu),
                Is.EqualTo(new[] { "{Binding ExportCsvCommand}", "{Binding ExportJsonCommand}" }));
            Assert.That(
                menu.Elements().Select(item => AttributeValue(item, "InputGesture")),
                Is.EqualTo(new[] { "Meta+E", "Shift+Meta+E" }));
        });
    }

    [Test]
    public void FullActionGroupHoldsOnlyTheMergedControls()
    {
        var commands = CommandValues(FullActionGroup()).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(AttributeValue(FullActionGroup(), "IsVisible"), Is.Null);
            Assert.That(commands, Does.Not.Contain("{Binding AddSelectedItemToCleanupBasketCommand}"));
            Assert.That(commands, Does.Not.Contain("{Binding RemoveSelectedItemFromCleanupBasketCommand}"));
            Assert.That(
                FullActionGroup().Elements(AvaloniaNamespace + "Border").Count(),
                Is.EqualTo(2));
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
            Assert.That(AttributeValue(actionsButton, "Name"), Is.EqualTo("CompactActionsButton"));
            Assert.That(AttributeValue(actionsButton, "IsVisible"), Is.EqualTo("False"));
            Assert.That(commands, Does.Contain("{Binding RevealInFinderCommand}"));
            Assert.That(commands, Does.Contain("{Binding QuickLookCommand}"));
            Assert.That(commands, Does.Contain("{Binding MoveToTrashCommand}"));
            Assert.That(commands, Does.Contain("{Binding ToggleSelectedItemInCleanupBasketCommand}"));
            Assert.That(commands, Does.Contain("{Binding ExportCsvCommand}"));
            Assert.That(commands, Does.Contain("{Binding ExportJsonCommand}"));
        });
    }

    [Test]
    public void DirectActionsAreShownAtTheDefaultAndMinimumWindowWidth()
    {
        var window = XDocument.Load(MainWindowPath()).Root!;
        var defaultWidth = double.Parse(AttributeValue(window, "Width")!, CultureInfo.InvariantCulture);
        var minimumWidth = double.Parse(AttributeValue(window, "MinWidth")!, CultureInfo.InvariantCulture);

        Assert.Multiple(() =>
        {
            Assert.That(minimumWidth, Is.GreaterThanOrEqualTo(MainWindow.FullToolbarActionThreshold));
            Assert.That(defaultWidth, Is.GreaterThanOrEqualTo(MainWindow.FullToolbarActionThreshold));
            Assert.That(MeasuredWidestSingleRow, Is.LessThanOrEqualTo(MainWindow.FullToolbarActionThreshold));
        });
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

    private static XElement FullActionGroup() =>
        ElementsWithAttribute(MainToolbar(), "Name", "FullActionGroup").Single();

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
