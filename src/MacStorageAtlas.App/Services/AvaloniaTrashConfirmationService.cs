using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.Services;

public sealed class AvaloniaTrashConfirmationService(Window owner) : ITrashConfirmationService
{
    public Task<bool> ConfirmMoveToTrashAsync(DiskItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var dialog = new Window
        {
            Title = "Move to Trash?",
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var cancelButton = new Button { Content = "Cancel" };
        var trashButton = new Button { Content = "Move to Trash" };
        cancelButton.Click += (_, _) => dialog.Close(false);
        trashButton.Click += (_, _) => dialog.Close(true);

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 18,
            Children =
            {
                new TextBlock
                {
                    Text = $"Move “{item.Name}” to Trash?",
                    FontSize = 18,
                    FontWeight = Avalonia.Media.FontWeight.SemiBold,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = "The item will be moved to the macOS Trash and will not be permanently deleted.",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancelButton, trashButton }
                }
            }
        };

        return dialog.ShowDialog<bool>(owner);
    }
}
