using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using WinDiskCleaner.Core.Interfaces;
using WinDiskCleaner.Core.Models;
using WinDiskCleaner.Core.Services;

namespace WinDiskCleaner.App.Views;

public partial class ScanTab : UserControl
{
    private ScanReport? _lastScanReport;

    public ObservableCollection<AISuggestionItem> Suggestions { get; } = new();

    public ScanTab()
    {
        InitializeComponent();
        DataContext = this;
    }

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        StatusTextBlock.Text = "扫描中...";
        ScanSummaryTextBlock.Text = "正在扫描磁盘，请稍候";
        try
        {
            var scanner = new DiskScanner();
            var progress = new Progress<ScanProgress>(p =>
            {
                StatusTextBlock.Text = $"扫描中：{p.CurrentPath}";
            });

            var options = new ScanOptions { SkippedDirectoryNames = SettingsView.SkippedDirectoryNames.ToList() };
            _lastScanReport = await scanner.ScanDriveAsync(DrivePathTextBox.Text, options, progress);
            ScanSummaryTextBlock.Text = $"Top 目录 {_lastScanReport.TopDirectories.Count} 个，Top 文件 {_lastScanReport.TopFiles.Count} 个，低风险 {_lastScanReport.LowRiskItems.Count} 项，预计安全清理 {_lastScanReport.EstimatedSafeClean} 字节";
            StatusTextBlock.Text = "扫描完成";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"扫描失败：{ex.Message}";
        }
    }

    private async void AnalyzeButton_Click(object sender, RoutedEventArgs e)
    {
        StatusTextBlock.Text = "AI 分析中...";
        try
        {
            using var httpClient = new HttpClient();
            var service = new AIService(httpClient, string.Empty, "https://api.openai.com/v1");
            var report = _lastScanReport ?? CreatePlaceholderReport();
            var suggestion = await service.AnalyzeReportAsync(report);

            Suggestions.Clear();
            foreach (var item in suggestion.Items)
            {
                item.Selected = true;
                Suggestions.Add(item);
            }

            StatusTextBlock.Text = $"AI 分析完成：{Suggestions.Count} 条建议";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"AI 分析失败：{ex.Message}";
        }
    }

    private async void ExecuteCleanButton_Click(object sender, RoutedEventArgs e)
    {
        StatusTextBlock.Text = "执行清理中...";
        var selectedItems = Suggestions.Where(item => item.Selected).ToList();
        var executor = new CleanExecutor();
        var result = await executor.ExecuteAsync(selectedItems);
        StatusTextBlock.Text = $"清理完成：成功 {result.Succeeded}，失败 {result.Failed}，释放 {result.FreedBytes} 字节";
    }

    private static ScanReport CreatePlaceholderReport()
    {
        return new ScanReport
        {
            Items = new List<ScanReportItem>()
        };
    }
}
