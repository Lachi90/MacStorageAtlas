using System.Globalization;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Layout;
using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.App.Services;

public sealed class AvaloniaScanHistoryClearConfirmationService(Window owner)
    : IScanHistoryClearConfirmationService
{
    public Task<bool> ConfirmClearHistoryAsync(int snapshotCount, long totalSizeBytes)
    {
        var dialog = new Window
        {
            Title = "Clear scan history?",
            Width = 460,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var cancelButton = new Button { Content = "Cancel" };
        var clearButton = new Button { Content = "Clear History" };
        cancelButton.Click += (_, _) => dialog.Close(false);
        clearButton.Click += (_, _) => dialog.Close(true);

        var snapshots = snapshotCount.ToString("N0", CultureInfo.CurrentCulture);
        var heading = snapshotCount == 1
            ? "Clear 1 recorded scan?"
            : $"Clear {snapshots} recorded scans?";

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 18,
            Children =
            {
                new TextBlock
                {
                    Text = heading,
                    FontSize = 18,
                    FontWeight = Avalonia.Media.FontWeight.SemiBold,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                new TextBlock
                {
                    Text = "Recorded scans are deleted permanently rather than moved to "
                        + "Trash, so that clearing history removes the record of your "
                        + $"file names from this Mac. This frees "
                        + $"{FileSizeFormatter.Format(totalSizeBytes)}. Your scan "
                        + "options, filter presets, and recent locations are not "
                        + "changed.",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancelButton, clearButton }
                }
            }
        };

        return dialog.ShowDialog<bool>(owner);
    }
}
