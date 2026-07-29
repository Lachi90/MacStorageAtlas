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

        return false;
    }

    internal static bool IsTextEditingSource(object? source) =>
        source is Control control &&
        (control is TextBox || control.FindAncestorOfType<TextBox>() is not null);
}
