using WinDiskCleaner.Core.Models;
using WinDiskCleaner.Core.Services;

namespace WinDiskCleaner.Core.Tests;

public class Mvp1RiskClassificationIntegrationTests
{
    [Fact]
    public async Task DiskScanner_ScanDriveAsync_AppliesRiskClassifierToAllReportGroups()
    {
        var root = Path.Combine(Environment.CurrentDirectory, "WinDiskCleanerRiskTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "Temp"));
        Directory.CreateDirectory(Path.Combine(root, "Downloads"));
        Directory.CreateDirectory(Path.Combine(root, "Program Files"));
        Directory.CreateDirectory(Path.Combine(root, "Windows"));
        await File.WriteAllTextAsync(Path.Combine(root, "Temp", "cache.tmp"), new string('a', 10));
        await File.WriteAllTextAsync(Path.Combine(root, "Downloads", "user.zip"), new string('b', 20));

        var scanner = new DiskScanner();

        var report = await scanner.ScanDriveAsync(root);

        Assert.Contains(report.LowRiskItems, item => item.Path.EndsWith(Path.Combine("Temp", "cache.tmp")) && item.RiskLevel == RiskLevel.Low);
        Assert.Contains(report.MediumRiskItems, item => item.Name == "Downloads" && item.RiskLevel == RiskLevel.Medium);
        Assert.Contains(report.HighRiskItems, item => item.Name == "Program Files" && item.RiskLevel == RiskLevel.High);
        Assert.Contains(report.ForbiddenItems, item => item.Name == "Windows" && item.RiskLevel == RiskLevel.Forbidden);
        Assert.Equal(report.LowRiskItems.Sum(item => item.Size), report.EstimatedSafeClean);
        Assert.Equal(report.MediumRiskItems.Sum(item => item.Size), report.EstimatedConfirmClean);
    }
}
