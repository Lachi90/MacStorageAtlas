using MacStorageAtlas.Core;
using MacStorageAtlas.Rendering;

namespace MacStorageAtlas.Tests;

public class TreemapLayoutServiceTests
{
    private readonly ITreemapLayoutService _service = new TreemapLayoutService();

    [Test]
    public void LayoutUsesItemSizesAsAreaProportions()
    {
        var bounds = new TreemapBounds(0, 0, 100, 40);
        var rectangles = _service.Layout(
            [Item("large", 3), Item("small", 1)],
            bounds);

        Assert.Multiple(() =>
        {
            Assert.That(rectangles, Has.Count.EqualTo(2));
            Assert.That(rectangles[0].Width, Is.EqualTo(75).Within(0.000001));
            Assert.That(rectangles[1].Width, Is.EqualTo(25).Within(0.000001));
            Assert.That(rectangles[0].Width * rectangles[0].Height,
                Is.EqualTo(3 * rectangles[1].Width * rectangles[1].Height).Within(0.000001));
        });
    }

    [Test]
    public void LayoutKeepsEveryRectangleInsideOffsetBounds()
    {
        var bounds = new TreemapBounds(13, 17, 37, 101);
        var rectangles = _service.Layout(
            [Item("one", 5), Item("zero", 0), Item("two", 2), Item("three", 1)],
            bounds);

        Assert.That(rectangles, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            foreach (var rectangle in rectangles)
            {
                Assert.That(rectangle.X, Is.GreaterThanOrEqualTo(bounds.X));
                Assert.That(rectangle.Y, Is.GreaterThanOrEqualTo(bounds.Y));
                Assert.That(rectangle.X + rectangle.Width, Is.LessThanOrEqualTo(bounds.X + bounds.Width));
                Assert.That(rectangle.Y + rectangle.Height, Is.LessThanOrEqualTo(bounds.Y + bounds.Height));
                Assert.That(rectangle.Width, Is.GreaterThanOrEqualTo(0));
                Assert.That(rectangle.Height, Is.GreaterThanOrEqualTo(0));
            }
        });
    }

    [Test]
    public void LayoutOmitsSubPixelItems()
    {
        var rectangles = _service.Layout(
            [Item("huge", long.MaxValue), Item("tiny", 1)],
            new TreemapBounds(0, 0, 100, 100));

        var rectangle = rectangles.Single();
        Assert.That(rectangle.Item.Item.Name, Is.EqualTo("huge"));
    }

    [Test]
    public void LayoutUsesSquarifiedRowsForBalancedItems()
    {
        var rectangles = _service.Layout(
            [Item("one", 1), Item("two", 1), Item("three", 1), Item("four", 1)],
            new TreemapBounds(0, 0, 100, 100));

        Assert.Multiple(() =>
        {
            Assert.That(rectangles, Has.Count.EqualTo(4));
            foreach (var rectangle in rectangles)
            {
                Assert.That(rectangle.Width, Is.EqualTo(50).Within(0.000001));
                Assert.That(rectangle.Height, Is.EqualTo(50).Within(0.000001));
            }
        });
    }

    [Test]
    public void LayoutWithASingleItemFillsTheAvailableBounds()
    {
        var bounds = new TreemapBounds(5, 9, 120, 80);
        var rectangles = _service.Layout([Item("only", 42)], bounds);

        var rectangle = rectangles.Single();
        Assert.Multiple(() =>
        {
            Assert.That(rectangle.X, Is.EqualTo(bounds.X).Within(0.000001));
            Assert.That(rectangle.Y, Is.EqualTo(bounds.Y).Within(0.000001));
            Assert.That(rectangle.Width, Is.EqualTo(bounds.Width).Within(0.000001));
            Assert.That(rectangle.Height, Is.EqualTo(bounds.Height).Within(0.000001));
        });
    }

    [Test]
    public void LayoutWithEmptyInputReturnsNoRectangles()
    {
        var rectangles = _service.Layout([], new TreemapBounds(0, 0, 100, 100));

        Assert.That(rectangles, Is.Empty);
    }

    [Test]
    public void LayoutWithOnlyZeroSizeItemsReturnsNoRectangles()
    {
        var rectangles = _service.Layout(
            [Item("empty", 0)],
            new TreemapBounds(0, 0, 100, 100));

        Assert.That(rectangles, Is.Empty);
    }

    private static TreemapItem Item(string name, long size) =>
        new(new DiskItem(name, $"/{name}", isDirectory: false), size);
}
