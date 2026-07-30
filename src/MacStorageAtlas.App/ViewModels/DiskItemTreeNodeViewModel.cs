using System;
using System.Collections.Generic;
using System.Linq;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.ViewModels;

public sealed class DiskItemTreeNodeViewModel
{
    private IReadOnlyList<DiskItemTreeNodeViewModel>? _children;

    public DiskItemTreeNodeViewModel(DiskItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Item = item;
    }

    internal DiskItemTreeNodeViewModel(
        DiskItem item,
        IReadOnlyList<DiskItemTreeNodeViewModel> children)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(children);

        Item = item;
        _children = children;
    }

    public DiskItem Item { get; }

    public bool IsExpanded { get; set; }

    public string Name => Item.Name;

    public long SizeBytes => Item.SizeBytes;

    public string FormattedSize =>
        Item.IsSizeCountedElsewhere
            ? $"{FileSizeFormatter.Format(Item.SizeBytes)} counted, "
              + $"{FileSizeFormatter.Format(Item.SharedSizeBytes)} shared"
            : FileSizeFormatter.Format(SizeBytes);

    internal bool HasMaterializedChildren => _children is not null;

    public IReadOnlyList<DiskItemTreeNodeViewModel> Children =>
        _children ??= Item.Children
            .Select(child => new DiskItemTreeNodeViewModel(child))
            .ToArray();
}
