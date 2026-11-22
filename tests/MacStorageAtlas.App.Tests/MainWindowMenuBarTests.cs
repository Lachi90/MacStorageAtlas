using System.Globalization;
using System.Xml.Linq;
using Avalonia.Controls;
using MacStorageAtlas.App.Converters;
using MacStorageAtlas.App.Models;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.App.Views;
using MacStorageAtlas.Core.Platform;
using MacStorageAtlas.Core.Scanning;
using NSubstitute;

namespace MacStorageAtlas.App.Tests;

public class MainWindowMenuBarTests
{
    private static readonly XNamespace AvaloniaNamespace = "https://github.com/avaloniaui";

    private static readonly string[] TextEditingCollisionGestures =
    [
        "Space",
        "Meta+Back",
        "Meta+B",
        "Alt+Meta+C",
        "Meta+Alt+C",
        "Shift+Meta+R",
        "Meta+Z",
        "Shift+Meta+Z",
        "Meta+X",
        "Meta+C",
        "Meta+V",
        "Meta+A"
    ];

    [Test]
    public void MenuBarListsTheStandardMacMenusInOrder()
    {
        var headers = TopLevelMenus().Select(menu => AttributeValue(menu, "Header")).ToArray();

        Assert.That(headers, Is.EqualTo(new[]
        {
            "File", "Edit", "Scan", "Item", "View", "Window", "Help"
        }));
    }

    [TestCase("File", "Select Folder…", "{Binding SelectFolderCommand}", "Meta+O")]
    [TestCase("File", "Export as CSV…", "{Binding ExportCsvCommand}", "Meta+E")]
    [TestCase("File", "Export as JSON…", "{Binding ExportJsonCommand}", "Shift+Meta+E")]
    [TestCase("Scan", "{Binding ScanCommandLabel}", "{Binding RescanCommand}", "Meta+R")]
    [TestCase("Scan", "Rescan Folder", "{Binding RescanFolderCommand}", null)]
    [TestCase("Scan", "Stop", "{Binding StopScanCommand}", "Meta+OemPeriod")]
    [TestCase("Scan", "Find Duplicates", "{Binding StartDuplicateAnalysisCommand}", null)]
    [TestCase("Item", "Quick Look", "{Binding QuickLookCommand}", "Meta+Y")]
    [TestCase("Item", "Show Details", "{Binding ShowSelectedItemDetailsCommand}", "Meta+I")]
    [TestCase("Item", "Reveal in Finder", "{Binding RevealInFinderCommand}", "Alt+Meta+R")]
    [TestCase("Item", "Copy Path", "{Binding CopySelectedItemPathCommand}", null)]
    [TestCase(
        "Item",
        "{Binding CleanupBasketCommandLabel}",
        "{Binding ToggleSelectedItemInCleanupBasketCommand}",
        null)]
    [TestCase("Item", "Move to Trash…", "{Binding MoveToTrashCommand}", null)]
    [TestCase("View", "Show Whole Scan", "{Binding ResetInsightScopeCommand}", null)]
    [TestCase(
        "View",
        "Scan Inside Application Bundles",
        "{Binding ToggleExpandApplicationBundlesCommand}",
        null)]
    [TestCase("View", "Include Hidden Files", "{Binding ToggleIncludeHiddenFilesCommand}", null)]
    [TestCase("View", "Follow Symbolic Links", "{Binding ToggleFollowSymbolicLinksCommand}", null)]
    public void MenuEntryInvokesItsCommandWithItsShortcut(
        string menu,
        string header,
        string command,
        string? gesture)
    {
        var entry = Entry(menu, header);

        Assert.Multiple(() =>
        {
            Assert.That(AttributeValue(entry, "Command"), Is.EqualTo(command));
            Assert.That(AttributeValue(entry, "Gesture"), Is.EqualTo(gesture));
            Assert.That(AttributeValue(entry, "Click"), Is.Null);
        });
    }

