using WinDiskCleaner.Core.Interfaces;
using WinDiskCleaner.Core.Models;
using WinDiskCleaner.Core.Services;

namespace WinDiskCleaner.Core.Tests;

public class Mvp3DuplicateFinderTests
{
    [Fact]
    public async Task FindDuplicatesAsync_EmptyDirectory_ReturnsEmptyList()
    {
        var root = CreateTempRoot();
        var finder = new DuplicateFinder();

        var groups = await finder.FindDuplicatesAsync(new List<string> { root }, progress: null, CancellationToken.None);

        Assert.Empty(groups);
    }

    [Fact]
    public async Task FindDuplicatesAsync_NoDuplicateFiles_ReturnsEmptyList()
    {
        var root = CreateTempRoot();
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "alpha");
        await File.WriteAllTextAsync(Path.Combine(root, "b.txt"), "bravo");
        var finder = new DuplicateFinder();

        var groups = await finder.FindDuplicatesAsync(new List<string> { root }, progress: null, CancellationToken.None);

        Assert.Empty(groups);
    }

    [Fact]
    public async Task FindDuplicatesAsync_TwoIdenticalFiles_AreDetectedAsOneGroup()
    {
        var root = CreateTempRoot();
        var content = "same duplicate content";
        var older = Path.Combine(root, "older.txt");
        var newer = Path.Combine(root, "newer.txt");
        await File.WriteAllTextAsync(older, content);
        await File.WriteAllTextAsync(newer, content);
        File.SetLastWriteTimeUtc(older, DateTime.UtcNow.AddDays(-2));
        File.SetLastWriteTimeUtc(newer, DateTime.UtcNow.AddDays(-1));
        var finder = new DuplicateFinder();

        var groups = await finder.FindDuplicatesAsync(new List<string> { root }, progress: null, CancellationToken.None);

        var group = Assert.Single(groups);
        Assert.Equal(2, group.Files.Count);
        Assert.Equal(content.Length, group.WastedSpace);
        Assert.Contains(group.Files, file => file.Path == newer && file.IsRecommended && !file.Selected);
        Assert.Contains(group.Files, file => file.Path == older && !file.IsRecommended && file.Selected);
    }

    [Fact]
    public async Task FindDuplicatesAsync_SameSizeDifferentContent_NotDuplicate()
    {
        var root = CreateTempRoot();
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "abc123");
        await File.WriteAllTextAsync(Path.Combine(root, "b.txt"), "xyz789");
        var finder = new DuplicateFinder();

        var groups = await finder.FindDuplicatesAsync(new List<string> { root }, progress: null, CancellationToken.None);

        Assert.Empty(groups);
    }

    [Fact]
    public async Task FindDuplicatesAsync_LargeFiles_UsePartialAndFullHashChain()
    {
        var root = CreateTempRoot();
        var first = Path.Combine(root, "large-a.bin");
        var second = Path.Combine(root, "large-b.bin");
        var differentTail = Path.Combine(root, "large-c.bin");
        var sharedPrefix = Enumerable.Repeat((byte)42, 1024 * 1024).ToArray();
        var tail = Enumerable.Repeat((byte)7, 512).ToArray();
        await File.WriteAllBytesAsync(first, sharedPrefix.Concat(tail).ToArray());
        await File.WriteAllBytesAsync(second, sharedPrefix.Concat(tail).ToArray());
        await File.WriteAllBytesAsync(differentTail, sharedPrefix.Concat(Enumerable.Repeat((byte)8, 512)).ToArray());
        var finder = new DuplicateFinder();

        var groups = await finder.FindDuplicatesAsync(new List<string> { root }, progress: null, CancellationToken.None);

        var group = Assert.Single(groups);
        Assert.Equal(2, group.Files.Count);
        Assert.DoesNotContain(group.Files, file => file.Path == differentTail);
        Assert.Matches("^[a-f0-9]{64}$", group.Hash);
    }

    [Fact]
    public async Task FindDuplicatesAsync_CanBeCancelled()
    {
        var root = CreateTempRoot();
        await File.WriteAllTextAsync(Path.Combine(root, "a.txt"), "same");
        await File.WriteAllTextAsync(Path.Combine(root, "b.txt"), "same");
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var finder = new DuplicateFinder();

        await Assert.ThrowsAsync<OperationCanceledException>(() => finder.FindDuplicatesAsync(new List<string> { root }, progress: null, cts.Token));
    }

    [Fact]
    public async Task FindDuplicatesAsync_SkipsUnsafeAndVirtualDiskFiles()
    {
        var root = CreateTempRoot();
        var safeDuplicate = Path.Combine(root, "safe-a.txt");
        var virtualDiskA = Path.Combine(root, "disk-a.vhdx");
        var virtualDiskB = Path.Combine(root, "disk-b.vhdx");
        await File.WriteAllTextAsync(safeDuplicate, "same");
        await File.WriteAllTextAsync(Path.Combine(root, "safe-b.txt"), "same");
        await File.WriteAllTextAsync(virtualDiskA, "virtual disk");
        await File.WriteAllTextAsync(virtualDiskB, "virtual disk");
        var finder = new DuplicateFinder();

        var groups = await finder.FindDuplicatesAsync(new List<string> { root }, progress: null, CancellationToken.None);

        var group = Assert.Single(groups);
        Assert.DoesNotContain(group.Files, file => file.Path.EndsWith(".vhdx", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DuplicateReportExporter_CreatesCsvAndHtmlReports()
    {
        var group = new DuplicateGroup
        {
            Hash = new string('a', 64),
            TotalSize = 20,
            WastedSpace = 10,
            Files = new List<DuplicateFileInfo>
            {
                new() { Path = "C:/Users/Test/a.txt", Size = 10, LastModified = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), IsRecommended = true },
                new() { Path = "C:/Users/Test/b.txt", Size = 10, LastModified = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc), Selected = true }
            }
        };
        var exporter = new DuplicateReportExporter();

        var csv = exporter.GenerateCsv(new List<DuplicateGroup> { group });
        var html = exporter.GenerateHtml(new List<DuplicateGroup> { group });

        Assert.Contains("Hash,Path,Size,LastModified,IsRecommended,Selected", csv);
        Assert.Contains("C:/Users/Test/a.txt", csv);
        Assert.Contains("<html", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("C:/Users/Test/b.txt", html);
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinDiskCleanerMvp3", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
