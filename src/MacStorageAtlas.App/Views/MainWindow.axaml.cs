using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using MacStorageAtlas.App.ViewModels;

namespace MacStorageAtlas.App.Views;

public partial class MainWindow : Window
{
    internal const double FullToolbarActionThreshold = 1_050;

    internal const string ResultItemContextMenuKey = "ResultItemContextMenu";

    private const KeyModifiers CopyPathModifiers = KeyModifiers.Meta | KeyModifiers.Alt;

    private const KeyModifiers RescanFolderModifiers = KeyModifiers.Meta | KeyModifiers.Shift;

    internal const string ClearRecentLocationsMenuHeader = "Clear Menu";

    internal static readonly Uri SupportPageUri =
        new("https://lachi90.github.io/MacStorageAtlas/support.html");

    internal static readonly Uri PrivacyPolicyUri =
        new("https://lachi90.github.io/MacStorageAtlas/privacy.html");

    private readonly ContextMenu? _resultItemContextMenu;
    private bool _isApplyingSavedSize;
    private MainWindowViewModel? _observedViewModel;

    public MainWindow()
    {
        InitializeComponent();
        UpdateToolbarActionPresentation();
        DataContextChanged += OnDataContextChanged;
        SizeChanged += OnSizeChanged;

        _resultItemContextMenu =
            Resources.TryGetResource(ResultItemContextMenuKey, null, out var resource)
                ? resource as ContextMenu
                : null;

        if (_resultItemContextMenu is not null)
        {
            _resultItemContextMenu.Opening += OnResultItemContextMenuOpening;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        ObserveViewModel(DataContext as MainWindowViewModel);
        if (_resultItemContextMenu is not null)
        {
            _resultItemContextMenu.DataContext = DataContext;
        }

        ApplySavedWindowSize();
    }

    private void ObserveViewModel(MainWindowViewModel? viewModel)
    {
        if (ReferenceEquals(_observedViewModel, viewModel))
        {
            return;
        }

        if (_observedViewModel is not null)
        {
            _observedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _observedViewModel = viewModel;

        if (viewModel is not null)
        {
            viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is MainWindowViewModel viewModel
            && IsResultReplacement(e.PropertyName, viewModel.SelectedLargeFile is not null)
            && _resultItemContextMenu is { IsOpen: true } menu)
        {
            menu.Close();
        }
    }

    internal static bool IsResultReplacement(string? propertyName, bool isLargeFileSelected) =>
        propertyName switch
        {
            nameof(MainWindowViewModel.TreeItems)
                or nameof(MainWindowViewModel.TreemapRectangles)
                or nameof(MainWindowViewModel.DuplicateGroups) => true,
            nameof(MainWindowViewModel.LargeFiles) => isLargeFileSelected,
            _ => false
        };

    private void OnResultItemContextMenuOpening(object? sender, CancelEventArgs e)
    {
        if (sender is ContextMenu menu)
        {
            UpdateSeparatorVisibility(menu.Items);
        }
    }

    internal static void UpdateSeparatorVisibility(IEnumerable<object?> items)
    {
        Separator? pendingSeparator = null;
        var hasVisibleEntry = false;

        foreach (var item in items)
        {
            if (item is Separator separator)
            {
                separator.IsVisible = false;
                if (hasVisibleEntry)
                {
                    pendingSeparator = separator;
                }

                continue;
            }

            if (item is not Control { IsVisible: true })
            {
                continue;
            }

            if (pendingSeparator is not null)
            {
                pendingSeparator.IsVisible = true;
                pendingSeparator = null;
            }

            hasVisibleEntry = true;
        }
    }

    private void ApplySavedWindowSize()
    {
        if (DataContext is not MainWindowViewModel viewModel ||
            viewModel.InitialWindowWidth is not { } width ||
            viewModel.InitialWindowHeight is not { } height)
        {
            return;
        }

        _isApplyingSavedSize = true;
        try
        {
            Width = Math.Max(width, MinWidth);
            Height = Math.Max(height, MinHeight);
        }
        finally
        {
            _isApplyingSavedSize = false;
        }
    }

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateToolbarActionPresentation();

        if (_isApplyingSavedSize ||
            WindowState != WindowState.Normal ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.SaveWindowSize(Bounds.Width, Bounds.Height);
    }

    private void UpdateToolbarActionPresentation()
    {
        var width = Bounds.Width > 0 ? Bounds.Width : Width;
        var showFullActions = width >= FullToolbarActionThreshold;

        if (this.FindControl<Control>("FullActionGroup") is { } fullActionGroup)
        {
            fullActionGroup.IsVisible = showFullActions;
        }

        if (this.FindControl<Control>("CompactActionsButton") is { } compactActionsButton)
        {
            compactActionsButton.IsVisible = !showFullActions;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (TryHandleInspectionShortcut(e))
        {
            return;
        }

        base.OnKeyDown(e);
    }

    internal bool TryHandleInspectionShortcut(KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return false;
        }

        if (TryHandleSelectedItemShortcut(viewModel, e))
        {
            return true;
        }

        if (e.Key == Key.F && e.KeyModifiers.HasFlag(KeyModifiers.Meta))
        {
            e.Handled = FocusSearchBox();
            return e.Handled;
        }

        return false;
    }

    private bool FocusSearchBox()
    {
        if (this.FindControl<TextBox>("SearchBox") is not { } searchBox)
        {
            return false;
        }

        searchBox.Focus();
        searchBox.SelectAll();
        return true;
    }

    internal static bool TryHandleSelectedItemShortcut(MainWindowViewModel viewModel, KeyEventArgs e)
    {
        if (SelectedItemShortcutCommand(viewModel, e) is not { } command ||
            !command.CanExecute(null))
        {
            return false;
        }

        command.Execute(null);
        e.Handled = true;
        return true;
    }

    private static ICommand? SelectedItemShortcutCommand(MainWindowViewModel viewModel, KeyEventArgs e) =>
        e.Key switch
        {
            Key.Space when !IsTextEditingSource(e.Source) => viewModel.QuickLookCommand,
            Key.C when e.KeyModifiers == CopyPathModifiers && !IsTextEditingSource(e.Source) =>
                viewModel.CopySelectedItemPathCommand,
            Key.R when e.KeyModifiers == RescanFolderModifiers && !IsTextEditingSource(e.Source) =>
                viewModel.RescanFolderCommand,
            Key.B when e.KeyModifiers == KeyModifiers.Meta && !IsTextEditingSource(e.Source) =>
                viewModel.ToggleSelectedItemInCleanupBasketCommand,
            Key.Back when e.KeyModifiers == KeyModifiers.Meta && !IsTextEditingSource(e.Source) =>
                viewModel.MoveToTrashCommand,
            _ => null
        };

    internal static bool IsTextEditingSource(object? source) =>
        source is Control control &&
        (control is TextBox || control.FindAncestorOfType<TextBox>() is not null);

    internal static TextBox? TextBoxForEditing(object? focusedElement) =>
        focusedElement switch
        {
            TextBox textBox => textBox,
            Control control => control.FindAncestorOfType<TextBox>(),
            _ => null
        };

    private TextBox? FocusedTextBox() => TextBoxForEditing(FocusManager?.GetFocusedElement());

    private void OnUndoMenuItemClick(object? sender, EventArgs e) => FocusedTextBox()?.Undo();

    private void OnRedoMenuItemClick(object? sender, EventArgs e) => FocusedTextBox()?.Redo();

    private void OnCutMenuItemClick(object? sender, EventArgs e) => FocusedTextBox()?.Cut();

    private void OnCopyMenuItemClick(object? sender, EventArgs e) => FocusedTextBox()?.Copy();

    private void OnPasteMenuItemClick(object? sender, EventArgs e) => FocusedTextBox()?.Paste();

    private void OnSelectAllMenuItemClick(object? sender, EventArgs e) =>
        FocusedTextBox()?.SelectAll();

    private void OnFilterAndSearchMenuItemClick(object? sender, EventArgs e) => FocusSearchBox();

    private void OnScanHistoryMenuItemClick(object? sender, EventArgs e)
    {
        if (this.FindControl<Button>("ScanHistoryButton") is not { } button)
        {
            return;
        }

        if (button.Command is { } command && command.CanExecute(button.CommandParameter))
        {
            command.Execute(button.CommandParameter);
        }

        button.Flyout?.ShowAt(button);
    }

    private void OnMinimizeMenuItemClick(object? sender, EventArgs e) =>
        WindowState = WindowState.Minimized;

    private void OnZoomMenuItemClick(object? sender, EventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;

    private async void OnSupportMenuItemClick(object? sender, EventArgs e) =>
        await Launcher.LaunchUriAsync(SupportPageUri);

    private async void OnPrivacyPolicyMenuItemClick(object? sender, EventArgs e) =>
        await Launcher.LaunchUriAsync(PrivacyPolicyUri);

    private void OnOpenRecentMenuNeedsUpdate(object? sender, EventArgs e)
    {
        if (sender is NativeMenu menu && DataContext is MainWindowViewModel viewModel)
        {
            PopulateRecentLocationsMenu(menu, viewModel);
        }
    }

    internal static void PopulateRecentLocationsMenu(NativeMenu menu, MainWindowViewModel viewModel)
    {
        var recentLocations = viewModel.RecentLocations;
        if (IsRecentLocationsMenuCurrent(menu, viewModel))
        {
            return;
        }

        menu.Items.Clear();
        foreach (var path in recentLocations)
        {
            menu.Items.Add(new NativeMenuItem(path)
            {
                Command = viewModel.ScanRecentLocationCommand,
                CommandParameter = path
            });
        }

        if (recentLocations.Count > 0)
        {
            menu.Items.Add(new NativeMenuItemSeparator());
        }

        menu.Items.Add(new NativeMenuItem(ClearRecentLocationsMenuHeader)
        {
            Command = viewModel.ClearRecentLocationsCommand
        });
    }

    private static bool IsRecentLocationsMenuCurrent(
        NativeMenu menu,
        MainWindowViewModel viewModel)
    {
        var locationEntries = menu.Items
            .OfType<NativeMenuItem>()
            .Where(item => ReferenceEquals(item.Command, viewModel.ScanRecentLocationCommand))
            .Select(item => item.CommandParameter as string)
            .ToArray();

        return menu.Items
                .OfType<NativeMenuItem>()
                .Any(item => ReferenceEquals(item.Command, viewModel.ClearRecentLocationsCommand))
            && locationEntries.SequenceEqual(viewModel.RecentLocations);
    }
}
