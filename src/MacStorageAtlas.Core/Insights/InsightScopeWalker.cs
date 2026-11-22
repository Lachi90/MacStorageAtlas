using MacStorageAtlas.Core.Items;

namespace MacStorageAtlas.Core.Insights;

internal static class InsightScopeWalker
{
    public static void VisitFiles(
        DiskItem scope,
        Func<DiskItem, bool>? include,
        CancellationToken cancellationToken,
        Action<DiskItem> visit)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var pending = new Stack<DiskItem>();
        pending.Push(scope);
        while (pending.Count > 0)
        {
            var item = pending.Pop();
            if (include is not null && !include(item))
            {
                continue;
            }

            if (!item.IsDirectory)
            {
                visit(item);
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            foreach (var child in item.Children)
            {
                pending.Push(child);
            }
        }
    }
}
