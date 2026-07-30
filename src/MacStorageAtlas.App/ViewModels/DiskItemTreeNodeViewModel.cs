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
        IReadOnlyList<DiskItemTreeNodeViewModel> children,
        long? matchedSizeBytes = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(children);

        Item = item;
        _children = children;
        MatchedSizeBytes = matchedSizeBytes;
    }

    public DiskItem Item { get; }

    public bool IsExpanded { get; set; }

    public string Name => Item.Name;

    public long SizeBytes => Item.SizeBytes;

    public long? MatchedSizeBytes { get; }

    public bool HasMatchedSize => MatchedSizeBytes is not null;

    public string FormattedSize =>
        Item.IsSizeCountedElsewhere
            ? $"{FileSizeFormatter.Format(Item.SizeBytes)} counted, "
              + $"{FileSizeFormatter.Format(Item.SharedSizeBytes)} shared"
            : FileSizeFormatter.Format(SizeBytes);

    public string DisplaySize =>
        MatchedSizeBytes is { } matched
            ? FileSizeFormatter.Format(matched)
            : FormattedSize;

    internal bool HasMaterializedChildren => _children is not null;

    public IReadOnlyList<DiskItemTreeNodeViewModel> Children =>
        _children ??= Item.Children
            .Select(child => new DiskItemTreeNodeViewModel(child))
            .ToArray();
}
