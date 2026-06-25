using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using WinDiskCleaner.App.ViewModels;
using WinDiskCleaner.Core.Services;
using WinDiskCleaner.Infrastructure.Registry;

namespace WinDiskCleaner.App.Views;

public partial class RegistryTab : UserControl
{
    private readonly RegistryViewModel _viewModel = new();
    private readonly RegistryScanner _scanner;
    private readonly RegistryBackupService _backupService;
    private readonly string _backupDir;
    private string _selectedRegFile = string.Empty;

    public RegistryTab()
    {
        InitializeComponent();
        DataContext = _viewModel;
        _scanner = new RegistryScanner(new WindowsRegistryReader());
        _backupDir = Path.Combine(GetAppDataRoot(), "RegistryBackups");
        _backupService = new RegistryBackupService(new WindowsRegistryBranchExporter(), new WindowsRegistryFileImporter(), Path.Combine(GetAppDataRoot(), "Logs", "registry-operations.log"), _backupDir);
        RefreshBackupHistory();
        _viewModel.AddLog("注册表页已就绪");
    }

    private async void ScanSoftwareBtn_Click(object sender, RoutedEventArgs e)
    {
        ScanSoftwareBtn.IsEnabled = false;
        try
        {
            _viewModel.AddLog("开始扫描卸载注册表项");
            var software = await _scanner.ScanInstalledSoftwareAsync();
            _viewModel.LoadSoftware(software);
            _viewModel.AddLog($"扫描完成：{software.Count} 项，残留 {software.Count(item => item.IsOrphan)} 项，无效 {software.Count(item => !item.IsValid)} 项");
        }
        catch (Exception ex)
        {
            _viewModel.AddLog($"扫描失败：{ex.Message}");
            MessageBox.Show(ex.Message, "注册表扫描失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            ScanSoftwareBtn.IsEnabled = true;
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _viewModel.SearchQuery = SearchBox.Text;
    }

    private void SortNameBtn_Click(object sender, RoutedEventArgs e) => _viewModel.SortByName();

    private void SortSizeBtn_Click(object sender, RoutedEventArgs e) => _viewModel.SortBySize();

    private void SortPublisherBtn_Click(object sender, RoutedEventArgs e) => _viewModel.SortByPublisher();

    private async void BackupBranchBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var branch = BranchCombo.SelectedItem?.ToString() ?? _viewModel.SelectedBackupBranch;
            var filePath = await _backupService.BackupBranchAsync(branch, _backupDir);
            _viewModel.AddLog($"备份完成：{filePath}");
            RefreshBackupHistory();
        }
        catch (Exception ex)
        {
            _viewModel.AddLog($"备份失败：{ex.Message}");
            MessageBox.Show(ex.Message, "注册表备份失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void DeleteBackupBtn_Click(object sender, RoutedEventArgs e)
    {
        if (BackupHistoryList.SelectedItem is not Core.Models.RegistryBackup backup)
        {
            MessageBox.Show("请先选择一个备份文件。", "删除备份", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirm = MessageBox.Show($"确认删除备份文件？\n{backup.FilePath}", "删除备份", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        if (_backupService.DeleteBackup(backup.FilePath))
        {
            _viewModel.AddLog($"已删除备份：{backup.FilePath}");
            RefreshBackupHistory();
        }
    }

    private void ChooseRegFileBtn_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择注册表备份文件",
            Filter = "Registry files (*.reg)|*.reg"
        };

        if (dialog.ShowDialog() == true)
        {
            _selectedRegFile = dialog.FileName;
            SelectedRegFileText.Text = _selectedRegFile;
            PreviewBox.Text = string.Empty;
        }
    }

    private async void PreviewRegFileBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedRegFile))
        {
            MessageBox.Show("请先选择 .reg 文件。", "预览注册表恢复", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var preview = await _backupService.PreviewRestoreFileAsync(_selectedRegFile);
        PreviewBox.Text = preview.IsValid
            ? string.Join(Environment.NewLine, preview.Branches)
            : $"文件不可导入：{preview.ErrorMessage}";
        _viewModel.AddLog(preview.IsValid ? "恢复预览完成" : $"恢复预览失败：{preview.ErrorMessage}");
    }

    private async void RestoreBtn_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_selectedRegFile))
        {
            MessageBox.Show("请先选择 .reg 文件。", "恢复注册表", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var preview = await _backupService.PreviewRestoreFileAsync(_selectedRegFile);
        if (!preview.IsValid)
        {
            PreviewBox.Text = $"文件不可导入：{preview.ErrorMessage}";
            _viewModel.AddLog($"恢复被拒绝：{preview.ErrorMessage}");
            return;
        }

        var firstConfirm = MessageBox.Show($"即将导入以下注册表分支：\n{string.Join(Environment.NewLine, preview.Branches)}\n\n确认进入最终恢复确认？", "确认恢复注册表", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (firstConfirm != MessageBoxResult.Yes)
        {
            _viewModel.AddLog("恢复已取消");
            return;
        }

        var secondConfirm = MessageBox.Show("最终确认：导入 .reg 文件会修改注册表。确认执行恢复？", "二次确认：恢复注册表", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (secondConfirm != MessageBoxResult.Yes)
        {
            _viewModel.AddLog("恢复已取消");
            return;
        }

        var result = await _backupService.RestoreFromFileAsync(_selectedRegFile);
        _viewModel.AddLog(result ? $"恢复完成：{_selectedRegFile}" : $"恢复失败：{_selectedRegFile}");
        MessageBox.Show(result ? "恢复命令已执行。" : "恢复失败，详情请查看日志。", "恢复注册表", MessageBoxButton.OK, result ? MessageBoxImage.Information : MessageBoxImage.Error);
    }

    private void RefreshBackupHistory()
    {
        _viewModel.LoadBackupHistory(_backupService.GetBackupHistory(_backupDir));
    }

    private static string GetAppDataRoot()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(appData))
        {
            appData = Path.Combine(Path.GetTempPath(), "WinDiskCleaner");
        }

        return Path.Combine(appData, "WinDiskCleaner");
    }
}
