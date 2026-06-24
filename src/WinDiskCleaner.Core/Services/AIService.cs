using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WinDiskCleaner.Core.Interfaces;
using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Services;

public class AIService : IAIService
{
    private static readonly JsonSerializerOptions ReportJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

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
        var jsonReport = JsonSerializer.Serialize(report, ReportJsonOptions);
        var prompt = BuildPrompt(jsonReport);
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

    private string BuildPrompt(string jsonReport)
    {
        return $@"你是一个 Windows 磁盘清理顾问。以下是磁盘扫描报告的 JSON，请分析哪些文件/目录可以安全删除。
分析规则：
1. 低风险：临时文件、缓存、崩溃转储，建议删除
2. 中风险：下载文件、桌面文件，需用户确认
3. 高风险：程序目录，不建议手动删除
4. 禁止删除：系统目录，绝对不能删
请按以下 JSON 格式返回分析结果，不要加额外说明：
{{
""suggestions"": [
{{
""path"": ""完整路径"",
""action"": ""delete/keep/manual"",
""reason"": ""删除理由"",
""estimatedSpace"": 字节数,
""confidence"": 0.0-1.0
}}
]
}}
报告 JSON：
{jsonReport}";
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
