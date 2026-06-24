using System.IO;

namespace WinDiskCleaner.Core.Tests;

public class Mvp2SafetyHardeningTests
{
    [Fact]
    public void CleanSuggestionView_PrivacyPrompt_IsShownBeforeAiAnalysis()
    {
        var source = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "WinDiskCleaner.App", "Views", "CleanSuggestionView.xaml.cs"));

        var analyzeIndex = source.IndexOf("AnalyzeReportAsync", StringComparison.Ordinal);
        var privacyIndex = source.IndexOf("本地路径", StringComparison.Ordinal);

        Assert.True(privacyIndex >= 0, "AI 分析前需要提示会发送本地路径信息。");
        Assert.True(analyzeIndex >= 0, "应存在 AI 分析调用。");
        Assert.True(privacyIndex < analyzeIndex, "隐私提示必须发生在 AnalyzeReportAsync 之前。");
        Assert.Contains("MessageBoxResult.Yes", source);
    }
}
