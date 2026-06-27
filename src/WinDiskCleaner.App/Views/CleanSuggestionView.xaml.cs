using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using WinDiskCleaner.Core.Interfaces;
using WinDiskCleaner.Core.Models;
using WinDiskCleaner.Core.Services;

namespace WinDiskCleaner.App.Views;

public partial class CleanSuggestionView : UserControl, INotifyPropertyChanged
{
    private const double MaxBarWidth = 220;
    private const int TopDirectoryLimit = 8;
    private const int ChildDirectoryLimit = 8;

    private readonly IDiskScanner _scanner = new DiskScanner();
    private CancellationTokenSource? _cts;
    private ScanReport? _currentReport;

    public ObservableCollection<DirectoryBarItem> DirectoryBars { get; } = new();
    public ObservableCollection<AiSuggestionCard> AiSuggestions { get; } = new();

    private string _totalSizeText = "0 B";
    public string TotalSizeText
    {
        get => _totalSizeText;
        set => SetProperty(ref _totalSizeText, value);
    }

    private string _usedSizeText = "0 B";
    public string UsedSizeText
    {
        get => _usedSizeText;
        set => SetProperty(ref _usedSizeText, value);
    }

    private string _freeSizeText = "0 B";
    public string FreeSizeText
    {
        get => _freeSizeText;
        set => SetProperty(ref _freeSizeText, value);
    }

    private string _spaceStatusText = "等待扫描";
    public string SpaceStatusText
    {
        get => _spaceStatusText;
        set => SetProperty(ref _spaceStatusText, value);
    }

    private Brush _spaceStatusBrush = Brushes.WhiteSmoke;
    public Brush SpaceStatusBrush
    {
        get => _spaceStatusBrush;
        set => SetProperty(ref _spaceStatusBrush, value);
    }

