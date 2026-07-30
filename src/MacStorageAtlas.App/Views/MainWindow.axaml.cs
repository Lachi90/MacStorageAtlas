using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using MacStorageAtlas.App.ViewModels;

namespace MacStorageAtlas.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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
