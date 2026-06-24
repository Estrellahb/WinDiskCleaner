using WinDiskCleaner.Core.Interfaces;
using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Services;

public class DiskScanner : IDiskScanner
{
    private static readonly string[] SkipDirs =
    {
        "System Volume Information",
        "$Recycle.Bin",
        "Windows",
        "Program Files",
        "Program Files (x86)",
        "ProgramData"
    };

    private readonly RiskClassifier _riskClassifier;
    private long _directoriesScanned;
    private long _filesScanned;

    public DiskScanner(RiskClassifier? riskClassifier = null)
    {
        _riskClassifier = riskClassifier ?? new RiskClassifier();
    }

    public Task<ScanReport> ScanDriveAsync(string drivePath, CancellationToken ct = default)
    {
        return ScanDriveAsync(drivePath, null, ct);
    }

    public async Task<ScanReport> ScanDriveAsync(string drivePath, IProgress<ScanProgress>? progress, CancellationToken ct = default)
    {
        _directoriesScanned = 0;
        _filesScanned = 0;
        var rootDirectory = ResolveRootDirectory(drivePath);
        var drive = TryCreateDriveInfo(drivePath, rootDirectory);
        var report = new ScanReport
        {
            ScanTime = DateTime.Now,
            Drive = drive?.Name.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) ?? rootDirectory.FullName,
            TotalSize = drive?.TotalSize ?? 0,
            UsedSize = drive is null ? 0 : drive.TotalSize - drive.AvailableFreeSpace,
            FreeSize = drive?.AvailableFreeSpace ?? 0
        };

