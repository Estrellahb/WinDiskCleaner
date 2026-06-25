using WinDiskCleaner.Core.Models;
using WinDiskCleaner.Core.Services;

namespace WinDiskCleaner.Core.Tests;

public class Mvp3DuplicateSafetyTests
{
    [Fact]
    public async Task DuplicateDeleteExecutor_DeletesConfirmedDuplicateOutsideLowRiskFolders()
    {
        var root = CreateTempRoot();
        var recycleRoot = Path.Combine(root, "recycle");
        var keep = Path.Combine(root, "Documents", "keep.txt");
        var delete = Path.Combine(root, "Documents", "delete.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(keep)!);
        await File.WriteAllTextAsync(keep, "same content");
        await File.WriteAllTextAsync(delete, "same content");
        var group = DuplicateGroupFor(keep, delete);
        var executor = new DuplicateDeleteExecutor(recycleRoot, Path.Combine(root, "delete.log"));

        var result = await executor.DeleteAsync(new List<DuplicateGroup> { group }, new[] { delete });

        Assert.Equal(1, result.Succeeded);
        Assert.Equal(0, result.Failed);
        Assert.True(File.Exists(keep));
        Assert.False(File.Exists(delete));
    }

    [Fact]
    public async Task DuplicateDeleteExecutor_RehashesBeforeDeleteAndSkipsChangedFiles()
    {
        var root = CreateTempRoot();
        var recycleRoot = Path.Combine(root, "recycle");
        var keep = Path.Combine(root, "Documents", "keep.txt");
        var delete = Path.Combine(root, "Documents", "delete.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(keep)!);
        await File.WriteAllTextAsync(keep, "same content");
        await File.WriteAllTextAsync(delete, "same content");
        var group = DuplicateGroupFor(keep, delete);
        await File.WriteAllTextAsync(delete, "changed content");
        var executor = new DuplicateDeleteExecutor(recycleRoot, Path.Combine(root, "delete.log"));

        var result = await executor.DeleteAsync(new List<DuplicateGroup> { group }, new[] { delete });

        Assert.Equal(0, result.Succeeded);
        Assert.Equal(1, result.Failed);
        Assert.True(File.Exists(keep));
        Assert.True(File.Exists(delete));
        Assert.Contains(result.Errors, error => error.Contains("文件已变化"));
    }

    [Fact]
    public async Task DuplicateDeleteExecutor_RequiresOneRemainingCopyPerGroup()
    {
        var root = CreateTempRoot();
        var recycleRoot = Path.Combine(root, "recycle");
        var first = Path.Combine(root, "Documents", "first.txt");
        var second = Path.Combine(root, "Documents", "second.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(first)!);
        await File.WriteAllTextAsync(first, "same content");
        await File.WriteAllTextAsync(second, "same content");
        var group = DuplicateGroupFor(first, second);
        var executor = new DuplicateDeleteExecutor(recycleRoot, Path.Combine(root, "delete.log"));

        var result = await executor.DeleteAsync(new List<DuplicateGroup> { group }, new[] { first, second });

        Assert.Equal(0, result.Succeeded);
        Assert.Equal(2, result.Failed);
        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
        Assert.Contains(result.Errors, error => error.Contains("至少保留一份"));
    }

    [Fact]
    public async Task FindDuplicatesAsync_SkipsFilesThatDisappearDuringHashing()
    {
        var root = CreateTempRoot();
        var brokenLink = Path.Combine(root, "broken-link.txt");
        File.CreateSymbolicLink(brokenLink, Path.Combine(root, "missing-target.txt"));
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "same");
        await File.WriteAllTextAsync(Path.Combine(root, "b.txt"), "same");
        var finder = new DuplicateFinder();

        var groups = await finder.FindDuplicatesAsync(new List<string> { root }, progress: null, CancellationToken.None);

        var group = Assert.Single(groups);
        Assert.Equal(2, group.Files.Count);
        Assert.DoesNotContain(group.Files, file => file.Path == brokenLink);
    }

    [Fact]
    public void DuplicateReportExporter_EscapesCsvFormulaPrefixes()
    {
        var exporter = new DuplicateReportExporter();
        var group = new DuplicateGroup
        {
            Hash = new string('a', 64),
            TotalSize = 10,
            WastedSpace = 5,
            Files = new List<DuplicateFileInfo>
            {
                new() { Path = "=HYPERLINK(\"https://example.com\")", Size = 5, LastModified = DateTime.UtcNow }
            }
        };

        var csv = exporter.GenerateCsv(new List<DuplicateGroup> { group });

        Assert.Contains("\"'=HYPERLINK", csv);
    }

    private static DuplicateGroup DuplicateGroupFor(string keepPath, string deletePath)
    {
        var keepInfo = new FileInfo(keepPath);
        var deleteInfo = new FileInfo(deletePath);
        return new DuplicateGroup
        {
            Hash = DuplicateFinder.ComputeSha256(keepPath),
            TotalSize = keepInfo.Length + deleteInfo.Length,
            WastedSpace = deleteInfo.Length,
            Files = new List<DuplicateFileInfo>
            {
                new()
                {
                    Path = keepPath,
                    Size = keepInfo.Length,
                    LastModified = keepInfo.LastWriteTimeUtc,
                    IsRecommended = true,
                    Selected = false
                },
                new()
                {
                    Path = deletePath,
                    Size = deleteInfo.Length,
                    LastModified = deleteInfo.LastWriteTimeUtc,
                    IsRecommended = false,
                    Selected = true
                }
            }
        };
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinDiskCleanerMvp3Safety", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
