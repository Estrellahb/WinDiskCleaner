namespace WinDiskCleaner.Core.Models;

public class ScanReportItem
{
    public string Path { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsInUse { get; set; }
}