        var root = await ScanDirectoryAsync(rootDirectory, 0, progress, ct);
        progress?.Report(new ScanProgress
        {
            CurrentPath = rootDirectory.FullName,
            DirectoriesScanned = _directoriesScanned,
            FilesScanned = _filesScanned,
            Percent = 100
        });
        ApplyRisk(root);
        report.TopDirectories = FlattenAndSort(root, true).Take(10).ToList();
        report.TopFiles = FlattenAndSort(root, false).Take(20).ToList();
        CategorizeByRisk(root, report);
        report.EstimatedSafeClean = report.LowRiskItems.Sum(x => x.Size);
        report.EstimatedConfirmClean = report.MediumRiskItems.Sum(x => x.Size);
        report.Items = report.TopFiles.Select(file => new ScanReportItem
        {
            Path = file.Path,
            SizeBytes = file.Size,
            Category = file.RiskLevel.ToString(),
            Description = file.Name
        }).ToList();
        return report;
    }

    private async Task<ScanNode> ScanDirectoryAsync(DirectoryInfo dir, int depth, IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var node = new ScanNode
        {
            Path = dir.FullName,
            Name = string.IsNullOrWhiteSpace(dir.Name) ? dir.FullName : dir.Name,
            IsDirectory = true,
            LastModified = dir.Exists ? dir.LastWriteTime : DateTime.MinValue,
            RiskLevel = _riskClassifier.Classify(NormalizePathForClassification(dir.FullName), true)
        };

        if (SkipDirs.Contains(dir.Name, StringComparer.OrdinalIgnoreCase) || depth > 8)
        {
            return node;
        }

        try
        {
            _directoriesScanned++;
            foreach (var subDir in dir.GetDirectories())
            {
                var child = await ScanDirectoryAsync(subDir, depth + 1, progress, ct);
                node.Children.Add(child);
                node.Size += child.Size;
                node.FileCount += child.FileCount;
            }

            foreach (var file in dir.GetFiles())
            {
                ct.ThrowIfCancellationRequested();
                _filesScanned++;
                node.FileCount++;
                node.Size += file.Length;
                var fileNode = new ScanNode
                {
                    Path = file.FullName,
                    Name = file.Name,
                    Size = file.Length,
                    IsDirectory = false,
                    LastModified = file.LastWriteTime,
                    RiskLevel = _riskClassifier.Classify(NormalizePathForClassification(file.FullName), false)
                };
                node.Children.Add(fileNode);
            }

            progress?.Report(new ScanProgress
            {
                CurrentPath = dir.FullName,
                DirectoriesScanned = _directoriesScanned,
                FilesScanned = _filesScanned,
                Percent = CalculateProgressPercent()
            });
        }
        catch (UnauthorizedAccessException)
        {
            node.ErrorMessage = "无权限访问";
            node.RiskLevel = RiskLevel.Unknown;
        }
        catch (DirectoryNotFoundException)
        {
            node.ErrorMessage = "目录已不存在";
        }
        catch (IOException ex)
        {
            node.ErrorMessage = ex.Message;
        }

        return node;
    }

    private List<ScanNode> FlattenAndSort(ScanNode node, bool dirsOnly)
    {
        var list = new List<ScanNode>();

        void Walk(ScanNode current)
        {
            if (current.IsDirectory == dirsOnly && current != node)
            {
                list.Add(current);
            }

            foreach (var child in current.Children)
            {
                Walk(child);
            }
        }

        Walk(node);
        return list.OrderByDescending(x => x.Size).ToList();
    }

    private void CategorizeByRisk(ScanNode node, ScanReport report)
    {
        foreach (var scanNode in FlattenAndSort(node, false))
        {
            switch (scanNode.RiskLevel)
            {
                case RiskLevel.Low:
                    report.LowRiskItems.Add(scanNode);
                    break;
                case RiskLevel.Medium:
                    report.MediumRiskItems.Add(scanNode);
                    break;
                case RiskLevel.High:
                    report.HighRiskItems.Add(scanNode);
                    break;
                case RiskLevel.Forbidden:
                    report.ForbiddenItems.Add(scanNode);
                    break;
            }
        }

        foreach (var scanNode in FlattenAndSort(node, true))
        {
            switch (scanNode.RiskLevel)
            {
                case RiskLevel.Low:
                    report.LowRiskItems.Add(scanNode);
                    break;
                case RiskLevel.Medium:
                    report.MediumRiskItems.Add(scanNode);
                    break;
                case RiskLevel.High:
                    report.HighRiskItems.Add(scanNode);
                    break;
                case RiskLevel.Forbidden:
                    report.ForbiddenItems.Add(scanNode);
                    break;
            }
        }
    }

    private void ApplyRisk(ScanNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.ErrorMessage))
        {
            node.RiskLevel = RiskLevel.Unknown;
            foreach (var child in node.Children)
            {
                ApplyRisk(child);
            }

            return;
        }

        node.RiskLevel = _riskClassifier.Classify(NormalizePathForClassification(node.Path), node.IsDirectory);
        foreach (var child in node.Children)
        {
            ApplyRisk(child);
        }
    }

    private int CalculateProgressPercent()
    {
        var scannedItems = _directoriesScanned + _filesScanned;
        if (scannedItems <= 0)
        {
            return 0;
        }

        return (int)Math.Min(99, scannedItems);
    }

    private static string NormalizePathForClassification(string path)
    {
        return path.Replace(Path.AltDirectorySeparatorChar, '\\').Replace(Path.DirectorySeparatorChar, '\\');
    }

    private static DirectoryInfo ResolveRootDirectory(string drivePath)
    {
        if (Directory.Exists(drivePath))
        {
            return new DirectoryInfo(drivePath);
        }

        var drive = new DriveInfo(drivePath);
        return drive.RootDirectory;
    }

    private static DriveInfo? TryCreateDriveInfo(string drivePath, DirectoryInfo rootDirectory)
    {
        try
        {
            if (Directory.Exists(drivePath) && !Path.GetPathRoot(drivePath)?.Equals(drivePath, StringComparison.OrdinalIgnoreCase) == true)
            {
                return null;
            }

            return new DriveInfo(drivePath);
        }
        catch
        {
            try
            {
                return new DriveInfo(rootDirectory.Root.FullName);
            }
            catch
            {
                return null;
            }
        }
    }
}
