namespace WinDiskCleaner.Core.Models;

public class InstalledSoftware
{
    public string DisplayName { get; set; } = string.Empty;
    public string DisplayVersion { get; set; } = string.Empty;
    public string Publisher { get; set; } = string.Empty;
    public string InstallLocation { get; set; } = string.Empty;
    public long EstimatedSize { get; set; }
    public string UninstallString { get; set; } = string.Empty;
    public string RegistryPath { get; set; } = string.Empty;
    public bool IsValid { get; set; } = true;
    public bool IsOrphan { get; set; }
}
