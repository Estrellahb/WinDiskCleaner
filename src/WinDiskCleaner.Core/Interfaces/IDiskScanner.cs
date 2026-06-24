using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Interfaces;

public interface IDiskScanner
{
    Task<ScanReport> ScanDriveAsync(string drivePath, CancellationToken ct = default);
    Task<ScanReport> ScanDriveAsync(string drivePath, IProgress<ScanProgress>? progress, CancellationToken ct = default);
}

public class ScanProgress
{
    public string CurrentPath { get; set; } = string.Empty;
    public long DirectoriesScanned { get; set; }
    public long FilesScanned { get; set; }
    public int Percent { get; set; }
}
