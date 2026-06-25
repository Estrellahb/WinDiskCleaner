using WinDiskCleaner.Core.Interfaces;
using WinDiskCleaner.Core.Models;
using WinDiskCleaner.Core.Services;

namespace WinDiskCleaner.Core.Tests;

public class Mvp5ShortcutTests
{
    [Fact]
    public async Task Mvp5_ScanShortcutsAsync_ReturnsEmptyListForEmptyDirectories()
    {
        using var fixture = new ShortcutFixture();
        var scanner = new ShortcutScanner(new[] { new ShortcutScanRoot(fixture.Root, "测试目录") }, new FixtureShortcutParser());

        var items = await scanner.ScanShortcutsAsync(null, CancellationToken.None);

        Assert.Empty(items);
    }

    [Fact]
    public async Task Mvp5_ScanShortcutsAsync_ParsesShortcutTargetAndArguments()
    {
        using var fixture = new ShortcutFixture();
        var target = fixture.Touch("apps/app.exe");
        var shortcut = fixture.WriteShortcut("desktop/App.lnk", target, "--safe-mode");
        var scanner = new ShortcutScanner(new[] { new ShortcutScanRoot(fixture.Root, "桌面") }, new FixtureShortcutParser());

        var item = Assert.Single(await scanner.ScanShortcutsAsync(null, CancellationToken.None));

        Assert.Equal("App.lnk", item.Name);
        Assert.Equal(shortcut, item.ShortcutPath);
        Assert.Equal(target, item.TargetPath);
        Assert.Equal("--safe-mode", item.Arguments);
        Assert.Equal("桌面", item.SourceDirectory);
        Assert.True(item.TargetExists);
        Assert.True(item.FileSize > 0);
        Assert.NotEqual(default, item.LastModified);
    }

    [Fact]
    public async Task Mvp5_ScanShortcutsAsync_MarksExistingTargetsValidAndMissingTargetsInvalid()
    {
        using var fixture = new ShortcutFixture();
        var target = fixture.Touch("apps/existing.exe");
        fixture.WriteShortcut("Existing.lnk", target, string.Empty);
        fixture.WriteShortcut("Missing.lnk", Path.Combine(fixture.Root, "apps", "missing.exe"), string.Empty);
        var scanner = new ShortcutScanner(new[] { new ShortcutScanRoot(fixture.Root, "开始菜单") }, new FixtureShortcutParser());

        var items = await scanner.ScanShortcutsAsync(null, CancellationToken.None);

        Assert.True(items.Single(x => x.Name == "Existing.lnk").TargetExists);
        Assert.False(items.Single(x => x.Name == "Missing.lnk").TargetExists);
    }

    [Fact]
    public async Task Mvp5_ScanShortcutsAsync_IgnoresNonLnkFiles()
    {
        using var fixture = new ShortcutFixture();
        fixture.Touch("note.txt");
        fixture.Touch("script.url");
        var target = fixture.Touch("apps/app.exe");
        fixture.WriteShortcut("OnlyShortcut.lnk", target, string.Empty);
        var scanner = new ShortcutScanner(new[] { new ShortcutScanRoot(fixture.Root, "公共桌面") }, new FixtureShortcutParser());

        var item = Assert.Single(await scanner.ScanShortcutsAsync(null, CancellationToken.None));

        Assert.Equal("OnlyShortcut.lnk", item.Name);
    }

    [Fact]
    public async Task Mvp5_ScanShortcutsAsync_SkipsDirectoriesThatCannotBeEnumerated()
    {
        using var fixture = new ShortcutFixture();
        var target = fixture.Touch("apps/app.exe");
        fixture.WriteShortcut("visible/Visible.lnk", target, string.Empty);
        var scanner = new ShortcutScanner(
            new[] { new ShortcutScanRoot(fixture.Root, "桌面") },
            new FixtureShortcutParser(),
            path => path.EndsWith("blocked", StringComparison.OrdinalIgnoreCase)
                ? throw new UnauthorizedAccessException("blocked")
                : Directory.EnumerateFileSystemEntries(path));
        Directory.CreateDirectory(Path.Combine(fixture.Root, "blocked"));

        var item = Assert.Single(await scanner.ScanShortcutsAsync(null, CancellationToken.None));

        Assert.Equal("Visible.lnk", item.Name);
    }

    [Fact]
    public async Task Mvp5_ScanShortcutsAsync_ContinuesWhenSingleShortcutCannotBeParsed()
    {
        using var fixture = new ShortcutFixture();
        var target = fixture.Touch("apps/app.exe");
        fixture.WriteShortcut("Bad.lnk", target, string.Empty);
        fixture.WriteShortcut("Good.lnk", target, string.Empty);
        var scanner = new ShortcutScanner(
            new[] { new ShortcutScanRoot(fixture.Root, "桌面") },
            new ThrowingShortcutParser("Bad.lnk"));

        var items = await scanner.ScanShortcutsAsync(null, CancellationToken.None);

        Assert.Equal(2, items.Count);
        Assert.False(items.Single(item => item.Name == "Bad.lnk").TargetExists);
        Assert.True(items.Single(item => item.Name == "Good.lnk").TargetExists);
    }

    [Fact]
    public async Task Mvp5_ScanShortcutsAsync_CanBeCancelled()
    {
        using var fixture = new ShortcutFixture();
        var target = fixture.Touch("apps/app.exe");
        fixture.WriteShortcut("Cancel.lnk", target, string.Empty);
        using var cts = new CancellationTokenSource();
        var scanner = new ShortcutScanner(
            new[] { new ShortcutScanRoot(fixture.Root, "启动项") },
            new FixtureShortcutParser(),
            path =>
            {
                cts.Cancel();
                return Directory.EnumerateFileSystemEntries(path);
            });

        await Assert.ThrowsAsync<OperationCanceledException>(() => scanner.ScanShortcutsAsync(null, cts.Token));
    }

