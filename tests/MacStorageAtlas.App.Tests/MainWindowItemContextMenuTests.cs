using System.Xml.Linq;
using Avalonia.Controls;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.App.Views;

namespace MacStorageAtlas.App.Tests;

public class MainWindowItemContextMenuTests
{
    private static readonly XNamespace AvaloniaNamespace = "https://github.com/avaloniaui";
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";

    [Test]
    public void ItemContextMenuListsTheSelectedItemCommandsInGroupOrder()
    {
        var entries = ItemContextMenu()
            .Elements()
            .Select(element => element.Name == AvaloniaNamespace + "Separator"
                ? AttributeValue(element, "Name") ?? "-"
                : AttributeValue(element, "Command"))
            .ToArray();

        Assert.That(entries, Is.EqualTo(new[]
        {
            "{Binding QuickLookCommand}",
            "{Binding ShowSelectedItemDetailsCommand}",
            "{Binding RevealInFinderCommand}",
            "-",
            "{Binding CopySelectedItemPathCommand}",
            "FolderActionsGroup",
            "{Binding RescanFolderCommand}",
            "-",
            "{Binding AddSelectedItemToCleanupBasketCommand}",
            "{Binding RemoveSelectedItemFromCleanupBasketCommand}",
            "-",
            "{Binding MoveToTrashCommand}"
        }));
    }

    [Test]
    public void ItemContextMenuEntriesHaveAccessibleNamesAndShowShortcuts()
    {
        var items = ItemContextMenu()
            .Elements(AvaloniaNamespace + "MenuItem")
            .ToDictionary(
                element => AttributeValue(element, "Command")!,
                element => element);

        Assert.Multiple(() =>
        {
            foreach (var item in items.Values)
            {
                Assert.That(AttributeValue(item, "Header"), Is.Not.Empty);
                Assert.That(AttributeValue(item, "AutomationProperties.Name"), Is.Not.Empty);
            }

            Assert.That(
                AttributeValue(items["{Binding QuickLookCommand}"], "InputGesture"),
                Is.EqualTo("Space"));
            Assert.That(
                AttributeValue(items["{Binding ShowSelectedItemDetailsCommand}"], "InputGesture"),
                Is.EqualTo("Meta+I"));
            Assert.That(
                AttributeValue(items["{Binding CopySelectedItemPathCommand}"], "InputGesture"),
                Is.EqualTo("Meta+Alt+C"));
            Assert.That(
                AttributeValue(items["{Binding RescanFolderCommand}"], "InputGesture"),
                Is.EqualTo("Shift+Meta+R"));
            Assert.That(
                AttributeValue(items["{Binding RescanFolderCommand}"], "IsVisible"),
                Is.EqualTo("{Binding IsSelectedItemDirectory}"));
            Assert.That(
                AttributeValue(items["{Binding MoveToTrashCommand}"], "Classes"),
                Is.EqualTo("danger"));
        });
    }

    [Test]
    public void ItemContextMenuUsesTheWindowViewModel()
    {
        var menu = ItemContextMenu();

        Assert.Multiple(() =>
        {
            Assert.That(AttributeValue(menu, "DataType"), Is.EqualTo("vm:MainWindowViewModel"));
            Assert.That(AttributeValue(menu, "DataContext"), Is.Null);
        });
    }

    [Test]
    public void ItemContextMenuIsAttachedToEveryResultItemContainer()
    {
        var root = XDocument.Load(MainWindowPath()).Root!;
        var reference = $"{{StaticResource {MainWindow.ResultItemContextMenuKey}}}";
        var containerSelectors = root
            .Descendants(AvaloniaNamespace + "Setter")
            .Where(setter =>
                AttributeValue(setter, "Property") == "ContextMenu"
                && AttributeValue(setter, "Value") == reference)
            .Select(setter => AttributeValue(setter.Parent!, "Selector"))
            .ToArray();
        var styledCollections = root
            .Descendants(AvaloniaNamespace + "Setter")
            .Where(setter => AttributeValue(setter, "Value") == reference)
            .Select(setter => AttributeValue(setter.Parent!.Parent!.Parent!, "ItemsSource"))
            .ToArray();
        var treemap = root
            .Descendants()
            .Single(element => element.Name.LocalName == "TreemapControl");

        Assert.Multiple(() =>
        {
            Assert.That(
                containerSelectors,
                Is.EquivalentTo(new[] { "TreeViewItem", "ListBoxItem", "ListBoxItem" }));
            Assert.That(
                styledCollections,
                Is.EquivalentTo(new[]
                {
                    "{Binding TreeItems}",
                    "{Binding LargeFiles}",
                    "{Binding Entries}"
                }));
            Assert.That(AttributeValue(treemap, "ContextMenu"), Is.EqualTo(reference));
        });
    }

