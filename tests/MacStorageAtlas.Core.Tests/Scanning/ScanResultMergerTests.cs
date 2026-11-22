using MacStorageAtlas.Core.Items;
using MacStorageAtlas.Core.Scanning;

namespace MacStorageAtlas.Core.Tests.Scanning;

public class ScanResultMergerTests
{
    private static readonly DateTimeOffset ScanTime =
        new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void ReplaceSubtreeAddsGrowthToEveryAncestor()
    {
        var tree = Tree();
        var replacement = Folder("/root/a/project", file: ("big.bin", 500, 600, 0));

        var replaced = ScanResultMerger.ReplaceSubtree(tree.Root, "/root/a/project", replacement);

        Assert.Multiple(() =>
        {
            Assert.That(replaced, Is.True);
            Assert.That(tree.A.Children, Does.Contain(replacement));
            Assert.That(tree.A.SizeBytes, Is.EqualTo(510));
            Assert.That(tree.A.MeasuredSizeBytes, Is.EqualTo(612));
            Assert.That(tree.Root.SizeBytes, Is.EqualTo(540));
            Assert.That(tree.Root.MeasuredSizeBytes, Is.EqualTo(648));
        });
    }

    [Test]
    public void ReplaceSubtreeSubtractsShrinkFromEveryAncestorIncludingSharedBytes()
    {
        var tree = Tree();
        var replacement = Folder("/root/a/project", file: ("small.bin", 1, 2, 0));

        ScanResultMerger.ReplaceSubtree(tree.Root, "/root/a/project", replacement);

        Assert.Multiple(() =>
        {
            Assert.That(tree.A.SizeBytes, Is.EqualTo(11));
            Assert.That(tree.A.MeasuredSizeBytes, Is.EqualTo(14));
            Assert.That(tree.A.SharedSizeBytes, Is.Zero);
            Assert.That(tree.Root.SizeBytes, Is.EqualTo(41));
            Assert.That(tree.Root.SharedSizeBytes, Is.Zero);
        });
    }

    [Test]
    public void ReplaceSubtreeWithoutReplacementRemovesTheFolder()
    {
        var tree = Tree();

        var replaced = ScanResultMerger.ReplaceSubtree(tree.Root, "/root/a/project", null);

        Assert.Multiple(() =>
        {
            Assert.That(replaced, Is.True);
            Assert.That(tree.A.Children.Select(child => child.Name), Is.EqualTo(new[] { "keep.bin" }));
            Assert.That(tree.A.SizeBytes, Is.EqualTo(10));
            Assert.That(tree.Root.SizeBytes, Is.EqualTo(40));
            Assert.That(tree.Root.MeasuredSizeBytes, Is.EqualTo(48));
        });
    }

    [Test]
    public void ReplaceSubtreeReordersTouchedAncestorsBySize()
    {
        var tree = Tree();
        var replacement = Folder("/root/a/project", file: ("small.bin", 1, 1, 0));

        ScanResultMerger.ReplaceSubtree(tree.Root, "/root/a/project", replacement);

        Assert.Multiple(() =>
        {
            Assert.That(tree.Root.Children.Select(child => child.Name), Is.EqualTo(new[] { "b", "a" }));
            Assert.That(tree.A.Children.Select(child => child.Name), Is.EqualTo(new[] { "keep.bin", "project" }));
        });
    }

    [Test]
    public void ReplaceSubtreeKeepsAncestorAndSiblingIdentities()
    {
        var tree = Tree();
        var sibling = tree.B;
        var keep = tree.A.Children.Single(child => child.Name == "keep.bin");

        ScanResultMerger.ReplaceSubtree(
            tree.Root,
            "/root/a/project",
            Folder("/root/a/project", file: ("x.bin", 3, 3, 0)));

        Assert.Multiple(() =>
        {
            Assert.That(tree.Root.Children, Does.Contain(tree.A));
            Assert.That(tree.Root.Children, Does.Contain(sibling));
            Assert.That(tree.A.Children, Does.Contain(keep));
            Assert.That(sibling.SizeBytes, Is.EqualTo(30));
        });
    }

