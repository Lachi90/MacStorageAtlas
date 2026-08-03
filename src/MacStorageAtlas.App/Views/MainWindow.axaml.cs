using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using MacStorageAtlas.App.ViewModels;

namespace MacStorageAtlas.App.Views;

public partial class MainWindow : Window
{
    private bool _isApplyingSavedSize;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        SizeChanged += OnSizeChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e) =>
        ApplySavedWindowSize();

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
        if (_isApplyingSavedSize ||
            WindowState != WindowState.Normal ||
            DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.SaveWindowSize(Bounds.Width, Bounds.Height);
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

        if (e.Key == Key.Space &&
            !IsTextEditingSource(e.Source) &&
            viewModel.QuickLookCommand.CanExecute(null))
        {
            viewModel.QuickLookCommand.Execute(null);
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.I &&
            e.KeyModifiers.HasFlag(KeyModifiers.Meta) &&
            viewModel.ShowSelectedItemDetailsCommand.CanExecute(null))
        {
            viewModel.ShowSelectedItemDetailsCommand.Execute(null);
            e.Handled = true;
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

    internal static bool IsTextEditingSource(object? source) =>
        source is Control control &&
        (control is TextBox || control.FindAncestorOfType<TextBox>() is not null);
}
