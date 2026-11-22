using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using MacStorageAtlas.App.Converters;
using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.App.Services;

public sealed class AvaloniaFolderRescanConfirmationService(Window owner)
    : IFolderRescanConfirmationService
{
    public Task<bool> ConfirmFullRescanAsync(
        DiskItem folder,
        string scanRootPath,
        string explanation)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentException.ThrowIfNullOrWhiteSpace(scanRootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(explanation);

        var dialog = new Window
        {
            Title = "Rescan the whole result?",
            Width = 480,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var cancelButton = new Button { Content = "Cancel" };
        var rescanButton = new Button { Content = "Rescan All" };
        cancelButton.Click += (_, _) => dialog.Close(false);
        rescanButton.Click += (_, _) => dialog.Close(true);

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 18,
            Children =
            {
                new TextBlock
                {
                    Text = $"“{DisplayNameConverter.Display(folder.Name)}” cannot be rescanned on its own",
                    FontSize = 18,
                    FontWeight = Avalonia.Media.FontWeight.SemiBold,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = explanation,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = $"Rescan All measures {scanRootPath} again with the scan options of the displayed result.",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancelButton, rescanButton }
                }
            }
        };

        return dialog.ShowDialog<bool>(owner);
    }
}