    [Test]
    public void ReplaceSubtreeReportsAPathThatIsNotInTheResult()
    {
        var tree = Tree();

        var replaced = ScanResultMerger.ReplaceSubtree(
            tree.Root,
            "/root/missing",
            Folder("/root/missing", file: ("x.bin", 3, 3, 0)));

        Assert.Multiple(() =>
        {
            Assert.That(replaced, Is.False);
            Assert.That(tree.Root.SizeBytes, Is.EqualTo(140));
        });
    }

    [Test]
    public void ReplaceSubtreeRejectsTheScanRootAndAMismatchedReplacement()
    {
        var tree = Tree();

        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() =>
                ScanResultMerger.ReplaceSubtree(tree.Root, "/root", null));
            Assert.Throws<ArgumentException>(() =>
                ScanResultMerger.ReplaceSubtree(
                    tree.Root,
                    "/root/a/project",
                    Folder("/root/a/other", file: ("x.bin", 1, 1, 0))));
        });
    }

    [Test]
    public void FindByPathLocatesNestedItemsAndIgnoresTrailingSeparators()
    {
        var tree = Tree();

        Assert.Multiple(() =>
        {
            Assert.That(ScanResultMerger.FindByPath(tree.Root, "/root/a/project/"), Is.SameAs(tree.Project));
            Assert.That(ScanResultMerger.FindByPath(tree.Root, "/root"), Is.SameAs(tree.Root));
            Assert.That(ScanResultMerger.FindByPath(tree.Root, "/root/ab"), Is.Null);
        });
    }

    [Test]
    public void MergeErrorsReplacesErrorsInsideTheFolderAndKeepsOthers()
    {
        var inside = new ScanError("/root/a/project/locked", "Denied.", "UnauthorizedAccessException");
        var atFolder = new ScanError("/root/a/project", "Denied.", "UnauthorizedAccessException");
        var outside = new ScanError("/root/b/locked", "Denied.", "UnauthorizedAccessException");
        var lookalike = new ScanError("/root/a/project-old", "Denied.", "IOException");
        var refreshed = new ScanError("/root/a/project/new", "Gone.", "IOException");

        var merged = ScanResultMerger.MergeErrors(
            [inside, atFolder, outside, lookalike],
            "/root/a/project",
            [refreshed]);

        Assert.That(merged, Is.EqualTo(new[] { outside, lookalike, refreshed }));
    }

    [Test]
    public void MergeErrorsClearsAnAccessErrorWhenTheRescanHasNone()
    {
        var blocked = new ScanError("/root/a/project/locked", "Denied.", "UnauthorizedAccessException");

        var merged = ScanResultMerger.MergeErrors([blocked], "/root/a/project", []);

        Assert.That(merged, Is.Empty);
    }

    [Test]
    public void MergeOmittedEquivalentPathsReplacesOmissionsInsideTheFolder()
    {
        var inside = new EquivalentPath("/Users", "/System/Volumes/Data/Users");
        var outside = new EquivalentPath("/Applications", "/Volumes/Data/Applications");

        var merged = ScanResultMerger.MergeOmittedEquivalentPaths(
            [inside, outside],
            "/System",
            [inside]);

        Assert.That(merged, Is.EqualTo(new[] { outside, inside }));
    }

    [Test]
    public void MergeOmittedEquivalentPathsDropsOmissionsTheRescanNoLongerFound()
    {
        var inside = new EquivalentPath("/Users", "/System/Volumes/Data/Users");

        var merged = ScanResultMerger.MergeOmittedEquivalentPaths([inside], "/System", []);

        Assert.That(merged, Is.Empty);
    }

    [Test]
    public void AFullScanStartsWithoutRefreshedFolders()
    {
        var provenance = ScanResultProvenance.ForFullScan(ScanTime);

        Assert.Multiple(() =>
        {
            Assert.That(provenance.ScanCompletedAt, Is.EqualTo(ScanTime));
            Assert.That(provenance.RefreshedFolders, Is.Empty);
            Assert.That(provenance.HasRefreshedFolders, Is.False);
        });
    }

    [Test]
    public void RefreshedFoldersAccumulateInRescanOrder()
    {
        var first = ScanTime.AddMinutes(5);
        var second = ScanTime.AddMinutes(9);

        var provenance = ScanResultProvenance.ForFullScan(ScanTime)
            .WithRefreshedFolder("/root/a", first)
            .WithRefreshedFolder("/root/b", second);

        Assert.Multiple(() =>
        {
            Assert.That(provenance.ScanCompletedAt, Is.EqualTo(ScanTime));
            Assert.That(
                provenance.RefreshedFolders,
                Is.EqualTo(new[]
                {
                    new RefreshedFolder("/root/a", first),
                    new RefreshedFolder("/root/b", second)
                }));
        });
    }

    [Test]
    public void AnAncestorRescanSupersedesItsDescendants()
    {
        var later = ScanTime.AddMinutes(20);

        var provenance = ScanResultProvenance.ForFullScan(ScanTime)
            .WithRefreshedFolder("/root/a/project/src", ScanTime.AddMinutes(5))
            .WithRefreshedFolder("/root/b", ScanTime.AddMinutes(6))
            .WithRefreshedFolder("/root/a/project", ScanTime.AddMinutes(7))
            .WithRefreshedFolder("/root/a", later);

        Assert.That(
            provenance.RefreshedFolders,
            Is.EqualTo(new[]
            {
                new RefreshedFolder("/root/b", ScanTime.AddMinutes(6)),
                new RefreshedFolder("/root/a", later)
            }));
    }

    [Test]
    public void RescanningTheSameFolderAgainKeepsOnlyTheLatestTime()
    {
        var later = ScanTime.AddMinutes(30);

        var provenance = ScanResultProvenance.ForFullScan(ScanTime)
            .WithRefreshedFolder("/root/a", ScanTime.AddMinutes(1))
            .WithRefreshedFolder("/root/a/", later);

        Assert.That(
            provenance.RefreshedFolders,
            Is.EqualTo(new[] { new RefreshedFolder("/root/a", later) }));
    }

    [Test]
    public void ADescendantRescanDoesNotSupersedeItsAncestor()
    {
        var provenance = ScanResultProvenance.ForFullScan(ScanTime)
            .WithRefreshedFolder("/root/a", ScanTime.AddMinutes(1))
            .WithRefreshedFolder("/root/a/project", ScanTime.AddMinutes(2));

        Assert.That(
            provenance.RefreshedFolders.Select(folder => folder.Path),
            Is.EqualTo(new[] { "/root/a", "/root/a/project" }));
    }

    private static TestTree Tree()
    {
        var root = new DiskItem("root", "/root", isDirectory: true);
        var a = new DiskItem("a", "/root/a", isDirectory: true);
        var b = Folder("/root/b", file: ("b.bin", 30, 36, 0));
        var project = Folder("/root/a/project", file: ("old.bin", 100, 120, 20));
        var keep = FileItem("/root/a/keep.bin", 10, 12, 0);

        a.AddChild(project);
        a.AddChild(keep);
        a.SizeBytes = 110;
        a.MeasuredSizeBytes = 132;
        a.SharedSizeBytes = 20;

        root.AddChild(a);
        root.AddChild(b);
        root.SizeBytes = 140;
        root.MeasuredSizeBytes = 168;
        root.SharedSizeBytes = 20;
        return new TestTree(root, a, b, project);
    }

    private static DiskItem Folder(
        string path,
        (string Name, long Size, long Measured, long Shared) file)
    {
        var folder = new DiskItem(Path.GetFileName(path), path, isDirectory: true);
        folder.AddChild(FileItem($"{path}/{file.Name}", file.Size, file.Measured, file.Shared));
        folder.SizeBytes = file.Size;
        folder.MeasuredSizeBytes = file.Measured;
        folder.SharedSizeBytes = file.Shared;
        return folder;
    }

    private static DiskItem FileItem(string path, long size, long measured, long shared) =>
        new(Path.GetFileName(path), path, isDirectory: false)
        {
            SizeBytes = size,
            MeasuredSizeBytes = measured,
            SharedSizeBytes = shared
        };

    private sealed record TestTree(DiskItem Root, DiskItem A, DiskItem B, DiskItem Project);
}
