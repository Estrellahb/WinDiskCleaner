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
        var groups = SmartCleanCandidateGrouper.Group(report);
        var prompt = SmartCleanAiPromptBuilder.Build(report, groups);
        var response = await CallAIAsync(prompt);
        var suggestion = ParseResponse(response);
        return MapGroupSuggestionsToLocalItems(suggestion, groups);
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

    public static AISuggestion MapGroupSuggestionsToLocalItems(AISuggestion suggestion, IReadOnlyList<SmartCleanCandidateGroup> groups)
    {
        if (suggestion.Suggestions.Count == 1
            && string.Equals(suggestion.Suggestions[0].Path, "解析失败", StringComparison.Ordinal)
            && string.Equals(suggestion.Suggestions[0].Action, "manual", StringComparison.OrdinalIgnoreCase))
        {
            return suggestion;
        }

        var groupMap = groups.ToDictionary(group => group.GroupId, StringComparer.OrdinalIgnoreCase);
        var mapped = new AISuggestion
        {
            Summary = suggestion.Summary,
            AnalyzedAt = suggestion.AnalyzedAt == default ? DateTime.Now : suggestion.AnalyzedAt
        };

        foreach (var aiItem in suggestion.Suggestions.Where(item => string.Equals(item.Action, "delete", StringComparison.OrdinalIgnoreCase)))
        {
            if (string.IsNullOrWhiteSpace(aiItem.GroupId) || !groupMap.TryGetValue(aiItem.GroupId, out var group))
            {
                continue;
            }

            if (group.RiskLevel != RiskLevel.Low)
            {
                continue;
            }

            foreach (var node in group.Nodes.Where(node => !string.IsNullOrWhiteSpace(node.Path)))
            {
                mapped.Suggestions.Add(new AISuggestionItem
                {
                    GroupId = group.GroupId,
                    Path = node.Path,
                    Action = "delete",
                    Reason = string.IsNullOrWhiteSpace(aiItem.Reason) ? $"AI 建议清理 {group.DisplayName}" : aiItem.Reason,
                    EstimatedSpace = node.Size,
                    Confidence = aiItem.Confidence,
                    Risk = CleanRisk.Low
                });
            }
        }

        return mapped;
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