    [TestCase("Edit", "Undo", "OnUndoMenuItemClick", null)]
    [TestCase("Edit", "Redo", "OnRedoMenuItemClick", null)]
    [TestCase("Edit", "Cut", "OnCutMenuItemClick", null)]
    [TestCase("Edit", "Copy", "OnCopyMenuItemClick", null)]
    [TestCase("Edit", "Paste", "OnPasteMenuItemClick", null)]
    [TestCase("Edit", "Select All", "OnSelectAllMenuItemClick", null)]
    [TestCase("View", "Filter and Search", "OnFilterAndSearchMenuItemClick", "Meta+F")]
    [TestCase("View", "Scan History", "OnScanHistoryMenuItemClick", null)]
    [TestCase("Window", "Minimize", "OnMinimizeMenuItemClick", "Meta+M")]
    [TestCase("Window", "Zoom", "OnZoomMenuItemClick", null)]
    [TestCase("Help", "Atlas Storage Analyzer Support", "OnSupportMenuItemClick", null)]
    [TestCase("Help", "Privacy Policy", "OnPrivacyPolicyMenuItemClick", null)]
    public void ViewMenuEntryUsesItsWindowHandler(
        string menu,
        string header,
        string handler,
        string? gesture)
    {
        var entry = Entry(menu, header);

        Assert.Multiple(() =>
        {
            Assert.That(AttributeValue(entry, "Click"), Is.EqualTo(handler));
            Assert.That(AttributeValue(entry, "Gesture"), Is.EqualTo(gesture));
            Assert.That(AttributeValue(entry, "Command"), Is.Null);
        });
    }

    [Test]
    public void ItemMenuListsTheSelectedItemCommandsInGroupOrder()
    {
        var entries = Submenu("Item")
            .Elements()
            .Select(element => element.Name == AvaloniaNamespace + "NativeMenuItemSeparator"
                ? "-"
                : AttributeValue(element, "Header"))
            .ToArray();

        Assert.That(entries, Is.EqualTo(new[]
        {
            "Quick Look",
            "Show Details",
            "Reveal in Finder",
            "Copy Path",
            "-",
            "{Binding CleanupBasketCommandLabel}",
            "-",
            "Move to Trash…"
        }));
    }

