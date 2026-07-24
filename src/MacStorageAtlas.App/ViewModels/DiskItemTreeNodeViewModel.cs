using System;
using System.Collections.Generic;
using System.Linq;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.ViewModels;

public sealed class DiskItemTreeNodeViewModel
{
    public DiskItemTreeNodeViewModel(DiskItem item)
        : this(
            item,
            item?.Children.Select(child => new DiskItemTreeNodeViewModel(child)).ToArray()
                ?? throw new ArgumentNullException(nameof(item)))
    {
    }

    internal DiskItemTreeNodeViewModel(
        DiskItem item,
        IReadOnlyList<DiskItemTreeNodeViewModel> children)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(children);

        Item = item;
        Children = children;
    }

    public DiskItem Item { get; }

    public bool IsExpanded { get; set; }

    public string Name => Item.Name;

    public long SizeBytes => Item.SizeBytes;

    public string FormattedSize => FileSizeFormatter.Format(SizeBytes);

    public IReadOnlyList<DiskItemTreeNodeViewModel> Children { get; }
}
