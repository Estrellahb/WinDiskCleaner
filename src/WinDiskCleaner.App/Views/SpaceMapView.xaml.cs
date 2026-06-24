using System.IO;
using System.Windows;
using System.Windows.Controls;
using WinDiskCleaner.Core.Interfaces;
using WinDiskCleaner.Core.Models;
using WinDiskCleaner.Core.Services;

namespace WinDiskCleaner.App.Views;

public partial class SpaceMapView : UserControl
{
    private readonly IDiskScanner _scanner = new DiskScanner();
    private CancellationTokenSource? _cts;

    public event Action<ScanReport>? OnScanCompleted;

    public SpaceMapView()
    {
        InitializeComponent();
        LoadDrives();
    }

    private void LoadDrives()
    {
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed))
        {
            DriveCombo.Items.Add(drive.Name.TrimEnd('\\'));
        }

        if (DriveCombo.Items.Count > 0 && DriveCombo.SelectedIndex < 0)
        {
            DriveCombo.SelectedIndex = 0;
        }
    }

    private async void ScanBtn_Click(object sender, RoutedEventArgs e)
    {
        if (DriveCombo.SelectedItem == null)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        ScanBtn.IsEnabled = false;
        CancelBtn.IsEnabled = true;
        try
        {
            var progress = new Progress<ScanProgress>(p => ScanProgress.Value = p.Percent);
            var report = await _scanner.ScanDriveAsync(DriveCombo.SelectedItem.ToString()!, progress, _cts.Token);
            Treemap.ItemsSource = report.TopDirectories.Take(20).ToList();

            var reportGen = new ReportGenerator();
            await reportGen.SaveToFileAsync(report, Path.Combine(Environment.CurrentDirectory, $"scan_report_{DateTime.Now:yyyyMMdd_HHmmss}.json"));
            OnScanCompleted?.Invoke(report);
        }
        catch (OperationCanceledException)
        {
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
}
