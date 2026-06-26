using System.Windows;
using System.Windows.Controls;
using WinDiskCleaner.App.Views;

namespace WinDiskCleaner.App;

public partial class MainWindow : Window
{
    private readonly CleanSuggestionView _smartClean = new();
    private readonly DuplicateFilesView _duplicateFiles = new();
    private readonly RegistryTab _registry = new();
    private readonly ShortcutTab _shortcuts = new();
    private readonly SettingsView _settings = new();

    public MainWindow()
    {
        InitializeComponent();
        TabList.SelectedIndex = 0;
    }

    private void TabList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ContentArea.Content = TabList.SelectedIndex switch
        {
            0 => _smartClean,
            1 => _duplicateFiles,
            2 => _registry,
            3 => _shortcuts,
            4 => _settings,
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
