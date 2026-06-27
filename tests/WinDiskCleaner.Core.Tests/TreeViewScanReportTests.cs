using WinDiskCleaner.Core.Models;
using WinDiskCleaner.Core.Services;

namespace WinDiskCleaner.Core.Tests;

public class TreeViewScanReportTests
{
    [Fact]
    public async Task DiskScanner_ScanDriveAsync_PreservesFullRootNodeWithGrandchildren()
    {
        var root = Path.Combine(Environment.CurrentDirectory, "WinDiskCleanerTreeTests", Guid.NewGuid().ToString("N"));
        var parent = Path.Combine(root, "Parent");
        var child = Path.Combine(parent, "Child");
        var grandChild = Path.Combine(child, "GrandChild");
        Directory.CreateDirectory(grandChild);
        await File.WriteAllTextAsync(Path.Combine(grandChild, "deep.tmp"), new string('d', 12));

        var scanner = new DiskScanner();

        var report = await scanner.ScanDriveAsync(root);

        Assert.NotNull(report.RootNode);
        Assert.Equal(root, report.RootNode.Path);
        var parentNode = Assert.Single(report.RootNode.Children.Where(node => node.Name == "Parent"));
        var childNode = Assert.Single(parentNode.Children.Where(node => node.Name == "Child"));
        var grandChildNode = Assert.Single(childNode.Children.Where(node => node.Name == "GrandChild"));
        Assert.Contains(grandChildNode.Children, node => node.Name == "deep.tmp" && !node.IsDirectory);
    }

    [Fact]
    public async Task DiskScanner_ScanDriveAsync_UsesCustomSkippedDirectoryNames()
    {
        var root = Path.Combine(Environment.CurrentDirectory, "WinDiskCleanerSkipOptionTests", Guid.NewGuid().ToString("N"));
        var defaultSkipped = Path.Combine(root, "Windows");
        var customSkipped = Path.Combine(root, "IgnoreMe");
        Directory.CreateDirectory(defaultSkipped);
        Directory.CreateDirectory(customSkipped);
        await File.WriteAllTextAsync(Path.Combine(defaultSkipped, "included.tmp"), "included");
        await File.WriteAllTextAsync(Path.Combine(customSkipped, "excluded.tmp"), "excluded");
        var scanner = new DiskScanner();
        var options = new ScanOptions
        {
            SkippedDirectoryNames = new List<string> { "IgnoreMe" }
        };

        var report = await scanner.ScanDriveAsync(root, options);

        Assert.NotNull(report.RootNode);
        var windowsNode = Assert.Single(report.RootNode.Children.Where(node => node.Name == "Windows"));
        Assert.Contains(windowsNode.Children, node => node.Name == "included.tmp");
        var ignoredNode = Assert.Single(report.RootNode.Children.Where(node => node.Name == "IgnoreMe"));
        Assert.Empty(ignoredNode.Children);
        Assert.Equal(0, ignoredNode.Size);
    }

    [Fact]
    public void TreeViewNode_FromScanNode_DefaultExpansionIsBoundedAndChildrenAreLazy()
    {
        var root = DirectoryNode("root", 100,
            DirectoryNode("parent", 80,
                DirectoryNode("child", 60,
                    DirectoryNode("grandchild", 40,
                        FileNode("deep.tmp", 40)))));

        var treeNode = ScanTreeViewNode.FromScanNode(root, totalSize: root.Size, defaultExpandDepth: 2);

        Assert.True(treeNode.IsExpanded);
        var parentNode = Assert.Single(treeNode.Children);
        Assert.True(parentNode.IsExpanded);
        var childNode = Assert.Single(parentNode.Children);
        Assert.False(childNode.IsExpanded);
        Assert.True(childNode.HasUnrealizedChildren);
        var placeholder = Assert.Single(childNode.Children);
        Assert.True(placeholder.IsPlaceholder);

        childNode.EnsureChildrenLoaded();

        var grandchildNode = Assert.Single(childNode.Children);
        Assert.Equal("grandchild", grandchildNode.Name);
        Assert.False(grandchildNode.IsExpanded);
    }

    [Fact]
    public void TreeViewNode_FromScanNode_DoesNotCreateOtherBucketForNonTopDirectories()
    {
        var children = Enumerable.Range(1, 12)
            .Select(index => DirectoryNode($"Dir{index:00}", 100 - index, FileNode($"file{index}.tmp", 1)))
            .ToArray();
        var root = DirectoryNode("root", children.Sum(child => child.Size), children);

        var treeNode = ScanTreeViewNode.FromScanNode(root, totalSize: root.Size, defaultExpandDepth: 1);
        treeNode.EnsureChildrenLoaded();

        Assert.Equal(12, treeNode.Children.Count);
        Assert.DoesNotContain(treeNode.Children, node => node.Name == "其他" || node.Path == "其他小目录合计");
    }

    private static ScanNode DirectoryNode(string name, long size, params ScanNode[] children)
    {
        return new ScanNode
        {
            Name = name,
            Path = name,
            Size = size,
            FileCount = children.Sum(child => child.IsDirectory ? child.FileCount : 1),
            IsDirectory = true,
            RiskLevel = RiskLevel.Medium,
            Children = children.ToList()
        };
    }

    private static ScanNode FileNode(string name, long size)
    {
        return new ScanNode
        {
            Name = name,
            Path = name,
            Size = size,
            FileCount = 1,
            IsDirectory = false,
            RiskLevel = RiskLevel.Low
        };
    }
}
