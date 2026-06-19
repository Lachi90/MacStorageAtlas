namespace MacStorageAtlas.Rendering;

public interface ITreemapLayoutService
{
    IReadOnlyList<TreemapRect> Layout(
        IReadOnlyList<TreemapItem> items,
        TreemapBounds bounds);
}
