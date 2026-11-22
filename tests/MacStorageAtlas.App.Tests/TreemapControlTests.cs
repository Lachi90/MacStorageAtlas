using Avalonia;
using Avalonia.Input;
using MacStorageAtlas.App.Controls;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Rendering;

namespace MacStorageAtlas.App.Tests;

public class TreemapControlTests
{
    private static readonly TreemapRect LeftBlock = Block("left", 0, 0, 100, 100);
    private static readonly TreemapRect RightBlock = Block("right", 100, 0, 100, 100);

    [Test]
    public void SecondaryPressOnABlockSelectsIt()
    {
        var control = CreateControl();
        control.SelectedRectangle = LeftBlock;

        var handled = control.HandlePointerPress(new Point(150, 50), PointerUpdateKind.RightButtonPressed);

        Assert.Multiple(() =>
        {
            Assert.That(control.SelectedRectangle, Is.EqualTo(RightBlock));
            Assert.That(handled, Is.True);
        });
    }

    [Test]
    public void SecondaryPressOnEmptySpaceKeepsTheSelectionAndStaysUnhandled()
    {
        var control = CreateControl();
        control.SelectedRectangle = LeftBlock;

        var handled = control.HandlePointerPress(new Point(150, 150), PointerUpdateKind.RightButtonPressed);

        Assert.Multiple(() =>
        {
            Assert.That(control.SelectedRectangle, Is.EqualTo(LeftBlock));
            Assert.That(handled, Is.False);
        });
    }

    [Test]
    public void PrimaryPressOnABlockSelectsIt()
    {
        var control = CreateControl();

        var handled = control.HandlePointerPress(new Point(50, 50), PointerUpdateKind.LeftButtonPressed);

        Assert.Multiple(() =>
        {
            Assert.That(control.SelectedRectangle, Is.EqualTo(LeftBlock));
            Assert.That(handled, Is.True);
        });
    }

    [Test]
    public void PrimaryPressOnEmptySpaceClearsTheSelection()
    {
        var control = CreateControl();
        control.SelectedRectangle = LeftBlock;

        var handled = control.HandlePointerPress(new Point(150, 150), PointerUpdateKind.LeftButtonPressed);

        Assert.Multiple(() =>
        {
            Assert.That(control.SelectedRectangle, Is.Null);
            Assert.That(handled, Is.True);
        });
    }

    [Test]
    public void OtherPressesLeaveTheSelectionUnchanged()
    {
        var control = CreateControl();
        control.SelectedRectangle = LeftBlock;

        var handled = control.HandlePointerPress(new Point(150, 50), PointerUpdateKind.MiddleButtonPressed);

        Assert.Multiple(() =>
        {
            Assert.That(control.SelectedRectangle, Is.EqualTo(LeftBlock));
            Assert.That(handled, Is.False);
        });
    }

    [Test]
    public void PointerContextOverABlockAllowsTheItemContextMenu()
    {
        var control = CreateControl();

        Assert.That(control.HasItemContextAt(new Point(150, 50)), Is.True);
    }

    [Test]
    public void PointerContextOverEmptySpaceOpensNoItemContextMenu()
    {
        var control = CreateControl();
        control.SelectedRectangle = LeftBlock;

        Assert.Multiple(() =>
        {
            Assert.That(control.HasItemContextAt(new Point(150, 150)), Is.False);
            Assert.That(control.SelectedRectangle, Is.EqualTo(LeftBlock));
        });
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public void KeyboardContextRequestRequiresASelectedBlock(bool hasSelection, bool expectedHandled)
    {
        var control = CreateControl();
        control.SelectedRectangle = hasSelection ? LeftBlock : null;
        var request = new ContextRequestedEventArgs();

        control.RaiseEvent(request);

        Assert.That(request.Handled, Is.EqualTo(expectedHandled));
    }

    private static TreemapControl CreateControl() =>
        new()
        {
            Rectangles = [LeftBlock, RightBlock]
        };

    private static TreemapRect Block(string name, double x, double y, double width, double height) =>
        new(
            new TreemapItem(new DiskItem(name, $"/scan/{name}", isDirectory: false)),
            x,
            y,
            width,
            height);
}
