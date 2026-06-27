using System.Text.Json.Serialization;

namespace WinDiskCleaner.Core.Models;

public class ScanReport : IDisposable
{
    private bool _disposed;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ScanTime { get; set; } = DateTime.Now;
    public string Drive { get; set; } = string.Empty;
    public long TotalSize { get; set; }
    public long UsedSize { get; set; }
    public long FreeSize { get; set; }
    [JsonIgnore]
    public ScanNode? RootNode { get; set; }
    public List<ScanReportItem> Items { get; set; } = new();
    public List<ScanNode> TopDirectories { get; set; } = new();
    public List<ScanNode> TopFiles { get; set; } = new();
    public List<ScanNode> LowRiskItems { get; set; } = new();
    public List<ScanNode> MediumRiskItems { get; set; } = new();
    public List<ScanNode> HighRiskItems { get; set; } = new();
    public List<ScanNode> ForbiddenItems { get; set; } = new();
    public long EstimatedSafeClean { get; set; }
    public long EstimatedConfirmClean { get; set; }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        RootNode = null;
        Items.Clear();
        TopDirectories.Clear();
        TopFiles.Clear();
        LowRiskItems.Clear();
        MediumRiskItems.Clear();
        HighRiskItems.Clear();
        ForbiddenItems.Clear();
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
