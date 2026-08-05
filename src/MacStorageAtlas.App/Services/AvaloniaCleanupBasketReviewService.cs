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
            Title = review.Operation == CleanupOperationKind.Trash
                ? "Review Cleanup Basket"
                : "Review Cleanup Basket Transfer",
            Width = 680,
            Height = 520,
            CanResize = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var cancelButton = new Button { Content = "Cancel" };
        var confirmButton = new Button { Content = review.ConfirmButtonText };
        cancelButton.Click += (_, _) => dialog.Close(false);
        confirmButton.Click += (_, _) => dialog.Close(true);

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

        var content = new StackPanel
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 16,
            Children =
            {
                new TextBlock
                {
                    Text = review.OperationTitle,
                    FontSize = 18,
                    FontWeight = FontWeight.SemiBold,
                    TextWrapping = TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text =
                        $"Items: {review.Summary.ItemCount}  Logical: {FileSizeFormatter.Format(review.Summary.TotalLogicalSizeBytes)}  Expected reclaimable: {FileSizeFormatter.Format(review.ExpectedReclaimedSizeBytes)}",
                    TextWrapping = TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = review.OperationDescription,
                    TextWrapping = TextWrapping.Wrap
                }
            }
        };

        if (review.Destination is { } destination)
        {
            content.Children.Add(new TextBlock
            {
                Text = $"Destination: {destination.Path}",
                TextWrapping = TextWrapping.Wrap,
                FontWeight = FontWeight.SemiBold
            });
        }

        content.Children.Add(new ScrollViewer
        {
            Content = itemList,
            Height = 300
        });

        content.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { cancelButton, confirmButton }
        });

        dialog.Content = content;

        return dialog.ShowDialog<bool>(owner);
    }
}
