namespace WinDiskCleaner.Core.Models;

public class RegistryBackup
{
    public string FilePath { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public DateTime BackupTime { get; set; }
    public long FileSize { get; set; }
}
