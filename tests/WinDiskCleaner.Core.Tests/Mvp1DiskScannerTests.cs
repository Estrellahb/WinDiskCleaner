using WinDiskCleaner.Core.Interfaces;
using WinDiskCleaner.Core.Models;
using WinDiskCleaner.Core.Services;

namespace WinDiskCleaner.Core.Tests;

public class Mvp1DiskScannerTests
{
    [Fact]
    public async Task DiskScanner_ScanDriveAsync_ReturnsTopFilesAndRiskGroups()
    {
        var root = Path.Combine(Environment.CurrentDirectory, "WinDiskCleanerScannerTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "Temp"));
        Directory.CreateDirectory(Path.Combine(root, "Docs"));
        await File.WriteAllTextAsync(Path.Combine(root, "Temp", "a.tmp"), new string('a', 10));
        await File.WriteAllTextAsync(Path.Combine(root, "Docs", "b.txt"), new string('b', 5));
        var progressEvents = new List<string>();
        var progress = new RecordingProgress(p => progressEvents.Add(p.CurrentPath));
        var scanner = new DiskScanner();

        var report = await scanner.ScanDriveAsync(root, progress);

        Assert.Equal(root, report.Drive);
        Assert.True(report.ScanTime <= DateTime.Now);
        Assert.Contains(report.TopFiles, item => item.Name == "a.tmp" && item.Size == 10);
        Assert.Contains(report.TopDirectories, item => item.Name == "Temp");
        Assert.Contains(report.LowRiskItems, item => item.Path.EndsWith("a.tmp"));
        Assert.Contains(report.MediumRiskItems, item => item.Name == "Docs");
        Assert.True(report.EstimatedSafeClean >= 10);
        Assert.True(progressEvents.Count > 0);
    }

    [Fact]
    public void RiskClassifier_Classify_UsesBuiltInRules()
    {
        var classifier = new RiskClassifier();

        Assert.Equal(RiskLevel.Low, classifier.Classify(@"C:\Users\A\AppData\Local\Temp\x.tmp", false));
        Assert.Equal(RiskLevel.Medium, classifier.Classify(@"C:\Users\A\Downloads\x.zip", false));
        Assert.Equal(RiskLevel.High, classifier.Classify(@"C:\Program Files\App\app.exe", false));
        Assert.Equal(RiskLevel.Forbidden, classifier.Classify(@"C:\Windows\System32\kernel32.dll", false));
        Assert.Equal(RiskLevel.Medium, classifier.Classify(@"C:\Users\A\Documents", true));
        Assert.Equal(RiskLevel.Unknown, classifier.Classify(@"C:\Users\A\Documents\note.txt", false));
    }

    [Fact]
    public async Task DiskScanner_ScanDriveAsync_ReportsProgressPercentAboveZero()
    {
        var root = Path.Combine(Environment.CurrentDirectory, "WinDiskCleanerProgressTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(Path.Combine(root, "Temp"));
        await File.WriteAllTextAsync(Path.Combine(root, "Temp", "a.tmp"), new string('a', 10));
        var percentValues = new List<int>();
        var progress = new RecordingProgress(p => percentValues.Add(p.Percent));
        var scanner = new DiskScanner();

        await scanner.ScanDriveAsync(root, progress);

        Assert.Contains(percentValues, percent => percent > 0);
        Assert.Equal(100, percentValues.Last());
    }

    private sealed class RecordingProgress : IProgress<ScanProgress>
    {
        private readonly Action<ScanProgress> _handler;

        public RecordingProgress(Action<ScanProgress> handler)
        {
            _handler = handler;
        }

        public void Report(ScanProgress value)
        {
            _handler(value);
        }
    }
}
