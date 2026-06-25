namespace WinDiskCleaner.Core.Models;

public class RegistryRestorePreview
{
    public string FilePath { get; set; } = string.Empty;
    public List<string> Branches { get; set; } = new();
    public bool IsValid { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}
