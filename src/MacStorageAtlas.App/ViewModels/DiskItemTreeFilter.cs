using System;
using System.Collections.Generic;
using System.Linq;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.App.ViewModels;

public static class DiskItemTreeFilter
{
    public static IReadOnlyList<DiskItemTreeNodeViewModel> Filter(
        DiskItem root,
        string? searchText)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (string.IsNullOrWhiteSpace(searchText))
        {
            return [new DiskItemTreeNodeViewModel(root) { IsExpanded = true }];
        }

        var query = searchText.Trim();
        var filteredRoot = FilterNode(root, query);
        return filteredRoot is null ? [] : [filteredRoot];
    }

    private static DiskItemTreeNodeViewModel? FilterNode(DiskItem item, string query)
    {
        var children = item.Children
            .Select(child => FilterNode(child, query))
            .Where(child => child is not null)
            .Cast<DiskItemTreeNodeViewModel>()
            .ToArray();

        if (!Matches(item, query) && children.Length == 0)
        {
            return null;
        }

        return new DiskItemTreeNodeViewModel(item, children) { IsExpanded = children.Length > 0 };
    }

    private static bool Matches(DiskItem item, string query) =>
        item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
        item.Path.Contains(query, StringComparison.OrdinalIgnoreCase);
}
