using System.Net.Http;
using System.Windows;
using System.Windows.Controls;
using WinDiskCleaner.Core.Services;

namespace WinDiskCleaner.App.Views;

public partial class SettingsTab : UserControl
{
    public SettingsTab()
    {
        InitializeComponent();
    }

    private async void TestConnectionButton_Click(object sender, RoutedEventArgs e)
    {
        ConnectionStatusTextBlock.Text = "正在测试...";
        try
        {
            using var httpClient = new HttpClient();
            var service = new AIService(
                httpClient,
                ApiKeyPasswordBox.Password,
                BaseUrlTextBox.Text,
                ModelTextBox.Text);

            var success = await service.TestConnectionAsync();
            ConnectionStatusTextBlock.Text = success ? "连接成功" : "连接失败";
        }
        catch (Exception ex)
        {
            ConnectionStatusTextBlock.Text = $"连接失败：{ex.Message}";
        }
    }
}
