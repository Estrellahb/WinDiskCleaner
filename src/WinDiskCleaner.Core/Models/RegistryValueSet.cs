namespace WinDiskCleaner.Core.Models;

public class RegistryValueSet
{
    public string BranchPath { get; set; } = string.Empty;
    public string KeyName { get; set; } = string.Empty;
    public Dictionary<string, object?> Values { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
