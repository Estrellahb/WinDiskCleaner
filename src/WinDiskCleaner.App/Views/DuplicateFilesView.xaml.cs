using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WinDiskCleaner.Core.Interfaces;
using WinDiskCleaner.Core.Models;
using WinDiskCleaner.Core.Services;

namespace WinDiskCleaner.App.Views;

public partial class DuplicateFilesView : UserControl
{
    private CancellationTokenSource? _scanCts;

    public ObservableCollection<DuplicateGroupCard> DuplicateGroups { get; } = new();

    public DuplicateFilesView()
    {
        InitializeComponent();
        DataContext = this;
        ScanPathsBox.Text = string.Join(";", GetDefaultScanPaths());
    }

    private void BrowseBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "选择重复文件扫描目录"
        };

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
        {
            ScanPathsBox.Text = dialog.FolderName;
        }
    }

    private async void StartScanBtn_Click(object sender, RoutedEventArgs e)
    {
        var scanPaths = ParseScanPaths();
        if (scanPaths.Count == 0)
        {
            MessageBox.Show("请先选择至少一个扫描目录。", "重复文件检测", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DuplicateGroups.Clear();
        _scanCts = new CancellationTokenSource();
        StartScanBtn.IsEnabled = false;
        CancelScanBtn.IsEnabled = true;
        ScanProgressBar.IsIndeterminate = true;
        StatusText.Text = "扫描中...";

        try
        {
            var finder = new DuplicateFinder();
            var progress = new Progress<ScanProgress>(p =>
            {
                StatusText.Text = $"已扫描 {p.FilesScanned} 个文件：{p.CurrentPath}";
            });
            var groups = await finder.FindDuplicatesAsync(scanPaths, progress, _scanCts.Token);
            foreach (var group in groups)
            {
                DuplicateGroups.Add(new DuplicateGroupCard(group));
            }

            StatusText.Text = $"扫描完成：发现 {DuplicateGroups.Count} 组重复文件";
            RefreshWastedSpace();
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "扫描已取消";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"扫描失败：{ex.Message}";
        }
        finally
        {
            ScanProgressBar.IsIndeterminate = false;
            StartScanBtn.IsEnabled = true;
            CancelScanBtn.IsEnabled = false;
            _scanCts?.Dispose();
            _scanCts = null;
        }
    }

    private void CancelScanBtn_Click(object sender, RoutedEventArgs e)
    {
        _scanCts?.Cancel();
    }

    private async void DeleteSelectedBtn_Click(object sender, RoutedEventArgs e)
    {
        var selectedItems = DuplicateGroups
            .SelectMany(group => group.Files)
            .Where(file => file.Selected && !file.IsRecommended)
            .Select(file => new AISuggestionItem
            {
                Path = file.Path,
                Action = "delete",
                EstimatedSpace = file.Size,
                Risk = CleanRisk.Low,
                Reason = "重复文件清理"
            })
            .ToList();

        if (selectedItems.Count == 0)
        {
            MessageBox.Show("请先勾选要删除的重复文件。", "删除选中", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show($"确认将 {selectedItems.Count} 个重复文件移到回收站？", "删除选中", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        var executor = new CleanExecutor();
        var result = await executor.ExecuteAsync(selectedItems, allowedLowRiskPaths: selectedItems.Select(item => item.Path));
        RefreshAfterDelete(result.SucceededPaths);
        StatusText.Text = $"删除完成：成功 {result.Succeeded}，失败 {result.Failed}，释放 {ReportGenerator.FormatSize(result.FreedBytes)}";
        if (result.Errors.Count > 0)
        {
            StatusText.Text += $"；错误 {result.Errors.Count} 项";
        }
    }

    private async void ExportReportBtn_Click(object sender, RoutedEventArgs e)
    {
        var groups = DuplicateGroups.Select(group => group.Source).ToList();
        if (groups.Count == 0)
        {
            MessageBox.Show("暂无重复文件报告可导出。", "导出报告", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "导出重复文件报告",
            FileName = $"duplicate_report_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            Filter = "CSV 报告 (*.csv)|*.csv|HTML 报告 (*.html)|*.html"
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var exporter = new DuplicateReportExporter();
        var extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
        if (extension == ".html")
        {
            await exporter.SaveHtmlAsync(groups, dialog.FileName);
        }
        else
        {
            await exporter.SaveCsvAsync(groups, dialog.FileName);
        }

        StatusText.Text = $"报告已导出：{dialog.FileName}";
    }

    private void SelectRecommendedBtn_Click(object sender, RoutedEventArgs e)
    {
        foreach (var file in DuplicateGroups.SelectMany(group => group.Files))
        {
            file.Selected = !file.IsRecommended;
        }

        GroupsList.Items.Refresh();
        RefreshWastedSpace();
    }

    private void InvertSelectionBtn_Click(object sender, RoutedEventArgs e)
    {
        foreach (var file in DuplicateGroups.SelectMany(group => group.Files).Where(file => !file.IsRecommended))
        {
            file.Selected = !file.Selected;
        }

        GroupsList.Items.Refresh();
        RefreshWastedSpace();
    }

    private void RefreshAfterDelete(List<string> succeededPaths)
    {
        var succeeded = succeededPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var group in DuplicateGroups.ToList())
        {
            group.Files.RemoveAll(file => succeeded.Contains(file.Path));
            group.Source.Files.RemoveAll(file => succeeded.Contains(file.Path));
            if (group.Files.Count < 2)
            {
                DuplicateGroups.Remove(group);
            }
            else
            {
                group.Recalculate();
            }
        }

        GroupsList.Items.Refresh();
        RefreshWastedSpace();
    }

    private void RefreshWastedSpace()
    {
        var selectedBytes = DuplicateGroups.SelectMany(group => group.Files)
            .Where(file => file.Selected && !file.IsRecommended)
            .Sum(file => file.Size);
        WastedSpaceText.Text = $"可释放空间：{ReportGenerator.FormatSize(selectedBytes)}";
    }

    private List<string> ParseScanPaths()
    {
        return ScanPathsBox.Text
            .Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> GetDefaultScanPaths()
    {
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new[] { "Downloads", "Desktop", "Videos", "Documents" }
            .Select(name => Path.Combine(userProfile, name))
            .Where(Directory.Exists)
            .ToList();
    }
}

public class DuplicateGroupCard
{
    public DuplicateGroup Source { get; }
    public string Hash => Source.Hash;
    public List<DuplicateFileCard> Files { get; private set; }
    public string SummaryText => $"文件数 {Files.Count}；占用 {ReportGenerator.FormatSize(Source.TotalSize)}；浪费 {ReportGenerator.FormatSize(Source.WastedSpace)}";

    public DuplicateGroupCard(DuplicateGroup source)
    {
        Source = source;
        Files = source.Files.Select(file => new DuplicateFileCard(file)).ToList();
    }

    public void Recalculate()
    {
        Source.TotalSize = Source.Files.Sum(file => file.Size);
        Source.WastedSpace = Source.Files.Count > 1 ? Source.Files.Skip(1).Sum(file => file.Size) : 0;
        Files = Source.Files.Select(file => new DuplicateFileCard(file)).ToList();
    }
}

public class DuplicateFileCard
{
    private readonly DuplicateFileInfo _source;

    public string Path => _source.Path;
    public long Size => _source.Size;
    public bool IsRecommended => _source.IsRecommended;
    public bool CanSelect => !IsRecommended;
    public string RecommendedText => IsRecommended ? "推荐保留" : string.Empty;
    public string DetailText => $"{ReportGenerator.FormatSize(_source.Size)} · 修改时间 {_source.LastModified:yyyy-MM-dd HH:mm:ss}";
    public bool Selected
    {
        get => _source.Selected;
        set => _source.Selected = value;
    }

    public DuplicateFileCard(DuplicateFileInfo source)
    {
        _source = source;
    }
}
