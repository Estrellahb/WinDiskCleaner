using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WinDiskCleaner.App.ViewModels;
using WinDiskCleaner.Core.Interfaces;
using WinDiskCleaner.Core.Models;
using WinDiskCleaner.Core.Services;

namespace WinDiskCleaner.App.Views;

public partial class ShortcutTab : UserControl
{
    private readonly ShortcutViewModel _viewModel = new();
    private readonly IShortcutScanner _scanner = new ShortcutScanner();
    private readonly IShortcutDeleteService _deleteService = new ShortcutDeleteService();
    private readonly IShortcutReportExporter _reportExporter = new ShortcutReportExporter();
    private CancellationTokenSource? _scanCts;
    private CancellationTokenSource? _deleteCts;

    public ShortcutTab()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _viewModel.AddLog("快捷方式页已就绪");
    }

    private async void StartScanBtn_Click(object sender, RoutedEventArgs e)
    {
        StartScanBtn.IsEnabled = false;
        CancelScanBtn.IsEnabled = true;
        ScanProgressBar.IsIndeterminate = true;
        StatusText.Text = "扫描中...";
        _scanCts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<ScanProgress>(p =>
            {
                StatusText.Text = $"已扫描 {p.FilesScanned} 个文件：{p.CurrentPath}";
            });

            _viewModel.AddLog("开始扫描快捷方式");
            var shortcuts = await _scanner.ScanShortcutsAsync(progress, _scanCts.Token);
            _viewModel.LoadShortcuts(shortcuts);
            RefreshStats();
            StatusText.Text = $"扫描完成：{_viewModel.StatisticsText}";
            _viewModel.AddLog(StatusText.Text);
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "扫描已取消";
            _viewModel.AddLog("扫描已取消");
        }
        catch (Exception ex)
        {
            StatusText.Text = $"扫描失败：{ex.Message}";
            _viewModel.AddLog(StatusText.Text);
            MessageBox.Show(ex.Message, "快捷方式扫描失败", MessageBoxButton.OK, MessageBoxImage.Error);
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

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _viewModel.SearchQuery = SearchBox.Text;
    }

    private void SortPathBtn_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SortByPath();
    }

    private void SortStatusBtn_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SortByStatus();
    }

    private void SelectInvalidBtn_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectInvalid();
        ShortcutGrid.Items.Refresh();
    }

    private void InvertInvalidBtn_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.InvertInvalidSelection();
        ShortcutGrid.Items.Refresh();
    }

    private async void DeleteSelectedBtn_Click(object sender, RoutedEventArgs e)
    {
        var selected = _viewModel.GetSelectedInvalidShortcuts();
        if (selected.Count == 0)
        {
            MessageBox.Show("请先勾选失效快捷方式。有效快捷方式不会进入删除候选。", "删除选中", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var preview = string.Join(Environment.NewLine + Environment.NewLine, selected.Select(item => $"快捷方式：{item.ShortcutPath}{Environment.NewLine}目标：{item.TargetPath}"));
        var confirm = MessageBox.Show($"确认删除以下 {selected.Count} 个失效快捷方式？只会删除 .lnk 文件本身，不会删除目标程序或文件。\n\n{preview}", "确认删除快捷方式", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            _viewModel.AddLog("删除已取消");
            return;
        }

        DeleteSelectedBtn.IsEnabled = false;
        _deleteCts = new CancellationTokenSource();
        try
        {
            var result = await _deleteService.DeleteSelectedInvalidShortcutsAsync(selected, _deleteCts.Token);
            _viewModel.RemoveDeleted(result.DeletedPaths);
            RefreshStats();
            ShortcutGrid.Items.Refresh();
            StatusText.Text = $"删除完成：成功 {result.DeletedPaths.Count}，失败 {result.Failures.Count}";
            _viewModel.AddLog(StatusText.Text);
            foreach (var failure in result.Failures)
            {
                _viewModel.AddLog($"删除失败：{failure.Path}；{failure.Reason}");
            }
        }
        catch (OperationCanceledException)
        {
            StatusText.Text = "删除已取消";
            _viewModel.AddLog("删除已取消");
        }
        finally
        {
            DeleteSelectedBtn.IsEnabled = true;
            _deleteCts?.Dispose();
            _deleteCts = null;
        }
    }

    private void ExportReportBtn_Click(object sender, RoutedEventArgs e)
    {
        var items = _viewModel.GetAllShortcuts();
        if (items.Count == 0)
        {
            MessageBox.Show("暂无快捷方式报告可导出。", "导出报告", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "导出快捷方式报告",
            FileName = $"shortcut_report_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            Filter = "CSV 报告 (*.csv)|*.csv|HTML 报告 (*.html)|*.html"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var extension = Path.GetExtension(dialog.FileName).ToLowerInvariant();
        var content = extension == ".html"
            ? _reportExporter.ExportHtml(items)
            : _reportExporter.ExportCsv(items);
        File.WriteAllText(dialog.FileName, content, System.Text.Encoding.UTF8);
        StatusText.Text = $"报告已导出：{dialog.FileName}";
        _viewModel.AddLog(StatusText.Text);
    }

    private void RefreshStats()
    {
        StatsText.Text = _viewModel.StatisticsText;
    }
}