    [Test]
    public void EmptyFolderActionsGroupIsHiddenWithItsSeparator()
    {
        var folderActions = new Separator { Name = "FolderActionsGroup" };
        var basketSeparator = new Separator();
        var trashSeparator = new Separator();
        var inspectionSeparator = new Separator();

        MainWindow.UpdateSeparatorVisibility(
        [
            new MenuItem(),
            inspectionSeparator,
            new MenuItem(),
            folderActions,
            basketSeparator,
            new MenuItem(),
            trashSeparator,
            new MenuItem()
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(inspectionSeparator.IsVisible, Is.True);
            Assert.That(folderActions.IsVisible, Is.False);
            Assert.That(basketSeparator.IsVisible, Is.True);
            Assert.That(trashSeparator.IsVisible, Is.True);
        });
    }

    [Test]
    public void FolderActionsGroupIsShownWhenItHasAVisibleEntry()
    {
        var folderActions = new Separator { Name = "FolderActionsGroup" };
        var basketSeparator = new Separator();

        MainWindow.UpdateSeparatorVisibility(
        [
            new MenuItem(),
            folderActions,
            new MenuItem(),
            basketSeparator,
            new MenuItem()
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(folderActions.IsVisible, Is.True);
            Assert.That(basketSeparator.IsVisible, Is.True);
        });
    }

    [Test]
    public void FolderActionsGroupStaysHiddenWhenAllItsEntriesAreHidden()
    {
        var folderActions = new Separator { Name = "FolderActionsGroup" };
        var basketSeparator = new Separator();

        MainWindow.UpdateSeparatorVisibility(
        [
            new MenuItem(),
            folderActions,
            new MenuItem { IsVisible = false },
            basketSeparator,
            new MenuItem()
        ]);

        Assert.Multiple(() =>
        {
            Assert.That(folderActions.IsVisible, Is.False);
            Assert.That(basketSeparator.IsVisible, Is.True);
        });
    }

    [Test]
    public void LeadingAndTrailingSeparatorsAreHidden()
    {
        var leading = new Separator();
        var trailing = new Separator();

        MainWindow.UpdateSeparatorVisibility([leading, new MenuItem(), trailing]);

        Assert.Multiple(() =>
        {
            Assert.That(leading.IsVisible, Is.False);
            Assert.That(trailing.IsVisible, Is.False);
        });
    }

    [TestCase(nameof(MainWindowViewModel.TreeItems), true)]
    [TestCase(nameof(MainWindowViewModel.TreemapRectangles), true)]
    [TestCase(nameof(MainWindowViewModel.LargeFiles), true)]
    [TestCase(nameof(MainWindowViewModel.DuplicateGroups), true)]
    [TestCase(nameof(MainWindowViewModel.SelectedItem), false)]
    [TestCase(nameof(MainWindowViewModel.CleanupBasketItems), false)]
    [TestCase(null, false)]
    public void ResultReplacementPropertiesCloseTheItemContextMenu(string? propertyName, bool expected)
    {
        Assert.That(
            MainWindow.IsResultReplacement(propertyName, isLargeFileSelected: true),
            Is.EqualTo(expected));
    }

    [Test]
    public void ARescopedLargestFilesListKeepsAMenuOpenedOutsideThatList()
    {
        Assert.Multiple(() =>
        {
            Assert.That(
                MainWindow.IsResultReplacement(
                    nameof(MainWindowViewModel.LargeFiles),
                    isLargeFileSelected: false),
                Is.False);
            Assert.That(
                MainWindow.IsResultReplacement(
                    nameof(MainWindowViewModel.TreeItems),
                    isLargeFileSelected: false),
                Is.True);
        });
    }

    private static XElement ItemContextMenu() =>
        XDocument.Load(MainWindowPath())
            .Root!
            .Element(AvaloniaNamespace + "Window.Resources")!
            .Elements(AvaloniaNamespace + "ContextMenu")
            .Single(element =>
                element.Attribute(XamlNamespace + "Key")?.Value
                == MainWindow.ResultItemContextMenuKey);

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
