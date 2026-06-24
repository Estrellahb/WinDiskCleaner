using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using WinDiskCleaner.Core.Services;

namespace WinDiskCleaner.App.Views;

public partial class SettingsView : UserControl
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WinDiskCleaner",
        "settings.json");

    public static string BaseUrl { get; private set; } = "https://api.openai.com/v1";
    public static string ApiKey { get; private set; } = string.Empty;
    public static string ModelName { get; private set; } = "gpt-4o";

    public SettingsView()
    {
        InitializeComponent();
        LoadSettings();
    }

    private void LoadSettings()
    {
        if (File.Exists(SettingsPath))
        {
            try
            {
                var config = JsonSerializer.Deserialize<SettingsConfig>(File.ReadAllText(SettingsPath));
                if (config is not null)
                {
                    BaseUrl = string.IsNullOrWhiteSpace(config.BaseUrl) ? BaseUrl : config.BaseUrl;
                    ApiKey = config.ApiKey ?? string.Empty;
                    ModelName = string.IsNullOrWhiteSpace(config.Model) ? ModelName : config.Model;
                }
            }
            catch
            {
                StatusText.Text = "配置加载失败，已使用默认值";
            }
        }

        BaseUrlBox.Text = BaseUrl;
        ApiKeyBox.Password = ApiKey;
        ModelBox.Text = ModelName;
    }

    private async void TestBtn_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentValues();
        StatusText.Text = "测试中...";
        try
        {
            var ai = new AIService(BaseUrlBox.Text, ApiKeyBox.Password, ModelBox.Text);
            var ok = await ai.TestConnectionAsync();
            StatusText.Text = ok ? "连接成功！" : "连接失败，请检查配置";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"连接失败：{ex.Message}";
        }
    }

    private void SaveBtn_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentValues();
        SaveSettingsFile();
        StatusText.Text = "配置已保存";
    }

    private void SaveCurrentValues()
    {
        BaseUrl = string.IsNullOrWhiteSpace(BaseUrlBox.Text) ? "https://api.openai.com/v1" : BaseUrlBox.Text.Trim();
        ApiKey = ApiKeyBox.Password;
        ModelName = string.IsNullOrWhiteSpace(ModelBox.Text) ? "gpt-4o" : ModelBox.Text.Trim();
    }

    private static void SaveSettingsFile()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        var config = new SettingsConfig
        {
            BaseUrl = BaseUrl,
            ApiKey = ApiKey,
            Model = ModelName
        };
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }

    private sealed class SettingsConfig
    {
        public string BaseUrl { get; set; } = "https://api.openai.com/v1";
        public string ApiKey { get; set; } = string.Empty;
        public string Model { get; set; } = "gpt-4o";
    }
}
