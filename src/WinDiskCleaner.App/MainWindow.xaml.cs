using System.Windows;
using System.Windows.Controls;
using WinDiskCleaner.App.Views;

namespace WinDiskCleaner.App;

public partial class MainWindow : Window
{
    private readonly SpaceMapView _spaceMap = new();
    private readonly CleanSuggestionView _cleanSuggestion = new();
    private readonly DuplicateFilesView _duplicateFiles = new();
    private readonly SettingsView _settings = new();

    public MainWindow()
    {
        InitializeComponent();
        ContentArea.Content = _spaceMap;
        _spaceMap.OnScanCompleted += report =>
        {
            _cleanSuggestion.LoadReport(report);
        };
    }

    private void TabList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ContentArea.Content = TabList.SelectedIndex switch
        {
            0 => _spaceMap,
            1 => _cleanSuggestion,
            2 => _duplicateFiles,
            5 => _settings,
            _ => new TextBlock
            {
                Text = "Coming Soon",
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }
}
