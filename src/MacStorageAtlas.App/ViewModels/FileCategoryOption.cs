using System;
using CommunityToolkit.Mvvm.ComponentModel;
using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.App.ViewModels;

public sealed partial class FileCategoryOption : ObservableObject
{
    private readonly Action _selectionChanged;
    private bool _suppressNotification;

    internal FileCategoryOption(FileCategory category, Action selectionChanged)
    {
        ArgumentNullException.ThrowIfNull(selectionChanged);

        Category = category;
        _selectionChanged = selectionChanged;
    }

    public FileCategory Category { get; }

    public string DisplayName => Category switch
    {
        FileCategory.Archive => "Archives",
        FileCategory.Video => "Video",
        FileCategory.Image => "Images",
        FileCategory.Audio => "Audio",
        FileCategory.Document => "Documents",
        FileCategory.DiskImage => "Disk images and installers",
        FileCategory.Code => "Code",
        _ => throw new ArgumentOutOfRangeException(nameof(Category), Category, null)
    };

    [ObservableProperty]
    private bool _isSelected;

    internal void SetSelectedSilently(bool value)
    {
        _suppressNotification = true;
        try
        {
            IsSelected = value;
        }
        finally
        {
            _suppressNotification = false;
        }
    }

    partial void OnIsSelectedChanged(bool value)
    {
        if (!_suppressNotification)
        {
            _selectionChanged();
        }
    }
}
