using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.Tests;

public class DiskItemTreeFilterTests
{
    [Test]
    public void FilterMatchesNamesCaseInsensitivelyAndKeepsAncestors()
    {
        var root = CreateTree();

        var result = DiskItemTreeFilter.Filter(root, "REPORT");

        var rootNode = result.Single();
        Assert.Multiple(() =>
        {
            Assert.That(rootNode.Name, Is.EqualTo("root"));
            Assert.That(rootNode.Children.Select(item => item.Name), Is.EqualTo(["Documents"]));
            Assert.That(rootNode.Children.Single().Children.Select(item => item.Name),
                Is.EqualTo(["report.pdf"]));
        });
    }

    [Test]
    public void FilterMatchesPathsCaseInsensitively()
    {
        var result = DiskItemTreeFilter.Filter(CreateTree(), "/USERS/TEST/PHOTOS");

        Assert.That(result.Single().Children.Select(item => item.Name), Is.EqualTo(["Photos"]));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void EmptySearchRestoresTheCompleteTree(string? searchText)
    {
        var result = DiskItemTreeFilter.Filter(CreateTree(), searchText);

        Assert.That(result.Single().Children.Select(item => item.Name),
            Is.EqualTo(["Documents", "Photos"]));
    }

    [Test]
    public void FilterReturnsNoRootsWhenNothingMatches()
    {
        var result = DiskItemTreeFilter.Filter(CreateTree(), "missing");

        Assert.That(result, Is.Empty);
    }

    private static DiskItem CreateTree()
    {
        var root = new DiskItem("root", "/Users/test", isDirectory: true);
        var documents = new DiskItem("Documents", "/Users/test/Documents", isDirectory: true);
        documents.AddChild(new DiskItem(
            "report.pdf",
            "/Users/test/Documents/report.pdf",
            isDirectory: false));
        documents.AddChild(new DiskItem(
            "notes.txt",
            "/Users/test/Documents/notes.txt",
            isDirectory: false));
        var photos = new DiskItem("Photos", "/Users/test/Photos", isDirectory: true);
        photos.AddChild(new DiskItem(
            "holiday.jpg",
            "/Users/test/Photos/holiday.jpg",
            isDirectory: false));
        root.AddChild(documents);
        root.AddChild(photos);
        return root;
    }
}
