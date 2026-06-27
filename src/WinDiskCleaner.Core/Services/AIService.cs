using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinDiskCleaner.Core.Interfaces;
using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Services;

public class AIService : IAIService
{
    private static readonly JsonSerializerOptions ParseJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _httpClient;
    private string _baseUrl;
    private string _apiKey;
    private string _model;

    public AIService(string baseUrl = "https://api.openai.com/v1", string apiKey = "", string model = "gpt-4o")
        : this(new HttpClient(), apiKey, baseUrl, model)
    {
    }

    public AIService(HttpClient httpClient, string apiKey, string baseUrl, string model = "gpt-4o")
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = apiKey;
        _model = model;
    }

    public void Configure(string baseUrl, string apiKey, string model)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _apiKey = apiKey;
        _model = model;
    }

    public async Task<AISuggestion> AnalyzeReportAsync(ScanReport report)
    {
        var prompt = BuildPrompt(report);
        var response = await CallAIAsync(prompt);
        return ParseResponse(response);
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/models");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            using var resp = await _httpClient.SendAsync(request);
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private string BuildPrompt(ScanReport report)
    {
        var summary = BuildAnalysisSummary(report);
        return $@"你是一个 Windows 磁盘清理顾问。以下是本地规则整理后的候选清理项摘要，不是完整扫描 JSON。请只基于摘要判断，不要臆测不存在的路径。
分析规则：
1. 低风险：临时文件、缓存、崩溃转储，通常建议删除
2. 中风险：下载文件、桌面文件，需用户确认
3. 高风险：程序目录，不建议手动删除
4. 禁止删除：系统目录，绝对不能删
5. 本地程序会重新校验路径和风险，AI 不能越过本地安全规则
请按以下 JSON 格式返回分析结果，不要加额外说明：
{{
""suggestions"": [
{{
""path"": ""摘要中出现的样例路径或分组路径"",
""action"": ""delete/keep/manual"",
""reason"": ""删除理由"",
""estimatedSpace"": 字节数,
""confidence"": 0.0-1.0
}}
]
}}
候选清理项摘要：
{summary}";
    }

    private static string BuildAnalysisSummary(ScanReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"扫描范围：{report.Drive}");
        builder.AppendLine($"总容量：{FormatSize(report.TotalSize)}，已用：{FormatSize(report.UsedSize)}，剩余：{FormatSize(report.FreeSize)}");
        builder.AppendLine($"本地规则预计低风险可清理：{FormatSize(report.EstimatedSafeClean)}；需确认：{FormatSize(report.EstimatedConfirmClean)}");
        AppendRiskGroup(builder, "低风险候选", report.LowRiskItems, maxItems: 20);
        AppendRiskGroup(builder, "中风险候选", report.MediumRiskItems, maxItems: 12);
        AppendRiskGroup(builder, "高风险候选", report.HighRiskItems, maxItems: 6);
        AppendRiskGroup(builder, "禁止删除项", report.ForbiddenItems, maxItems: 6);
        if (report.LowRiskItems.Count == 0 && report.MediumRiskItems.Count == 0 && report.HighRiskItems.Count == 0 && report.ForbiddenItems.Count == 0)
        {
            AppendRiskGroup(builder, "Top 文件摘要", report.TopFiles, maxItems: 20);
            AppendReportItems(builder, report.Items, maxItems: 20);
        }

        return builder.ToString();
    }

    private static void AppendReportItems(StringBuilder builder, IReadOnlyCollection<ScanReportItem> items, int maxItems)
    {
        if (items.Count == 0)
        {
            return;
        }

        builder.AppendLine();
        builder.AppendLine($"报告条目摘要：共 {items.Count} 项，总大小 {FormatSize(items.Sum(item => item.SizeBytes))}");
        var index = 1;
        foreach (var item in items.OrderByDescending(item => item.SizeBytes).Take(maxItems))
        {
            builder.AppendLine($"{index}. 路径：{RedactPath(item.Path)}");
            builder.AppendLine($"- 大小：{FormatSize(item.SizeBytes)}，类别：{item.Category}");
            index++;
        }

        if (items.Count > maxItems)
        {
            builder.AppendLine($"- 未展开项：{items.Count - maxItems} 个报告条目，已在本地保留，未发送完整列表。");
        }
    }

    private static void AppendRiskGroup(StringBuilder builder, string title, IReadOnlyCollection<ScanNode> nodes, int maxItems)
    {
        builder.AppendLine();
        builder.AppendLine($"{title}：共 {nodes.Count} 项，总大小 {FormatSize(nodes.Sum(node => node.Size))}");
        var index = 1;
        foreach (var group in nodes
            .GroupBy(node => CreatePathPattern(node.Path))
            .Select(group => new
            {
                Pattern = group.Key,
                Count = group.Count(),
                TotalSize = group.Sum(node => node.Size),
                Risk = group.Select(node => node.RiskLevel).Distinct().Count() == 1 ? group.First().RiskLevel.ToString() : "Mixed",
                Oldest = group.Min(node => node.LastModified),
                Newest = group.Max(node => node.LastModified),
                Sample = group.OrderByDescending(node => node.Size).FirstOrDefault()
            })
            .OrderByDescending(group => group.TotalSize)
            .ThenBy(group => group.Pattern, StringComparer.CurrentCultureIgnoreCase)
            .Take(maxItems))
        {
            builder.AppendLine($"{index}. 路径模式：{group.Pattern}");
            builder.AppendLine($"- 数量：{group.Count}，总大小：{FormatSize(group.TotalSize)}，风险：{group.Risk}");
            if (group.Oldest != default || group.Newest != default)
            {
                builder.AppendLine($"- 修改时间范围：{FormatDate(group.Oldest)} 至 {FormatDate(group.Newest)}");
            }

            if (group.Sample is not null)
            {
                builder.AppendLine($"- 样例路径：{RedactPath(group.Sample.Path)}");
            }

            index++;
        }

        var hiddenCount = Math.Max(0, nodes.GroupBy(node => CreatePathPattern(node.Path)).Count() - maxItems);
        if (hiddenCount > 0)
        {
            builder.AppendLine($"- 未展开项：{hiddenCount} 个路径模式，已在本地保留，未发送完整列表。");
        }
    }

    private static string CreatePathPattern(string path)
    {
        var redacted = RedactPath(path).Replace('/', '\\');
        var directory = Path.GetDirectoryName(redacted);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return redacted;
        }

        return Path.Combine(directory, "*");
    }

    private static string RedactPath(string path)
    {
        var normalized = path.Replace('/', '\\');
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var temp = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        normalized = ReplacePrefix(normalized, userProfile, "%USERPROFILE%");
        normalized = ReplacePrefix(normalized, localAppData, "%LOCALAPPDATA%");
        normalized = ReplacePrefix(normalized, appData, "%APPDATA%");
        normalized = ReplacePrefix(normalized, temp, "%TEMP%");
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"^(?<drive>[A-Za-z]:\\Users\\)[^\\]+", "${drive}%USERNAME%");
        return normalized;
    }

    private static string ReplacePrefix(string path, string prefix, string replacement)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return path;
        }

        var normalizedPrefix = prefix.Replace('/', '\\').TrimEnd('\\');
        return path.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase)
            ? replacement + path[normalizedPrefix.Length..]
            : path;
    }

    private static string FormatSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var index = 0;
        while (size >= 1024 && index < suffixes.Length - 1)
        {
            size /= 1024;
            index++;
        }

        return $"{size:F1} {suffixes[index]}";
    }

    private static string FormatDate(DateTime value)
    {
        return value == default ? "未知" : value.ToString("yyyy-MM-dd");
    }

    private async Task<string> CallAIAsync(string prompt)
    {
        var requestBody = new
        {
            model = _model,
            messages = new[]
            {
                new { role = "system", content = "你是一个专业的 Windows 磁盘清理顾问，只返回 JSON 格式的分析结果。" },
                new { role = "user", content = prompt }
            },
            temperature = 0.1
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/chat/completions")
        {
            Content = content
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        using var response = await _httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private AISuggestion ParseResponse(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();

            if (string.IsNullOrWhiteSpace(content))
            {
                return CreateParseFailureSuggestion();
            }

            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}') + 1;
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                content = content.Substring(jsonStart, jsonEnd - jsonStart);
            }

            var suggestion = JsonSerializer.Deserialize<AISuggestion>(content, ParseJsonOptions);
            if (suggestion is null)
            {
                return CreateParseFailureSuggestion();
            }

            suggestion.AnalyzedAt = suggestion.AnalyzedAt == default ? DateTime.Now : suggestion.AnalyzedAt;
            foreach (var item in suggestion.Suggestions)
            {
                NormalizeSuggestionItem(item);
            }

            return suggestion;
        }
        catch
        {
            return CreateParseFailureSuggestion();
        }
    }

    private static void NormalizeSuggestionItem(AISuggestionItem item)
    {
        if (item.EstimatedSpace == 0 && item.SizeBytes > 0)
        {
            item.EstimatedSpace = item.SizeBytes;
        }

        item.Risk = item.Action.Equals("delete", StringComparison.OrdinalIgnoreCase)
            ? CleanRisk.Low
            : CleanRisk.High;
    }

    private static AISuggestion CreateParseFailureSuggestion()
    {
        return new AISuggestion
        {
            AnalyzedAt = DateTime.Now,
            Suggestions = new List<AISuggestionItem>
            {
                new() { Path = "解析失败", Action = "manual", Reason = "AI 返回格式异常，请重试", Confidence = 0 }
            }
        };
    }
}