    [Fact]
    public async Task Mvp5_DeleteSelectedInvalidShortcutsAsync_DeletesOnlySelectedInvalidLnkAndNeverTarget()
    {
        using var fixture = new ShortcutFixture();
        var target = fixture.Touch("apps/keep.exe");
        var invalidShortcut = fixture.WriteShortcut("invalid.lnk", Path.Combine(fixture.Root, "missing.exe"), string.Empty);
        var validShortcut = fixture.WriteShortcut("valid.lnk", target, string.Empty);
        var nonLnk = fixture.Touch("fake.txt");
        var service = new ShortcutDeleteService(new[] { fixture.Root });
        var items = new List<ShortcutItem>
        {
            new() { Name = "invalid", ShortcutPath = invalidShortcut, TargetPath = Path.Combine(fixture.Root, "missing.exe"), TargetExists = false, Selected = true },
            new() { Name = "valid", ShortcutPath = validShortcut, TargetPath = target, TargetExists = true, Selected = true },
            new() { Name = "fake", ShortcutPath = nonLnk, TargetPath = Path.Combine(fixture.Root, "missing2.exe"), TargetExists = false, Selected = true }
        };

        var result = await service.DeleteSelectedInvalidShortcutsAsync(items, CancellationToken.None);

        Assert.Single(result.DeletedPaths, invalidShortcut);
        Assert.False(File.Exists(invalidShortcut));
        Assert.True(File.Exists(validShortcut));
        Assert.True(File.Exists(target));
        Assert.True(File.Exists(nonLnk));
        Assert.Contains(result.Failures, failure => failure.Path == nonLnk && failure.Reason.Contains(".lnk", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.DeletedPaths, path => path == target);
    }

    [Fact]
    public async Task Mvp5_DeleteSelectedInvalidShortcutsAsync_RejectsShortcutOutsideAllowedRoots()
    {
        using var allowed = new ShortcutFixture();
        using var outside = new ShortcutFixture();
        var outsideShortcut = outside.WriteShortcut("outside.lnk", Path.Combine(outside.Root, "missing.exe"), string.Empty);
        var service = new ShortcutDeleteService(new[] { allowed.Root });

        var result = await service.DeleteSelectedInvalidShortcutsAsync(new[]
        {
            new ShortcutItem { Name = "outside", ShortcutPath = outsideShortcut, TargetPath = Path.Combine(outside.Root, "missing.exe"), TargetExists = false, Selected = true }
        }, CancellationToken.None);

        Assert.Empty(result.DeletedPaths);
        Assert.True(File.Exists(outsideShortcut));
        Assert.Contains(result.Failures, failure => failure.Path == outsideShortcut && failure.Reason.Contains("allowed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Mvp5_ShortcutReportExporter_ExportsCsvAndHtmlWithEscapedContent()
    {
        var exporter = new ShortcutReportExporter();
        var items = new List<ShortcutItem>
        {
            new()
            {
                Name = "=Bad,Name",
                ShortcutPath = @"C:\Users\Public\Desktop\Bad.lnk",
                TargetPath = @"C:\Missing\<bad>.exe",
                Arguments = "--flag",
                TargetExists = false,
                SourceDirectory = "公共桌面",
                FileSize = 12,
                LastModified = new DateTime(2026, 6, 25, 10, 0, 0)
            }
        };

        var csv = exporter.ExportCsv(items);
        var html = exporter.ExportHtml(items);

        Assert.Contains("Name,ShortcutPath,TargetPath,Arguments,TargetExists,SourceDirectory,FileSize,LastModified", csv);
        Assert.Contains("'=Bad,Name", csv);
        Assert.Contains("'\t=cmd", exporter.ExportCsv(new[] { new ShortcutItem { Name = "\t=cmd", ShortcutPath = @"C:\\x.lnk" } }));
        Assert.Contains("公共桌面", csv);
        Assert.Contains("&lt;bad&gt;", html);
        Assert.Contains("失效", html);
    }

    private sealed class FixtureShortcutParser : IShortcutParser
    {
        public ShortcutTarget Parse(string shortcutPath)
        {
            var lines = File.ReadAllLines(shortcutPath);
            return new ShortcutTarget(lines[0], lines.Length > 1 ? lines[1] : string.Empty);
        }
    }

    private sealed class ThrowingShortcutParser : IShortcutParser
    {
        private readonly string _fileNameToThrow;
        private readonly FixtureShortcutParser _inner = new();

        public ThrowingShortcutParser(string fileNameToThrow)
        {
            _fileNameToThrow = fileNameToThrow;
        }

        public ShortcutTarget Parse(string shortcutPath)
        {
            if (Path.GetFileName(shortcutPath).Equals(_fileNameToThrow, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("corrupt shortcut");
            }

            return _inner.Parse(shortcutPath);
        }
    }

    private sealed class ShortcutFixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "WinDiskCleanerMvp5", Guid.NewGuid().ToString("N"));

        public ShortcutFixture()
        {
            Directory.CreateDirectory(Root);
        }

        public string Touch(string relativePath)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "fixture");
            return path;
        }

        public string WriteShortcut(string relativePath, string targetPath, string arguments)
        {
            var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllLines(path, new[] { targetPath, arguments });
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
