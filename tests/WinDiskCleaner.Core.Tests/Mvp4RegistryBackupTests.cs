using WinDiskCleaner.Core.Interfaces;
using WinDiskCleaner.Core.Services;

namespace WinDiskCleaner.Core.Tests;

public class Mvp4RegistryBackupTests
{
    [Fact]
    public async Task BackupBranchAsync_CreatesTimestampedStandardRegFile()
    {
        var outputDir = CreateTempRoot();
        var exporter = new FixtureBranchExporter("Windows Registry Editor Version 5.00\r\n\r\n[HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall]\r\n");
        var service = new RegistryBackupService(exporter, new FixtureFileImporter(), Path.Combine(outputDir, "registry.log"));

        var filePath = await service.BackupBranchAsync(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall", outputDir);

        Assert.True(File.Exists(filePath));
        Assert.EndsWith(".reg", filePath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HKEY_CURRENT_USER_Software_Microsoft_Windows_CurrentVersion_Uninstall_", Path.GetFileName(filePath));
        Assert.Contains("Windows Registry Editor Version 5.00", await File.ReadAllTextAsync(filePath));
        Assert.Equal(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall", exporter.ExportedBranch);
    }

    [Fact]
    public async Task BackupBranchAsync_RejectsUnsafeSystemBranches()
    {
        var outputDir = CreateTempRoot();
        var service = new RegistryBackupService(new FixtureBranchExporter(), new FixtureFileImporter(), Path.Combine(outputDir, "registry.log"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.BackupBranchAsync(@"HKEY_LOCAL_MACHINE\SYSTEM", outputDir));
    }

    [Fact]
    public async Task GetBackupHistory_ReturnsRegBackupsWithBranchAndSize()
    {
        var outputDir = CreateTempRoot();
        var filePath = Path.Combine(outputDir, "HKEY_CURRENT_USER_Software_Test_20260625_111111.reg");
        await File.WriteAllTextAsync(filePath, "Windows Registry Editor Version 5.00\r\n\r\n[HKEY_CURRENT_USER\\Software\\Test]\r\n");
        var service = new RegistryBackupService(new FixtureBranchExporter(), new FixtureFileImporter(), Path.Combine(outputDir, "registry.log"));

        var backup = Assert.Single(service.GetBackupHistory(outputDir));

        Assert.Equal(filePath, backup.FilePath);
        Assert.Equal(@"HKEY_CURRENT_USER\Software\Test", backup.BranchName);
        Assert.True(backup.FileSize > 0);
        Assert.True(backup.BackupTime > DateTime.MinValue);
    }

    [Fact]
    public async Task RestoreFromFileAsync_ImportsValidRegFileAndLogsOperation()
    {
        var outputDir = CreateTempRoot();
        var regFile = Path.Combine(outputDir, "valid.reg");
        await File.WriteAllTextAsync(regFile, "Windows Registry Editor Version 5.00\r\n\r\n[HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Demo]\r\n\"DisplayName\"=\"Demo\"\r\n");
        var importer = new FixtureFileImporter { Result = true };
        var logPath = Path.Combine(outputDir, "registry.log");
        var service = new RegistryBackupService(new FixtureBranchExporter(), importer, logPath);

        var result = await service.RestoreFromFileAsync(regFile);

        Assert.True(result);
        Assert.Equal(regFile, importer.ImportedPath);
        Assert.Contains("Restore", await File.ReadAllTextAsync(logPath));
    }

    [Fact]
    public async Task RestoreFromFileAsync_InvalidRegFile_ReturnsFalseWithoutImporting()
    {
        var outputDir = CreateTempRoot();
        var regFile = Path.Combine(outputDir, "invalid.reg");
        await File.WriteAllTextAsync(regFile, "not a reg file");
        var importer = new FixtureFileImporter();
        var service = new RegistryBackupService(new FixtureBranchExporter(), importer, Path.Combine(outputDir, "registry.log"));

        var result = await service.RestoreFromFileAsync(regFile);

        Assert.False(result);
        Assert.Null(importer.ImportedPath);
    }

    [Fact]
    public async Task RestoreFromFileAsync_UnsafeSystemBranch_ReturnsFalseWithoutImporting()
    {
        var outputDir = CreateTempRoot();
        var regFile = Path.Combine(outputDir, "unsafe.reg");
        await File.WriteAllTextAsync(regFile, "Windows Registry Editor Version 5.00\r\n\r\n[HKEY_LOCAL_MACHINE\\SAM]\r\n");
        var importer = new FixtureFileImporter();
        var service = new RegistryBackupService(new FixtureBranchExporter(), importer, Path.Combine(outputDir, "registry.log"));

        var result = await service.RestoreFromFileAsync(regFile);

        Assert.False(result);
        Assert.Null(importer.ImportedPath);
    }

    [Fact]
    public async Task PreviewRestoreFile_ReturnsContainedBranchesBeforeImport()
    {
        var outputDir = CreateTempRoot();
        var regFile = Path.Combine(outputDir, "preview.reg");
        await File.WriteAllTextAsync(regFile, "Windows Registry Editor Version 5.00\r\n\r\n[HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Demo]\r\n");
        var service = new RegistryBackupService(new FixtureBranchExporter(), new FixtureFileImporter(), Path.Combine(outputDir, "registry.log"), outputDir);

        var preview = await service.PreviewRestoreFileAsync(regFile);

        Assert.True(preview.IsValid);
        Assert.Contains(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Uninstall\Demo", preview.Branches);
    }

    [Fact]
    public async Task RestoreFromFileAsync_DestructiveDeletionSection_ReturnsFalseWithoutImporting()
    {
        var outputDir = CreateTempRoot();
        var regFile = Path.Combine(outputDir, "delete-section.reg");
        await File.WriteAllTextAsync(regFile, "Windows Registry Editor Version 5.00\r\n\r\n[-HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Demo]\r\n");
        var importer = new FixtureFileImporter();
        var service = new RegistryBackupService(new FixtureBranchExporter(), importer, Path.Combine(outputDir, "registry.log"), outputDir);

        var result = await service.RestoreFromFileAsync(regFile);

        Assert.False(result);
        Assert.Null(importer.ImportedPath);
    }

    [Fact]
    public async Task RestoreFromFileAsync_NonUninstallBranch_ReturnsFalseWithoutImporting()
    {
        var outputDir = CreateTempRoot();
        var regFile = Path.Combine(outputDir, "non-uninstall.reg");
        await File.WriteAllTextAsync(regFile, "Windows Registry Editor Version 5.00\r\n\r\n[HKEY_CURRENT_USER\\Software\\Demo]\r\n");
        var importer = new FixtureFileImporter();
        var service = new RegistryBackupService(new FixtureBranchExporter(), importer, Path.Combine(outputDir, "registry.log"), outputDir);

        var result = await service.RestoreFromFileAsync(regFile);

        Assert.False(result);
        Assert.Null(importer.ImportedPath);
    }

    [Fact]
    public async Task RestoreFromFileAsync_HeaderMustBeFirstMeaningfulLine()
    {
        var outputDir = CreateTempRoot();
        var regFile = Path.Combine(outputDir, "bad-header.reg");
        await File.WriteAllTextAsync(regFile, "; fake before header\r\nWindows Registry Editor Version 5.00\r\n\r\n[HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Demo]\r\n");
        var importer = new FixtureFileImporter();
        var service = new RegistryBackupService(new FixtureBranchExporter(), importer, Path.Combine(outputDir, "registry.log"), outputDir);

        var result = await service.RestoreFromFileAsync(regFile);

        Assert.False(result);
        Assert.Null(importer.ImportedPath);
    }

    [Fact]
    public async Task DeleteBackup_OnlyDeletesRegFilesUnderBackupRoot()
    {
        var outputDir = CreateTempRoot();
        var outsideDir = CreateTempRoot();
        var insideBackup = Path.Combine(outputDir, "inside.reg");
        var outsideBackup = Path.Combine(outsideDir, "outside.reg");
        await File.WriteAllTextAsync(insideBackup, "Windows Registry Editor Version 5.00\r\n");
        await File.WriteAllTextAsync(outsideBackup, "Windows Registry Editor Version 5.00\r\n");
        var service = new RegistryBackupService(new FixtureBranchExporter(), new FixtureFileImporter(), Path.Combine(outputDir, "registry.log"), outputDir);

        Assert.False(service.DeleteBackup(outsideBackup));
        Assert.True(File.Exists(outsideBackup));
        Assert.True(service.DeleteBackup(insideBackup));
        Assert.False(File.Exists(insideBackup));
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinDiskCleanerMvp4Backup", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class FixtureBranchExporter : IRegistryBranchExporter
    {
        private readonly string _content;
        public string? ExportedBranch { get; private set; }

        public FixtureBranchExporter(string? content = null)
        {
            _content = content ?? "Windows Registry Editor Version 5.00\r\n\r\n[HKEY_CURRENT_USER\\Software\\Test]\r\n";
        }

        public async Task ExportBranchAsync(string registryBranch, string outputFilePath, CancellationToken ct = default)
        {
            ExportedBranch = registryBranch;
            await File.WriteAllTextAsync(outputFilePath, _content, ct);
        }
    }

    private sealed class FixtureFileImporter : IRegistryFileImporter
    {
        public string? ImportedPath { get; private set; }
        public bool Result { get; set; } = true;

        public Task<bool> ImportAsync(string regFilePath, CancellationToken ct = default)
        {
            ImportedPath = regFilePath;
            return Task.FromResult(Result);
        }
    }
}
