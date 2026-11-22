using Avalonia.Controls;
using Avalonia.Input;
using MacStorageAtlas.App.Services;
using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.App.Views;
using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Platform;
using MacStorageAtlas.Core.Scanning;
using NSubstitute;

namespace MacStorageAtlas.App.Tests;

public class MainWindowSelectedItemShortcutTests
{
    private const KeyModifiers CopyPathModifiers = KeyModifiers.Meta | KeyModifiers.Alt;

    [Test]
    public void CommandOptionCCopiesTheSelectedItemPath()
    {
        var clipboard = Substitute.For<IClipboardService>();
        var viewModel = CreateViewModel(clipboard);
        viewModel.SelectedLargeFile = new DiskItem("movie.mov", "/scan/movie.mov", isDirectory: false);
        var keyEvent = KeyDown(Key.C, CopyPathModifiers, new Border());

        var handled = MainWindow.TryHandleSelectedItemShortcut(viewModel, keyEvent);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(keyEvent.Handled, Is.True);
        });
        clipboard.Received(1).SetTextAsync("/scan/movie.mov");
    }

    [Test]
    public void CommandOptionCLeavesTheClipboardUnchangedWithoutASelection()
    {
        var clipboard = Substitute.For<IClipboardService>();
        var viewModel = CreateViewModel(clipboard);
        var keyEvent = KeyDown(Key.C, CopyPathModifiers, new Border());

        var handled = MainWindow.TryHandleSelectedItemShortcut(viewModel, keyEvent);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.False);
            Assert.That(keyEvent.Handled, Is.False);
        });
        clipboard.DidNotReceive().SetTextAsync(Arg.Any<string>());
    }

    [Test]
    public void CommandOptionCIsIgnoredWhileATextFieldHasFocus()
    {
        var clipboard = Substitute.For<IClipboardService>();
        var viewModel = CreateViewModel(clipboard);
        viewModel.SelectedLargeFile = new DiskItem("movie.mov", "/scan/movie.mov", isDirectory: false);
        var keyEvent = KeyDown(Key.C, CopyPathModifiers, new TextBox { Name = "SearchBox" });

        var handled = MainWindow.TryHandleSelectedItemShortcut(viewModel, keyEvent);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.False);
            Assert.That(keyEvent.Handled, Is.False);
        });
        clipboard.DidNotReceive().SetTextAsync(Arg.Any<string>());
    }

    [TestCase(KeyModifiers.Meta)]
    [TestCase(KeyModifiers.Alt)]
    [TestCase(KeyModifiers.Meta | KeyModifiers.Alt | KeyModifiers.Shift)]
    public void OtherCModifierCombinationsDoNotCopyThePath(KeyModifiers modifiers)
    {
        var clipboard = Substitute.For<IClipboardService>();
        var viewModel = CreateViewModel(clipboard);
        viewModel.SelectedLargeFile = new DiskItem("movie.mov", "/scan/movie.mov", isDirectory: false);
        var keyEvent = KeyDown(Key.C, modifiers, new Border());

        var handled = MainWindow.TryHandleSelectedItemShortcut(viewModel, keyEvent);

        Assert.That(handled, Is.False);
        clipboard.DidNotReceive().SetTextAsync(Arg.Any<string>());
    }

    [Test]
    public void SpaceStillOpensQuickLookForTheSelectedItem()
    {
        var quickLook = Substitute.For<IQuickLookService>();
        quickLook.Preview(Arg.Any<string>()).Returns(true);
        var viewModel = CreateViewModel(Substitute.For<IClipboardService>(), quickLook);
        viewModel.SelectedLargeFile = new DiskItem("movie.mov", "/scan/movie.mov", isDirectory: false);
        var keyEvent = KeyDown(Key.Space, KeyModifiers.None, new Border());

        var handled = MainWindow.TryHandleSelectedItemShortcut(viewModel, keyEvent);

        Assert.That(handled, Is.True);
        quickLook.Received(1).Preview("/scan/movie.mov");
    }

    [Test]
    public void CommandIIsLeftToTheMenuBarSoDetailsOpenOnlyOnce()
    {
        var viewModel = CreateViewModel(Substitute.For<IClipboardService>());
        viewModel.SelectedLargeFile = new DiskItem("movie.mov", "/scan/movie.mov", isDirectory: false);
        viewModel.SelectedResultsTabIndex = 2;
        var keyEvent = KeyDown(Key.I, KeyModifiers.Meta, new Border());

        var handled = MainWindow.TryHandleSelectedItemShortcut(viewModel, keyEvent);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.False);
            Assert.That(keyEvent.Handled, Is.False);
            Assert.That(viewModel.SelectedResultsTabIndex, Is.EqualTo(2));
            Assert.That(viewModel.ShowSelectedItemDetailsCommand.CanExecute(null), Is.True);
        });
    }

    [Test]
    public void SpaceIsIgnoredWhileATextFieldHasFocus()
    {
        var quickLook = Substitute.For<IQuickLookService>();
        var viewModel = CreateViewModel(Substitute.For<IClipboardService>(), quickLook);
        viewModel.SelectedLargeFile = new DiskItem("movie.mov", "/scan/movie.mov", isDirectory: false);
        var keyEvent = KeyDown(Key.Space, KeyModifiers.None, new TextBox { Name = "SearchBox" });

        var handled = MainWindow.TryHandleSelectedItemShortcut(viewModel, keyEvent);

        Assert.That(handled, Is.False);
        quickLook.DidNotReceive().Preview(Arg.Any<string>());
    }

    [Test]
    public void SpaceIsIgnoredWithoutASelectedItem()
    {
        var quickLook = Substitute.For<IQuickLookService>();
        var viewModel = CreateViewModel(Substitute.For<IClipboardService>(), quickLook);
        var keyEvent = KeyDown(Key.Space, KeyModifiers.None, new Border());

        var handled = MainWindow.TryHandleSelectedItemShortcut(viewModel, keyEvent);

        Assert.That(handled, Is.False);
        quickLook.DidNotReceive().Preview(Arg.Any<string>());
    }

    private static KeyEventArgs KeyDown(Key key, KeyModifiers modifiers, Control source) =>
        new()
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = key,
            KeyModifiers = modifiers,
            Source = source
        };

    private static MainWindowViewModel CreateViewModel(
        IClipboardService clipboard,
        IQuickLookService? quickLookService = null) =>
        new(
            Substitute.For<IFolderPickerService>(),
            Substitute.For<IDiskScanner>(),
            new ImmediateDispatcher(),
            Substitute.For<IFileRevealService>(),
            clipboardService: clipboard,
            quickLookService: quickLookService ?? Substitute.For<IQuickLookService>());

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }
    }
}
