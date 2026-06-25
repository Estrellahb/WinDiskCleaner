using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Interfaces;

public interface IRegistryBackupService
{
    Task<string> BackupBranchAsync(string registryBranch, string outputDir);
    Task<bool> RestoreFromFileAsync(string regFilePath);
    List<RegistryBackup> GetBackupHistory(string backupDir);
}

public interface IRegistryBranchExporter
{
    Task ExportBranchAsync(string registryBranch, string outputFilePath, CancellationToken ct = default);
}

public interface IRegistryFileImporter
{
    Task<bool> ImportAsync(string regFilePath, CancellationToken ct = default);
}
