namespace MacStorageAtlas.Rendering;

/// <summary>
/// A treemap item positioned within the layout bounds.
/// </summary>
public readonly record struct TreemapRect(
    TreemapItem Item,
    double X,
    double Y,
    double Width,
    double Height);
