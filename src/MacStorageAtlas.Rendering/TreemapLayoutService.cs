namespace MacStorageAtlas.Rendering;

/// <summary>
/// Lays items out as proportional slices along the longest side of the bounds.
/// </summary>
public sealed class TreemapLayoutService : ITreemapLayoutService
{
    public IReadOnlyList<TreemapRect> Layout(
        IReadOnlyList<TreemapItem> items,
        TreemapBounds bounds)
    {
        ArgumentNullException.ThrowIfNull(items);

        if (!HasUsableBounds(bounds) || items.Count == 0)
        {
            return [];
        }

        var positiveItems = items.Where(item => item.SizeBytes > 0).ToArray();
        if (positiveItems.Length == 0)
        {
            return [];
        }

        var totalSize = positiveItems.Sum(item => (double)item.SizeBytes);
        var rectangles = new List<TreemapRect>(positiveItems.Length);
        var sliceHorizontally = bounds.Width >= bounds.Height;
        var position = sliceHorizontally ? bounds.X : bounds.Y;
        var limit = position + (sliceHorizontally ? bounds.Width : bounds.Height);

        for (var index = 0; index < positiveItems.Length; index++)
        {
            var item = positiveItems[index];
            // Make the final slice consume the exact remainder. This prevents
            // accumulated floating-point error from crossing the far boundary.
            var nextPosition = index == positiveItems.Length - 1
                ? limit
                : position + ((sliceHorizontally ? bounds.Width : bounds.Height)
                    * item.SizeBytes / totalSize);
            nextPosition = Math.Clamp(nextPosition, position, limit);

            rectangles.Add(sliceHorizontally
                ? new TreemapRect(item, position, bounds.Y, nextPosition - position, bounds.Height)
                : new TreemapRect(item, bounds.X, position, bounds.Width, nextPosition - position));

            position = nextPosition;
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
}
