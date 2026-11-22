using System;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.App.ViewModels;

public partial class MainWindowViewModel
{
    public const string ScanLabel = "Scan";

    public const string RescanLabel = "Rescan";

    public const string AddToCleanupBasketLabel = "Add to Cleanup Basket";

    public const string RemoveFromCleanupBasketLabel = "Remove from Cleanup Basket";

    public string ScanCommandLabel =>
        IsCompletedResultDisplayedForSelectedFolder() ? RescanLabel : ScanLabel;

    public string CleanupBasketCommandLabel =>
        IsSelectedItemInCleanupBasket() ? RemoveFromCleanupBasketLabel : AddToCleanupBasketLabel;

    partial void OnSelectedFolderPathChanged(string? value)
    {
        OnPropertyChanged(nameof(ScanCommandLabel));
        NotifyPrimaryScanActionChanged();
    }

    private bool IsCompletedResultDisplayedForSelectedFolder() =>
        _scanRoot is { } root
        && ScanCompletedAt is not null
        && SelectedFolderPath is { } selectedPath
        && !string.IsNullOrWhiteSpace(selectedPath)
        && string.Equals(
            NormalizeFolderPath(root.Path),
            NormalizeFolderPath(selectedPath),
            StringComparison.Ordinal);

    private static string NormalizeFolderPath(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private bool IsSelectedItemInCleanupBasket() =>
        SelectedItem is { } item
        && CleanupBasketItems.Any(basketItem => ReferenceEquals(basketItem.Item, item));

    private bool CanToggleSelectedItemInCleanupBasket() =>
        IsSelectedItemInCleanupBasket()
            ? CanRemoveSelectedItemFromCleanupBasket()
            : CanAddSelectedItemToCleanupBasket();

    [RelayCommand(CanExecute = nameof(CanToggleSelectedItemInCleanupBasket))]
    private void ToggleSelectedItemInCleanupBasket()
    {
        var command = IsSelectedItemInCleanupBasket()
            ? RemoveSelectedItemFromCleanupBasketCommand
            : AddSelectedItemToCleanupBasketCommand;

        if (command.CanExecute(null))
        {
            command.Execute(null);
        }
    }

    [RelayCommand]
    private void SelectMeasurementMode(StorageMeasurementMode mode) => MeasurementMode = mode;

    [RelayCommand]
    private void ToggleExpandApplicationBundles() =>
        ExpandApplicationBundles = !ExpandApplicationBundles;

    [RelayCommand]
    private void ToggleIncludeHiddenFiles() => IncludeHiddenFiles = !IncludeHiddenFiles;

    [RelayCommand]
    private void ToggleFollowSymbolicLinks() => FollowSymbolicLinks = !FollowSymbolicLinks;

    private void NotifyMenuLabelsChanged()
    {
        OnPropertyChanged(nameof(ScanCommandLabel));
        OnPropertyChanged(nameof(CleanupBasketCommandLabel));
        NotifyPrimaryScanActionChanged();
        NotifyCleanupBasketToggleChanged();
    }
}
