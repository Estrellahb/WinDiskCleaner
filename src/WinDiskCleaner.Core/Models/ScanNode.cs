namespace WinDiskCleaner.Core.Models;

public class ScanNode
{
    public string Path { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long Size { get; set; }
    public bool IsDirectory { get; set; }
    public DateTime LastModified { get; set; }
    public long FileCount { get; set; }
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Unknown;
    public string? ErrorMessage { get; set; }
    public List<ScanNode> Children { get; set; } = new();
}
