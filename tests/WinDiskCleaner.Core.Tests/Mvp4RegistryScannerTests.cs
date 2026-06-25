using WinDiskCleaner.Core.Interfaces;
using WinDiskCleaner.Core.Models;
using WinDiskCleaner.Core.Services;

namespace WinDiskCleaner.Core.Tests;

public class Mvp4RegistryScannerTests
{
    [Fact]
    public async Task ScanInstalledSoftwareAsync_ParsesStandardUninstallEntries()
    {
        var existingInstall = CreateTempRoot();
        var scanner = new RegistryScanner(new FixtureRegistryReader(new List<RegistryValueSet>
        {
            Entry(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Uninstall", "AppA", new Dictionary<string, object?>
            {
                ["DisplayName"] = "Alpha App",
                ["DisplayVersion"] = "1.2.3",
                ["Publisher"] = "Alpha Inc",
                ["InstallLocation"] = existingInstall,
                ["EstimatedSize"] = 2048,
                ["UninstallString"] = "uninstall-alpha.exe"
            }),
            Entry(@"HKEY_LOCAL_MACHINE\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall", "AppB", new Dictionary<string, object?>
            {
                ["DisplayName"] = "Bravo App",
                ["EstimatedSize"] = "1024"
            }),
            Entry(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall", "AppC", new Dictionary<string, object?>
            {
                ["DisplayName"] = "Charlie App"
            })
        }));

        var software = await scanner.ScanInstalledSoftwareAsync();

        Assert.Equal(3, software.Count);
        var alpha = Assert.Single(software, item => item.DisplayName == "Alpha App");
        Assert.Equal("1.2.3", alpha.DisplayVersion);
        Assert.Equal("Alpha Inc", alpha.Publisher);
        Assert.Equal(existingInstall, alpha.InstallLocation);
        Assert.Equal(2048L * 1024L, alpha.EstimatedSize);
        Assert.Equal("uninstall-alpha.exe", alpha.UninstallString);
        Assert.Equal(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Uninstall\AppA", alpha.RegistryPath);
        Assert.True(alpha.IsValid);
        Assert.False(alpha.IsOrphan);
    }

    [Fact]
    public async Task ScanInstalledSoftwareAsync_MarksMissingInstallLocationAsOrphan()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), "WinDiskCleanerMissing", Guid.NewGuid().ToString("N"));
        var scanner = new RegistryScanner(new FixtureRegistryReader(new List<RegistryValueSet>
        {
            Entry(@"HKEY_LOCAL_MACHINE\Software\Microsoft\Windows\CurrentVersion\Uninstall", "Missing", new Dictionary<string, object?>
            {
                ["DisplayName"] = "Missing App",
                ["InstallLocation"] = missingPath
            })
        }));

        var item = Assert.Single(await scanner.ScanInstalledSoftwareAsync());

        Assert.True(item.IsOrphan);
        Assert.True(item.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\u0001\u0002")]
    public async Task ScanInstalledSoftwareAsync_MarksEmptyOrAbnormalDisplayNameAsInvalid(string displayName)
    {
        var scanner = new RegistryScanner(new FixtureRegistryReader(new List<RegistryValueSet>
        {
            Entry(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall", "Invalid", new Dictionary<string, object?>
            {
                ["DisplayName"] = displayName
            })
        }));

        var item = Assert.Single(await scanner.ScanInstalledSoftwareAsync());

        Assert.False(item.IsValid);
        Assert.Equal(displayName.Trim(), item.DisplayName);
    }

    private static RegistryValueSet Entry(string branchPath, string keyName, Dictionary<string, object?> values)
    {
        return new RegistryValueSet
        {
            BranchPath = branchPath,
            KeyName = keyName,
            Values = values
        };
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinDiskCleanerMvp4", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class FixtureRegistryReader : IRegistryReader
    {
        private readonly List<RegistryValueSet> _entries;

        public FixtureRegistryReader(List<RegistryValueSet> entries)
        {
            _entries = entries;
        }

        public Task<List<RegistryValueSet>> ReadUninstallEntriesAsync(CancellationToken ct = default)
        {
            return Task.FromResult(_entries);
        }
    }
}
