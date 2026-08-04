using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.Services;

public sealed class AvaloniaCleanupBasketReviewService(Window owner)
    : ICleanupBasketReviewService
{
    public Task<bool> ConfirmCleanupAsync(CleanupBasketReview review)
    {
        ArgumentNullException.ThrowIfNull(review);

        var dialog = new Window
        {
            Title = "Review Cleanup Basket",
            Width = 680,
            Height = 520,
            CanResize = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var cancelButton = new Button { Content = "Cancel" };
        var trashButton = new Button { Content = "Move to Trash" };
        cancelButton.Click += (_, _) => dialog.Close(false);
        trashButton.Click += (_, _) => dialog.Close(true);

        var itemList = new StackPanel { Spacing = 8 };
        foreach (var item in review.Items)
        {
            itemList.Children.Add(new StackPanel
            {
                Spacing = 2,
                Children =
                {
                    new TextBlock
                    {
                        Text = item.Item.Snapshot.Name,
                        FontWeight = FontWeight.SemiBold,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new TextBlock
                    {
                        Text = item.Item.Snapshot.Path,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = Brushes.Gray
                    },
                    new TextBlock
                    {
                        Text = item.CanExecute ? "Ready" : item.Status.Message,
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            });
        }

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = "Move items to Trash?",
                    FontSize = 18,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text =
                        $"Items: {review.Summary.ItemCount}  Logical: {FileSizeFormatter.Format(review.Summary.TotalLogicalSizeBytes)}  Expected reclaimable: {FileSizeFormatter.Format(review.Summary.ExpectedReclaimableSizeBytes)}",
                    TextWrapping = TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = "The items will be moved to the macOS Trash and will not be permanently deleted.",
                    TextWrapping = TextWrapping.Wrap
                },
                new ScrollViewer
                {
                    Content = itemList,
                    Height = 300
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
