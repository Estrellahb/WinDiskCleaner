using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using WinDiskCleaner.Core.Models;
using WinDiskCleaner.Core.Services;

namespace WinDiskCleaner.App.Views;

public partial class CleanSuggestionView : UserControl
{
    private ScanReport? _currentReport;

    public List<SuggestionCard> Suggestions { get; set; } = new();
    public ObservableCollection<AiSuggestionCard> AiSuggestions { get; } = new();
    public string SafeCleanText { get; set; } = "可安全清理：0 B";
    public string ConfirmCleanText { get; set; } = "需确认：0 B";
    public string ForbiddenText { get; set; } = "禁止删除：0 B";

    public CleanSuggestionView()
    {
        InitializeComponent();
        DataContext = this;
    }

    public void LoadReport(ScanReport report)
    {
        _currentReport = report;
        Suggestions.Clear();
        foreach (var item in report.LowRiskItems)
        {
            Suggestions.Add(new SuggestionCard(item, "可清理", "#107C10"));
        }

        foreach (var item in report.MediumRiskItems)
        {
            Suggestions.Add(new SuggestionCard(item, "需确认", "#FF8C00"));
        }

        foreach (var item in report.ForbiddenItems)
        {
            Suggestions.Add(new SuggestionCard(item, "禁止删除", "#E81123"));
        }

        SafeCleanText = $"可安全清理：{ReportGenerator.FormatSize(report.EstimatedSafeClean)}";
        ConfirmCleanText = $"需确认：{ReportGenerator.FormatSize(report.EstimatedConfirmClean)}";
        ForbiddenText = $"禁止删除：{ReportGenerator.FormatSize(report.ForbiddenItems.Sum(x => x.Size))}";
        DataContext = null;
        DataContext = this;
    }

    private void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentReport is not null)
        {
            LoadReport(_currentReport);
        }
    }

    private async void ExportBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentReport is null)
        {
            MessageBox.Show("暂无可导出的扫描报告，请先完成一次扫描。", "导出报告", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "导出扫描报告",
            FileName = $"scan_report_{DateTime.Now:yyyyMMdd_HHmmss}.json",
            Filter = "JSON 报告 (*.json)|*.json|HTML 报告 (*.html)|*.html|Markdown 报告 (*.md)|*.md"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var generator = new ReportGenerator();
        var extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
        switch (extension)
        {
            case ".html":
                await File.WriteAllTextAsync(dialog.FileName, generator.GenerateHtmlReport(_currentReport));
                break;
            case ".md":
                await File.WriteAllTextAsync(dialog.FileName, generator.GenerateMarkdownReport(_currentReport));
                break;
            default:
                await generator.SaveToFileAsync(_currentReport, dialog.FileName);
                break;
        }
    }

    private async void AIAnalyzeBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_currentReport is null)
        {
            MessageBox.Show("请先完成一次扫描。", "AI 分析", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        AiStatusText.Text = "AI 分析中...";
        try
        {
            using var httpClient = new HttpClient();
            var service = new AIService(httpClient, SettingsView.ApiKey, SettingsView.BaseUrl, SettingsView.ModelName);
            var suggestion = await service.AnalyzeReportAsync(_currentReport);
            AiSuggestions.Clear();
            foreach (var item in suggestion.Items)
            {
                AiSuggestions.Add(new AiSuggestionCard(item));
            }

            AiStatusText.Text = $"AI 建议：{suggestion.Summary}（{AiSuggestions.Count} 项）";
        }
        catch (Exception ex)
        {
            AiStatusText.Text = $"AI 分析失败：{ex.Message}";
        }
    }

    private async void ExecuteCleanBtn_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = AiSuggestions
            .Where(card => card.Selected)
            .Select(card => card.Item)
            .ToList();
        if (selectedItems.Count == 0)
        {
            MessageBox.Show("请先勾选要清理的 AI 建议。", "执行清理", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        AiStatusText.Text = "执行清理中...";
        var executor = new CleanExecutor();
        var result = await executor.ExecuteAsync(selectedItems);
        RefreshAfterClean(result);
        AiStatusText.Text = $"清理完成：成功 {result.Succeeded}，失败 {result.Failed}，释放 {ReportGenerator.FormatSize(result.FreedBytes)}";
    }

    private void RefreshAfterClean(WinDiskCleaner.Core.Interfaces.CleanResult result)
    {
        if (_currentReport is null || result.SucceededPaths.Count == 0)
        {
            return;
        }

        var successPaths = result.SucceededPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var card in AiSuggestions.Where(card => successPaths.Contains(card.Path)).ToList())
        {
            AiSuggestions.Remove(card);
        }

        _currentReport.EstimatedSafeClean = Math.Max(0, _currentReport.EstimatedSafeClean - result.FreedBytes);
        _currentReport.LowRiskItems.RemoveAll(node => successPaths.Contains(node.Path));
        LoadReport(_currentReport);
    }
}

public class SuggestionCard
{
    public ScanNode Node { get; set; }
    public string Path => Node.Path;
    public string SizeText => ReportGenerator.FormatSize(Node.Size);
    public string Description { get; set; }
    public string ActionText { get; set; }
    public Brush RiskColor { get; set; }

    public SuggestionCard(ScanNode node, string action, string color)
    {
        Node = node;
        ActionText = action;
        RiskColor = (Brush)new BrushConverter().ConvertFrom(color)!;
        Description = node.RiskLevel switch
        {
            RiskLevel.Low => "临时文件或缓存，删除后系统会自动重建",
            RiskLevel.Medium => "用户文件，请人工确认后再处理",
            RiskLevel.Forbidden => "系统关键目录，禁止手动删除",
            _ => "建议谨慎处理"
        };
    }
}

public class AiSuggestionCard
{
    public AISuggestionItem Item { get; }
    public bool Selected
    {
        get => Item.Selected;
        set => Item.Selected = value;
    }

    public string Path => Item.Path;
    public string Reason => Item.Reason;
    public string SizeText => ReportGenerator.FormatSize(Item.SizeBytes);
    public string ConfidenceText => $"置信度 {Item.Confidence:P0}";

    public AiSuggestionCard(AISuggestionItem item)
    {
        Item = item;
        Item.Selected = true;
    }
}
