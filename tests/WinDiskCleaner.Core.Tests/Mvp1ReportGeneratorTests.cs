using System.Text.Json;
using WinDiskCleaner.Core.Models;
using WinDiskCleaner.Core.Services;

namespace WinDiskCleaner.Core.Tests;

public class Mvp1ReportGeneratorTests
{
    [Fact]
    public async Task ReportGenerator_GenerateJsonAndSaveToFile_UsesCamelCaseIndentedJson()
    {
        var report = CreateReport();
        var generator = new ReportGenerator();
        var outputPath = Path.Combine(Path.GetTempPath(), "WinDiskCleanerReportTests", Guid.NewGuid().ToString("N"), "report.json");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var json = generator.GenerateJson(report);
        await generator.SaveToFileAsync(report, outputPath);

        Assert.Contains("\"scanTime\"", json);
        Assert.Contains("\"topDirectories\"", json);
        Assert.True(File.Exists(outputPath));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(outputPath));
        Assert.Equal("C:", document.RootElement.GetProperty("drive").GetString());
    }

    [Fact]
    public void ReportGenerator_GenerateHtmlAndMarkdownReport_IncludesSummaryAndTopItems()
    {
        var report = CreateReport();
        var generator = new ReportGenerator();

        var html = generator.GenerateHtmlReport(report);
        var markdown = generator.GenerateMarkdownReport(report);

        Assert.Contains("磁盘扫描报告 - C:", html);
        Assert.Contains("1.00 GB", html);
        Assert.Contains("class='low'", html);
        Assert.Contains("C:\\Temp", html);
        Assert.Contains("# 磁盘扫描报告 - C:", markdown);
        Assert.Contains("| 总容量 | 10.00 GB |", markdown);
        Assert.Contains("## Top 大文件", markdown);
        Assert.Contains("C:\\Temp\\a.tmp", markdown);
    }

    [Theory]
    [InlineData(512, "512.00 B")]
    [InlineData(1024, "1.00 KB")]
    [InlineData(1048576, "1.00 MB")]
    [InlineData(1073741824, "1.00 GB")]
    public void ReportGenerator_FormatSize_UsesReadableUnits(long bytes, string expected)
    {
        Assert.Equal(expected, ReportGenerator.FormatSize(bytes));
    }

    private static ScanReport CreateReport()
    {
        return new ScanReport
        {
            Drive = "C:",
            ScanTime = new DateTime(2026, 6, 24, 16, 0, 0),
            TotalSize = 10L * 1024 * 1024 * 1024,
            UsedSize = 6L * 1024 * 1024 * 1024,
            FreeSize = 4L * 1024 * 1024 * 1024,
            EstimatedSafeClean = 1024L * 1024 * 1024,
            EstimatedConfirmClean = 512L * 1024 * 1024,
            TopDirectories = new List<ScanNode>
            {
                new() { Path = @"C:\Temp", Name = "Temp", Size = 1024L * 1024 * 1024, RiskLevel = RiskLevel.Low, IsDirectory = true }
            },
            TopFiles = new List<ScanNode>
            {
                new() { Path = @"C:\Temp\a.tmp", Name = "a.tmp", Size = 512L * 1024 * 1024, RiskLevel = RiskLevel.Low }
            }
        };
    }
}