    private string _cleanResultText = string.Empty;
    public string CleanResultText
    {
        get => _cleanResultText;
        set => SetProperty(ref _cleanResultText, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public CleanSuggestionView()
    {
        InitializeComponent();
        DataContext = this;
        LoadDrives();
    }

    public void LoadReport(ScanReport report)
    {
        _currentReport = report;
        RenderReport(report);
    }

    private void LoadDrives()
    {
        DriveCombo.Items.Clear();
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
        {
            DriveCombo.Items.Add(drive.Name.TrimEnd('\\'));
        }

        if (DriveCombo.Items.Count > 0)
        {
            DriveCombo.SelectedIndex = 0;
        }
    }

    private async void ScanBtn_Click(object sender, RoutedEventArgs e)
    {
        if (DriveCombo.SelectedItem is null)
        {
            MessageBox.Show("未检测到可扫描的本地磁盘。", "智能清理", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _cts = new CancellationTokenSource();
        ScanBtn.IsEnabled = false;
        CancelBtn.IsEnabled = true;
        ScanProgress.Value = 0;
        ScanStatusText.Text = "扫描中...";
        AiStatusText.Text = "AI 建议：等待扫描完成";
        CleanResultText = string.Empty;
        AiSuggestions.Clear();

        try
        {
            var progress = new Progress<ScanProgress>(p => ScanProgress.Value = p.Percent);
            var report = await _scanner.ScanDriveAsync(DriveCombo.SelectedItem.ToString()!, progress, _cts.Token);
            _currentReport = report;
            RenderReport(report);
            ScanStatusText.Text = "扫描完成";
            await AnalyzeCurrentReportAsync(confirmPrivacy: true);
        }
        catch (OperationCanceledException)
        {
            ScanStatusText.Text = "扫描已取消";
            AiStatusText.Text = "AI 建议：扫描已取消";
        }
        finally
        {
            ScanBtn.IsEnabled = true;
            CancelBtn.IsEnabled = false;
        }
    }

    private void CancelBtn_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
    }

    private void RenderReport(ScanReport report)
    {
        TotalSizeText = ReportGenerator.FormatSize(report.TotalSize);
        UsedSizeText = ReportGenerator.FormatSize(report.UsedSize);
        FreeSizeText = ReportGenerator.FormatSize(report.FreeSize);
        UpdateSpaceStatus(report);

        DirectoryBars.Clear();
        foreach (var item in BuildDirectoryBars(report))
        {
            DirectoryBars.Add(item);
        }
    }

    private void UpdateSpaceStatus(ScanReport report)
    {
        var usedPercent = report.TotalSize <= 0 ? 0 : (double)report.UsedSize / report.TotalSize;
        if (usedPercent >= 0.9)
        {
            SpaceStatusText = "空间紧张";
            SpaceStatusBrush = (Brush)new BrushConverter().ConvertFrom("#FFE5D0")!;
        }
        else if (usedPercent >= 0.75)
        {
            SpaceStatusText = "建议清理";
            SpaceStatusBrush = (Brush)new BrushConverter().ConvertFrom("#FFF4CC")!;
        }
        else
        {
            SpaceStatusText = "空间正常";
            SpaceStatusBrush = (Brush)new BrushConverter().ConvertFrom("#DFF6DD")!;
        }
    }

    public static List<DirectoryBarItem> BuildDirectoryBars(ScanReport report)
    {
        var total = report.TopDirectories.Sum(node => node.Size);
        if (total <= 0)
        {
            total = report.UsedSize > 0 ? report.UsedSize : 1;
        }

        var ordered = report.TopDirectories
            .Where(node => node.Size > 0)
            .OrderByDescending(node => node.Size)
            .ToList();

        var result = ordered
            .Take(TopDirectoryLimit)
            .Select(node => DirectoryBarItem.FromNode(node, total, MaxBarWidth, ChildDirectoryLimit))
            .ToList();

        var remaining = ordered.Skip(TopDirectoryLimit).ToList();
        if (remaining.Count > 0)
        {
            var otherSize = remaining.Sum(node => node.Size);
            result.Add(new DirectoryBarItem
            {
                Name = "其他",
                Path = "其他小目录合计",
                Size = otherSize,
                Percent = Math.Round(otherSize * 100d / total, 2),
                RiskLevel = RiskLevel.Unknown,
                FileCount = remaining.Sum(node => node.FileCount),
                BarWidth = Math.Max(2, MaxBarWidth * otherSize / total),
                IsDirectory = true
            });
        }

        return result;
    }

    private async Task AnalyzeCurrentReportAsync(bool confirmPrivacy)
    {
        if (_currentReport is null)
        {
            AiStatusText.Text = "AI 建议：请先完成一次扫描";
            return;
        }

        if (string.IsNullOrWhiteSpace(SettingsView.ApiKey)
            || string.IsNullOrWhiteSpace(SettingsView.BaseUrl)
            || string.IsNullOrWhiteSpace(SettingsView.ModelName))
        {
            AiStatusText.Text = "AI 配置缺失：请先去设置页填写 API Key、Base URL 和模型名称";
            return;
        }

        if (confirmPrivacy)
        {
            var privacyResult = MessageBox.Show(
                $"AI 分析会把当前扫描结果的摘要发送到配置的 AI 服务：{SettingsView.BaseUrl}\n\n摘要会按清理项聚合，本地路径会脱敏，不会发送完整扫描 JSON 或完整文件清单。确认继续？",
                "AI 分析隐私确认",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (privacyResult != MessageBoxResult.Yes)
            {
                AiStatusText.Text = "AI 建议：已取消 AI 分析";
                return;
            }
        }

        AiStatusText.Text = "AI 分析中...";
        try
        {
            using var httpClient = new HttpClient();
            var service = new AIService(httpClient, SettingsView.ApiKey, SettingsView.BaseUrl, SettingsView.ModelName);
            var suggestion = await service.AnalyzeReportAsync(_currentReport);
            var allowedLowRiskPaths = _currentReport.LowRiskItems.Select(item => NormalizePath(item.Path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            AiSuggestions.Clear();
            foreach (var item in suggestion.Items.Where(item => string.Equals(item.Action, "delete", StringComparison.OrdinalIgnoreCase)))
            {
                if (allowedLowRiskPaths.Contains(NormalizePath(item.Path)))
                {
                    AiSuggestions.Add(new AiSuggestionCard(item));
                }
            }

            AiStatusText.Text = AiSuggestions.Count == 0
                ? "AI 分析完成：未发现可自动清理的 Low 风险建议"
                : $"AI 分析完成：已筛选出 {AiSuggestions.Count} 项 Low 风险清理建议";
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

        var confirm = MessageBox.Show(
            "已勾选的项目将默认移动到回收站。确认执行清理？",
            "执行清理确认",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        AiStatusText.Text = "执行清理中...";
        var allowedLowRiskPaths = _currentReport?.LowRiskItems.Select(item => item.Path).ToList() ?? new List<string>();
        var executor = new CleanExecutor();
        var result = await executor.ExecuteAsync(selectedItems, toRecycleBin: true, allowedLowRiskPaths: allowedLowRiskPaths);
        RefreshAfterClean(result);
        CleanResultText = $"清理结果：成功 {result.Succeeded}，失败 {result.Failed}，预计释放 {ReportGenerator.FormatSize(result.FreedBytes)}，实际磁盘容量请重新扫描确认";
        AiStatusText.Text = CleanResultText;
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

    private void DirectoryBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is DirectoryBarItem item && item.Children.Count > 0)
        {
            item.IsExpanded = !item.IsExpanded;
        }
    }

    private void RefreshAfterClean(CleanResult result)
    {
        if (_currentReport is null || result.SucceededPaths.Count == 0)
        {
            return;
        }

        var successPaths = result.SucceededPaths
            .Select(NormalizePath)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var card in AiSuggestions.Where(card => successPaths.Contains(NormalizePath(card.Path))).ToList())
        {
            AiSuggestions.Remove(card);
        }

        _currentReport.EstimatedSafeClean = Math.Max(0, _currentReport.EstimatedSafeClean - result.FreedBytes);
        _currentReport.LowRiskItems.RemoveAll(node => successPaths.Contains(NormalizePath(node.Path)));
        RemoveSucceededNodesAndRecalculate(_currentReport.TopDirectories, successPaths);
        RenderReport(_currentReport);
    }

    private static long RemoveSucceededNodesAndRecalculate(List<ScanNode> nodes, HashSet<string> successPaths)
    {
        nodes.RemoveAll(node => successPaths.Contains(NormalizePath(node.Path)));
        long total = 0;
        foreach (var node in nodes)
        {
            if (node.IsDirectory)
            {
                node.Size = RemoveSucceededNodesAndRecalculate(node.Children, successPaths);
                node.FileCount = node.Children.Sum(child => child.IsDirectory ? child.FileCount : 1);
            }

            total += node.Size;
        }

        return total;
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }
        catch
        {
            return path.Trim();
        }
    }

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class DirectoryBarItem : INotifyPropertyChanged
{
    private bool _isExpanded;

    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public double Percent { get; set; }
    public RiskLevel RiskLevel { get; set; }
    public long FileCount { get; set; }
    public bool IsDirectory { get; set; }
    public double BarWidth { get; set; }
    public List<DirectoryBarItem> Children { get; set; } = new();

    public string SizeText => ReportGenerator.FormatSize(Size);
    public string PercentText => $"{Percent:F1}%";
    public string KindLabel => string.Empty;
    public string DisplayName => IsExpanded && Children.Count > 0 ? $"{Name}（收起）" : Children.Count > 0 ? $"{Name}（展开）" : Name;
    public string TooltipText => $"{Path}\n文件数：{FileCount}\n大小：{SizeText}";
    public Brush RiskBrush => RiskLevel switch
    {
        RiskLevel.Low => (Brush)new BrushConverter().ConvertFrom("#107C10")!,
        RiskLevel.Medium => (Brush)new BrushConverter().ConvertFrom("#F9C74F")!,
        RiskLevel.High => (Brush)new BrushConverter().ConvertFrom("#FF8C00")!,
        RiskLevel.Forbidden => (Brush)new BrushConverter().ConvertFrom("#E81123")!,
        _ => (Brush)new BrushConverter().ConvertFrom("#8A8A8A")!
    };

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(VisibleChildren));
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public IEnumerable<DirectoryBarItem> VisibleChildren => IsExpanded ? Children : Enumerable.Empty<DirectoryBarItem>();

    public event PropertyChangedEventHandler? PropertyChanged;

    public static DirectoryBarItem FromNode(ScanNode node, long totalSize, double maxBarWidth, int childLimit)
    {
        totalSize = totalSize <= 0 ? 1 : totalSize;
        var item = new DirectoryBarItem
        {
            Name = string.IsNullOrWhiteSpace(node.Name) ? System.IO.Path.GetFileName(node.Path.TrimEnd('\\', '/')) : node.Name,
            Path = node.Path,
            Size = node.Size,
            Percent = Math.Round(node.Size * 100d / totalSize, 2),
            RiskLevel = node.RiskLevel,
            FileCount = node.FileCount,
            IsDirectory = node.IsDirectory,
            BarWidth = Math.Max(2, maxBarWidth * node.Size / totalSize)
        };

        var childTotal = node.Children.Sum(child => child.Size);
        if (childTotal <= 0)
        {
            childTotal = node.Size > 0 ? node.Size : 1;
        }

        item.Children = node.Children
            .Where(child => child.Size > 0)
            .OrderByDescending(child => child.Size)
            .Take(childLimit)
            .Select(child => FromNode(child, childTotal, maxBarWidth, 0))
            .ToList();

        return item;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
    public string SizeText => ReportGenerator.FormatSize(Item.SizeBytes > 0 ? Item.SizeBytes : Item.EstimatedSpace);
    public string ConfidenceText => $"置信度 {Item.Confidence:P0}";

    public AiSuggestionCard(AISuggestionItem item)
    {
        Item = item;
        Item.Selected = true;
    }
}
