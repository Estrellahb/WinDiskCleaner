namespace WinDiskCleaner.Core.Models;

public class RiskRule
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<string> PathPatterns { get; set; } = new();
    public RiskLevel RiskLevel { get; set; } = RiskLevel.Unknown;
    public string Category { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
