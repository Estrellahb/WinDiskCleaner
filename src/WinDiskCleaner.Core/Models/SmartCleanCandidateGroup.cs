namespace WinDiskCleaner.Core.Models;

public class SmartCleanCandidateGroup
{
    public string GroupId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Category { get; set; } = "unknown";
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Unknown;
    public string ActionHint { get; set; } = "manual";
    public long TotalBytes { get; set; }
    public int ItemCount { get; set; }
    public string PathPattern { get; set; } = string.Empty;
    public List<string> SamplePaths { get; set; } = new();
    public List<string> LocalPaths { get; set; } = new();
    public List<ScanNode> Nodes { get; set; } = new();
}
