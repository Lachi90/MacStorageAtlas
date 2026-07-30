using MacStorageAtlas.App.ViewModels;
using MacStorageAtlas.Core;

namespace MacStorageAtlas.Tests;

public class DiskItemTreeMaterializationTests
{
    private const int Depth = 5;
    private const int Branching = 8;

    [Test]
    public void UnfilteredPreparationMaterializesOnlyTheRootNode()
    {
        var root = BuildSyntheticTree();

        var items = DiskItemTreeFilter.Filter(root, searchText: null);

        Assert.That(CountMaterializedNodes(items), Is.EqualTo(1));
    }

    [Test]
    public void UnfilteredPreparationLeavesDescendantsUnmaterialized()
    {
        var root = BuildSyntheticTree();

        var rootNode = DiskItemTreeFilter.Filter(root, searchText: null).Single();

        Assert.That(rootNode.HasMaterializedChildren, Is.False);
    }

    [Test]
    public void ExpandingANodeMaterializesExactlyItsChildren()
    {
        var root = BuildSyntheticTree();
        var rootNode = DiskItemTreeFilter.Filter(root, searchText: null).Single();

        var children = rootNode.Children;

        Assert.Multiple(() =>
        {
            Assert.That(children, Has.Count.EqualTo(root.Children.Count));
            Assert.That(rootNode.HasMaterializedChildren, Is.True);
            Assert.That(
                children.Any(child => child.HasMaterializedChildren),
                Is.False);
        });
    }

    [Test]
    public void ExpandingANodeTwiceReturnsTheSameChildren()
    {
        var root = BuildSyntheticTree();
        var rootNode = DiskItemTreeFilter.Filter(root, searchText: null).Single();

        var first = rootNode.Children;
        var second = rootNode.Children;

        Assert.That(second, Is.SameAs(first));
    }

    [Test]
    public void ExpandingEveryNodeMaterializesTheWholeTree()
    {
        var root = BuildSyntheticTree();
        var expectedNodeCount = CountDiskItems(root);

        var items = DiskItemTreeFilter.Filter(root, searchText: null);
        MaterializeAll(items);

        Assert.That(CountMaterializedNodes(items), Is.EqualTo(expectedNodeCount));
    }

    [Test]
    public void FilteredPreparationMaterializesOnlyMatchesAndAncestors()
    {
        var root = BuildSyntheticTree();

        var items = DiskItemTreeFilter.Filter(root, "leaf-0-0-0-0-0");

        Assert.That(CountMaterializedNodes(items), Is.EqualTo(Depth + 1));
    }

    private static int CountMaterializedNodes(
        IReadOnlyList<DiskItemTreeNodeViewModel> nodes)
    {
        var total = 0;
        foreach (var node in nodes)
        {
            total++;
            if (node.HasMaterializedChildren)
            {
                total += CountMaterializedNodes(node.Children);
            }
        }

        return total;
    }

    private static void MaterializeAll(IReadOnlyList<DiskItemTreeNodeViewModel> nodes)
    {
        foreach (var node in nodes)
        {
            MaterializeAll(node.Children);
        }
    }

    private static int CountDiskItems(DiskItem item) =>
        1 + item.Children.Sum(CountDiskItems);

    private static DiskItem BuildSyntheticTree()
    {
        var root = new DiskItem("root", "/synthetic", isDirectory: true);
        AddChildren(root, "/synthetic", string.Empty, Depth);
        return root;
    }

    private static void AddChildren(
        DiskItem parent,
        string parentPath,
        string parentSuffix,
        int remainingDepth)
    {
        if (remainingDepth == 0)
        {
            return;
        }

        for (var index = 0; index < Branching; index++)
        {
            var suffix = $"{parentSuffix}-{index}";
            var isLeafLevel = remainingDepth == 1;
            var name = isLeafLevel ? $"leaf{suffix}" : $"dir{suffix}";
            var path = $"{parentPath}/{name}";
            var child = new DiskItem(name, path, isDirectory: !isLeafLevel);
            AddChildren(child, path, suffix, remainingDepth - 1);
            parent.AddChild(child);
        }
    }
}