    [Test]
    public void ShortcutsThatTextFieldsUseAreNotRegisteredOnTheMenu()
    {
        var gestures = MenuEntries()
            .Select(entry => AttributeValue(entry, "Gesture"))
            .OfType<string>()
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(gestures, Is.Unique);
            Assert.That(gestures.Intersect(TextEditingCollisionGestures), Is.Empty);
        });
    }

    [Test]
    public void OpenRecentIsASubmenuFilledWhenItOpens()
    {
        var openRecent = Entry("File", "Open Recent");
        var submenu = openRecent.Elements(AvaloniaNamespace + "NativeMenu").Single();

        Assert.Multiple(() =>
        {
            Assert.That(AttributeValue(openRecent, "Command"), Is.Null);
            Assert.That(AttributeValue(submenu, "NeedsUpdate"), Is.EqualTo("OnOpenRecentMenuNeedsUpdate"));
        });
    }

    [TestCase("Shared-aware allocated size", StorageMeasurementMode.SharedAwareAllocated)]
    [TestCase("Allocated size per path", StorageMeasurementMode.Allocated)]
    [TestCase("Logical size", StorageMeasurementMode.Logical)]
    public void MeasurementModesAreRadioEntries(string header, StorageMeasurementMode mode)
    {
        var entry = Entry("View", header);
        var modeReference = $"{{x:Static coreScanning:StorageMeasurementMode.{mode}}}";

        Assert.Multiple(() =>
        {
            Assert.That(header, Is.EqualTo(StorageMeasurementModeLabelConverter.Label(mode)));
            Assert.That(AttributeValue(entry, "ToggleType"), Is.EqualTo("Radio"));
            Assert.That(
                AttributeValue(entry, "Command"),
                Is.EqualTo("{Binding SelectMeasurementModeCommand}"));
            Assert.That(AttributeValue(entry, "CommandParameter"), Is.EqualTo(modeReference));
            Assert.That(AttributeValue(entry, "IsChecked"), Does.Contain(modeReference));
            Assert.That(
                AttributeValue(entry, "IsChecked"),
                Does.StartWith("{Binding MeasurementMode, Mode=OneWay"));
        });
    }

    [TestCase("Scan Inside Application Bundles", "ExpandApplicationBundles")]
    [TestCase("Include Hidden Files", "IncludeHiddenFiles")]
    [TestCase("Follow Symbolic Links", "FollowSymbolicLinks")]
    public void ScanOptionsAreCheckboxEntries(string header, string property)
    {
        var entry = Entry("View", header);

        Assert.Multiple(() =>
        {
            Assert.That(AttributeValue(entry, "ToggleType"), Is.EqualTo("CheckBox"));
            Assert.That(
                AttributeValue(entry, "IsChecked"),
                Is.EqualTo($"{{Binding {property}, Mode=OneWay}}"));
        });
    }

    [Test]
    public void RecentLocationsMenuListsLocationsInOrderFollowedByClearMenu()
    {
        var viewModel = CreateViewModel(["/Users/me/Projects", "/Volumes/Archive", "/Users/me"]);
        var menu = new NativeMenu();

        MainWindow.PopulateRecentLocationsMenu(menu, viewModel);

        var locations = menu.Items
            .OfType<NativeMenuItem>()
            .Where(item => ReferenceEquals(item.Command, viewModel.ScanRecentLocationCommand))
            .ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(
                locations.Select(item => item.Header),
                Is.EqualTo(viewModel.RecentLocations));
            Assert.That(
                locations.Select(item => item.CommandParameter),
                Is.EqualTo(viewModel.RecentLocations));
            Assert.That(menu.Items[^2], Is.InstanceOf<NativeMenuItemSeparator>());
            Assert.That(((NativeMenuItem)menu.Items[^1]).Header, Is.EqualTo("Clear Menu"));
            Assert.That(
                ((NativeMenuItem)menu.Items[^1]).Command,
                Is.SameAs(viewModel.ClearRecentLocationsCommand));
        });
    }

    [Test]
    public void RecentLocationsMenuOffersOnlyClearMenuWithoutLocations()
    {
        var viewModel = CreateViewModel([]);
        var menu = new NativeMenu();

        MainWindow.PopulateRecentLocationsMenu(menu, viewModel);

        Assert.That(menu.Items, Has.Count.EqualTo(1));
        Assert.That(((NativeMenuItem)menu.Items[0]).Header, Is.EqualTo("Clear Menu"));
    }

    [Test]
    public void RecentLocationsMenuFollowsChangedLocationsWithoutDuplicatingEntries()
    {
        var viewModel = CreateViewModel(["/Users/me/Projects", "/Volumes/Archive"]);
        var menu = new NativeMenu();
        MainWindow.PopulateRecentLocationsMenu(menu, viewModel);
        var firstEntry = menu.Items[0];

        MainWindow.PopulateRecentLocationsMenu(menu, viewModel);
        var unchangedFirstEntry = menu.Items[0];
        viewModel.ClearRecentLocationsCommand.Execute(null);
        MainWindow.PopulateRecentLocationsMenu(menu, viewModel);

        Assert.Multiple(() =>
        {
            Assert.That(unchangedFirstEntry, Is.SameAs(firstEntry));
            Assert.That(menu.Items, Has.Count.EqualTo(1));
            Assert.That(((NativeMenuItem)menu.Items[0]).Header, Is.EqualTo("Clear Menu"));
        });
    }

    [Test]
    public void ChoosingAMissingRecentLocationRemovesItWithTheExistingNotice()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), $"atlas-missing-{Guid.NewGuid():N}");
        var viewModel = CreateViewModel([missingPath]);
        var menu = new NativeMenu();
        MainWindow.PopulateRecentLocationsMenu(menu, viewModel);
        var entry = (NativeMenuItem)menu.Items[0];

        entry.Command!.Execute(entry.CommandParameter);

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.RecentLocations, Is.Empty);
            Assert.That(viewModel.RecentLocationStatusMessage, Does.Contain("no longer exists"));
        });
    }

    [Test]
    public void TextBoxForEditingFindsTheFocusedTextBoxOrItsTemplateParent()
    {
        var textBox = new TextBox();

        Assert.Multiple(() =>
        {
            Assert.That(MainWindow.TextBoxForEditing(textBox), Is.SameAs(textBox));
            Assert.That(MainWindow.TextBoxForEditing(new Border()), Is.Null);
            Assert.That(MainWindow.TextBoxForEditing(null), Is.Null);
        });
    }

    [TestCase(StorageMeasurementMode.Logical, StorageMeasurementMode.Logical, true)]
    [TestCase(StorageMeasurementMode.Logical, StorageMeasurementMode.Allocated, false)]
    [TestCase(
        StorageMeasurementMode.SharedAwareAllocated,
        StorageMeasurementMode.SharedAwareAllocated,
        true)]
    public void MeasurementModeMatchConverterChecksTheSelectedMode(
        StorageMeasurementMode current,
        StorageMeasurementMode entry,
        bool expected)
    {
        var converted = StorageMeasurementModeMatchConverter.Instance.Convert(
            current,
            typeof(bool),
            entry,
            CultureInfo.InvariantCulture);

        Assert.That(converted, Is.EqualTo(expected));
    }

    [Test]
    public void MeasurementModeMatchConverterRejectsMissingValues()
    {
        var converted = StorageMeasurementModeMatchConverter.Instance.Convert(
            null,
            typeof(bool),
            StorageMeasurementMode.Logical,
            CultureInfo.InvariantCulture);

        Assert.That(converted, Is.EqualTo(false));
    }

    private static MainWindowViewModel CreateViewModel(IReadOnlyList<string> recentLocations)
    {
        var settings = Substitute.For<ISettingsService>();
        settings.Load().Returns(new AppSettings { RecentLocations = recentLocations.ToList() });

        return new MainWindowViewModel(
            Substitute.For<IFolderPickerService>(),
            Substitute.For<IDiskScanner>(),
            new ImmediateDispatcher(),
            Substitute.For<IFileRevealService>(),
            settingsService: settings,
            quickLookService: Substitute.For<IQuickLookService>());
    }

    private static IEnumerable<XElement> TopLevelMenus() =>
        XDocument.Load(MainWindowPath())
            .Root!
            .Elements(AvaloniaNamespace + "NativeMenu.Menu")
            .Single()
            .Elements(AvaloniaNamespace + "NativeMenu")
            .Single()
            .Elements(AvaloniaNamespace + "NativeMenuItem");

    private static XElement Submenu(string header) =>
        TopLevelMenus()
            .Single(menu => AttributeValue(menu, "Header") == header)
            .Elements(AvaloniaNamespace + "NativeMenu")
            .Single();

    private static IEnumerable<XElement> MenuEntries() =>
        TopLevelMenus()
            .SelectMany(menu => menu.Descendants(AvaloniaNamespace + "NativeMenuItem"));

    private static XElement Entry(string menu, string header) =>
        Submenu(menu)
            .Elements(AvaloniaNamespace + "NativeMenuItem")
            .Single(entry => AttributeValue(entry, "Header") == header);

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

        throw new FileNotFoundException("MainWindow.axaml was not found.");
    }

    private static string? AttributeValue(XElement element, string name) =>
        element
            .Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName == name)
            ?.Value;

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
    }
}
