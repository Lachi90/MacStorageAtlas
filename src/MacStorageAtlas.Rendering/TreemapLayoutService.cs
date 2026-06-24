namespace MacStorageAtlas.Rendering;

/// <summary>
/// Lays items out as a squarified treemap to keep visible rectangles compact.
/// </summary>
public sealed class TreemapLayoutService : ITreemapLayoutService
{
    private const double MinimumRenderableArea = 1;

    public IReadOnlyList<TreemapRect> Layout(
        IReadOnlyList<TreemapItem> items,
        TreemapBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (!HasUsableBounds(bounds) || items.Count == 0)
        {
            return [];
        }

        var totalSize = items.Where(item => item.SizeBytes > 0).Sum(item => (double)item.SizeBytes);
        if (!double.IsFinite(totalSize) || totalSize <= 0)
        {
            return [];
        }

        var boundsArea = bounds.Width * bounds.Height;
        var visibleItems = items
            .Where(item => item.SizeBytes > 0)
            .OrderByDescending(item => item.SizeBytes)
            .Select(item => new WeightedTreemapItem(
                item,
                Math.Max(0, item.SizeBytes / totalSize * boundsArea)))
            .Where(item => item.Area >= MinimumRenderableArea)
            .ToArray();

        if (visibleItems.Length == 0)
        {
            return [];
        }

        var visibleArea = visibleItems.Sum(item => item.Area);
        var positiveItems = visibleItems
            .Select(item => item with { Area = item.Area / visibleArea * boundsArea })
            .ToArray();

        var rectangles = new List<TreemapRect>(positiveItems.Length);
        var remainingBounds = bounds;
        var row = new List<WeightedTreemapItem>();

        foreach (var item in positiveItems)
        {
            if (row.Count == 0 || ImprovesAspectRatio(row, item, ShortSide(remainingBounds)))
            {
                row.Add(item);
                continue;
            }

            LayoutRow(row, remainingBounds, rectangles, out remainingBounds);
            row.Clear();
            row.Add(item);
        }

        if (row.Count > 0)
        {
            LayoutRow(row, remainingBounds, rectangles, out _);
        }

        return rectangles;
    }

    private static bool HasUsableBounds(TreemapBounds bounds) =>
        double.IsFinite(bounds.X)
        && double.IsFinite(bounds.Y)
        && double.IsFinite(bounds.Width)
        && double.IsFinite(bounds.Height)
        && bounds.Width > 0
        && bounds.Height > 0;

    private static double ShortSide(TreemapBounds bounds) =>
        Math.Min(bounds.Width, bounds.Height);

    private static bool ImprovesAspectRatio(
        IReadOnlyList<WeightedTreemapItem> row,
        WeightedTreemapItem item,
        double sideLength)
    {
        if (sideLength <= 0)
        {
            return false;
        }

        var currentWorst = WorstAspectRatio(row, sideLength);
        var next = new WeightedTreemapItem[row.Count + 1];
        for (var index = 0; index < row.Count; index++)
        {
            next[index] = row[index];
        }

        next[^1] = item;
        return WorstAspectRatio(next, sideLength) <= currentWorst;
    }

    private static double WorstAspectRatio(
        IReadOnlyList<WeightedTreemapItem> row,
        double sideLength)
    {
        if (row.Count == 0)
        {
            return double.PositiveInfinity;
        }

        var sum = 0d;
        var min = double.PositiveInfinity;
        var max = 0d;

        foreach (var item in row)
        {
            sum += item.Area;
            min = Math.Min(min, item.Area);
            max = Math.Max(max, item.Area);
        }

        if (sum <= 0 || min <= 0)
        {
            return double.PositiveInfinity;
        }

        var sideSquared = sideLength * sideLength;
        return Math.Max(
            sideSquared * max / (sum * sum),
            (sum * sum) / (sideSquared * min));
    }

    private static void LayoutRow(
        IReadOnlyList<WeightedTreemapItem> row,
        TreemapBounds bounds,
        ICollection<TreemapRect> rectangles,
        out TreemapBounds remainingBounds)
    {
        var rowArea = row.Sum(item => item.Area);
        if (rowArea <= 0)
        {
            remainingBounds = bounds;
            return;
        }

        if (bounds.Width >= bounds.Height)
        {
            var rowWidth = Math.Min(bounds.Width, rowArea / bounds.Height);
            var y = bounds.Y;
            var bottom = bounds.Y + bounds.Height;

            for (var index = 0; index < row.Count; index++)
            {
                var item = row[index];
                var nextY = index == row.Count - 1
                    ? bottom
                    : Math.Min(bottom, y + item.Area / rowWidth);

                rectangles.Add(new TreemapRect(
                    item.Item,
                    bounds.X,
                    y,
                    rowWidth,
                    Math.Max(0, nextY - y)));
                y = nextY;
            }

            remainingBounds = new TreemapBounds(
                bounds.X + rowWidth,
                bounds.Y,
                Math.Max(0, bounds.Width - rowWidth),
                bounds.Height);
            return;
        }

        var rowHeight = Math.Min(bounds.Height, rowArea / bounds.Width);
        var x = bounds.X;
        var right = bounds.X + bounds.Width;

        for (var index = 0; index < row.Count; index++)
        {
            var item = row[index];
            var nextX = index == row.Count - 1
                ? right
                : Math.Min(right, x + item.Area / rowHeight);

            rectangles.Add(new TreemapRect(
                item.Item,
                x,
                bounds.Y,
                Math.Max(0, nextX - x),
                rowHeight));
            x = nextX;
        }

        remainingBounds = new TreemapBounds(
            bounds.X,
            bounds.Y + rowHeight,
            bounds.Width,
            Math.Max(0, bounds.Height - rowHeight));
    }

    private readonly record struct WeightedTreemapItem(TreemapItem Item, double Area);
}
