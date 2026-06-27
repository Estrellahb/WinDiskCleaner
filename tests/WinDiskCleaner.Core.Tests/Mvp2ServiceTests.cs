using System.Net;
using System.Text;
using System.Text.Json;
using WinDiskCleaner.Core.Interfaces;
using WinDiskCleaner.Core.Models;
using WinDiskCleaner.Core.Services;

namespace WinDiskCleaner.Core.Tests;

public class Mvp2ServiceTests
{
    [Fact]
    public async Task AIService_AnalyzeReportAsync_PostsPromptAndParsesSuggestionsPayload()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedRequestBody = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            capturedRequestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "choices": [
                    {
                      "message": {
                        "content": "```json\n{\"suggestions\":[{\"path\":\"/tmp/a.tmp\",\"action\":\"delete\",\"reason\":\"临时文件\",\"estimatedSpace\":123,\"confidence\":0.91}]}\n```"
                      }
                    }
                  ]
                }
                """, Encoding.UTF8, "application/json")
            };
        }));
        var service = new AIService(httpClient, "test-key", "https://api.example.com/v1", "gpt-test");
        var report = new ScanReport
        {
            RootNode = new ScanNode
            {
                Path = "/",
                Name = "/",
                IsDirectory = true,
                Children = new List<ScanNode>
                {
                    new() { Path = "/tmp/full-tree-only.tmp", Name = "full-tree-only.tmp", Size = 999, RiskLevel = RiskLevel.Low }
                }
            },
            Items = new List<ScanReportItem>
            {
                new() { Path = "/tmp/a.tmp", SizeBytes = 123, Category = "Temp" }
            }
        };

        var suggestion = await service.AnalyzeReportAsync(report);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest!.Method);
        Assert.Equal("https://api.example.com/v1/chat/completions", capturedRequest.RequestUri!.ToString());
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization!.Scheme);
        Assert.Equal("test-key", capturedRequest.Headers.Authorization.Parameter);
        Assert.NotNull(capturedRequestBody);
        using var requestJson = JsonDocument.Parse(capturedRequestBody!);
        Assert.Equal("gpt-test", requestJson.RootElement.GetProperty("model").GetString());
        var messages = requestJson.RootElement.GetProperty("messages");
        var userPrompt = messages[1].GetProperty("content").GetString();
        Assert.Contains("Windows 磁盘清理顾问", userPrompt);
        Assert.Contains("a.tmp", userPrompt);
        Assert.DoesNotContain("rootNode", userPrompt);
        Assert.DoesNotContain("full-tree-only.tmp", userPrompt);

        var item = Assert.Single(suggestion.Suggestions);
        Assert.Equal("/tmp/a.tmp", item.Path);
        Assert.Equal("delete", item.Action);
        Assert.Equal("临时文件", item.Reason);
        Assert.Equal(123, item.EstimatedSpace);
        Assert.Equal(123, item.SizeBytes);
        Assert.Equal(CleanRisk.Low, item.Risk);
        Assert.Equal(0.91, item.Confidence, 2);
        Assert.True(suggestion.AnalyzedAt > DateTime.MinValue);
    }

    [Fact]
    public async Task AIService_AnalyzeReportAsync_SendsBoundedSummaryInsteadOfFullReportJson()
    {
        string? capturedRequestBody = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            capturedRequestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "choices": [
                    {
                      "message": {
                        "content": "{\"suggestions\":[]}"
                      }
                    }
                  ]
                }
                """, Encoding.UTF8, "application/json")
            };
        }));
        var service = new AIService(httpClient, "test-key", "https://api.example.com/v1", "gpt-test");
        var report = new ScanReport
        {
            Drive = "C:",
            TotalSize = 1_000_000_000,
            UsedSize = 900_000_000,
            FreeSize = 100_000_000,
            EstimatedSafeClean = 500_000_000,
            EstimatedConfirmClean = 250_000_000,
            LowRiskItems = Enumerable.Range(1, 500)
                .Select(index => new ScanNode
                {
                    Path = $@"C:\Users\Alice\AppData\Local\Temp\oversized-{index:0000}.tmp",
                    Name = $"oversized-{index:0000}.tmp",
                    Size = 1024L * index,
                    IsDirectory = false,
                    RiskLevel = RiskLevel.Low,
                    LastModified = new DateTime(2026, 1, 1).AddDays(index % 30)
                })
                .ToList(),
            MediumRiskItems = Enumerable.Range(1, 300)
                .Select(index => new ScanNode
                {
                    Path = $@"C:\Users\Alice\Downloads\maybe-{index:0000}.zip",
                    Name = $"maybe-{index:0000}.zip",
                    Size = 2048L * index,
                    IsDirectory = false,
                    RiskLevel = RiskLevel.Medium,
                    LastModified = new DateTime(2026, 2, 1).AddDays(index % 30)
                })
                .ToList()
        };

        await service.AnalyzeReportAsync(report);

        Assert.NotNull(capturedRequestBody);
        using var requestJson = JsonDocument.Parse(capturedRequestBody!);
        var userPrompt = requestJson.RootElement.GetProperty("messages")[1].GetProperty("content").GetString();
        Assert.NotNull(userPrompt);
        Assert.True(userPrompt!.Length < 12000, $"Prompt length was {userPrompt.Length}");
        Assert.Contains("候选清理项摘要", userPrompt);
        Assert.Contains("未展开项", userPrompt);
        Assert.DoesNotContain("报告 JSON", userPrompt);
        Assert.DoesNotContain("\"lowRiskItems\"", userPrompt);
        Assert.DoesNotContain("oversized-0001.tmp", userPrompt);
        Assert.DoesNotContain("maybe-0001.zip", userPrompt);
    }

    [Fact]
    public async Task AIService_TestConnectionAsync_UsesModelsEndpointAndConfigureValues()
    {
        HttpRequestMessage? capturedRequest = null;
        using var httpClient = new HttpClient(new StubHttpMessageHandler(request =>
        {
            capturedRequest = request;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));
        var service = new AIService(httpClient, "old-key", "https://old.example.com/v1", "old-model");
        service.Configure("https://api.example.com/v1/", "new-key", "new-model");

        var ok = await service.TestConnectionAsync();

        Assert.True(ok);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Get, capturedRequest!.Method);
        Assert.Equal("https://api.example.com/v1/models", capturedRequest.RequestUri!.ToString());
        Assert.Equal("new-key", capturedRequest.Headers.Authorization!.Parameter);
    }

    [Fact]
    public async Task AIService_AnalyzeReportAsync_ReturnsManualSuggestionWhenResponseCannotParse()
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "choices": [
                {
                  "message": {
                    "content": "not json"
                  }
                }
              ]
            }
            """, Encoding.UTF8, "application/json")
        }));
        var service = new AIService(httpClient, "test-key", "https://api.example.com/v1", "gpt-test");

        var suggestion = await service.AnalyzeReportAsync(new ScanReport());

        var item = Assert.Single(suggestion.Suggestions);
        Assert.Equal("解析失败", item.Path);
        Assert.Equal("manual", item.Action);
        Assert.Equal(0, item.Confidence);
    }

    [Fact]
    public void RiskClassifier_Classify_ProtectedRootsTakePrecedenceOverLowRiskPatterns()
    {
        var classifier = new RiskClassifier();

        Assert.Equal(RiskLevel.Forbidden, classifier.Classify(@"C:\Windows\Temp\cache.tmp", false));
        Assert.Equal(RiskLevel.High, classifier.Classify(@"C:\Program Files\App\Cache\data.tmp", false));
        Assert.Equal(RiskLevel.High, classifier.Classify(@"C:\Program Files (x86)\App\Cache\data.tmp", false));
    }

    [Fact]
    public async Task CleanExecutor_ExecuteAsync_RequiresAllowedLowRiskPathWhitelist()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "WinDiskCleanerTests", Guid.NewGuid().ToString("N"));
        var recycleRoot = Path.Combine(tempRoot, "recycle");
        var scannedLowRiskFile = Path.Combine(tempRoot, "Cache", "scanned.tmp");
        var aiInjectedFile = Path.Combine(tempRoot, "Cache", "injected.tmp");
        Directory.CreateDirectory(Path.GetDirectoryName(scannedLowRiskFile)!);
        await File.WriteAllTextAsync(scannedLowRiskFile, "safe");
        await File.WriteAllTextAsync(aiInjectedFile, "injected");
        var executor = new CleanExecutor(recycleRoot, Path.Combine(tempRoot, "clean.log"));

        var result = await executor.ExecuteAsync(new List<AISuggestionItem>
        {
            new() { Path = scannedLowRiskFile, Action = "delete", EstimatedSpace = 4 },
            new() { Path = aiInjectedFile, Action = "delete", EstimatedSpace = 8 }
        }, allowedLowRiskPaths: new[] { scannedLowRiskFile });

        Assert.Equal(1, result.Succeeded);
        Assert.Equal(1, result.Failed);
        Assert.False(File.Exists(scannedLowRiskFile));
        Assert.True(File.Exists(aiInjectedFile));
        Assert.Contains(result.Errors, error => error.Contains("不在当前扫描 Low 风险白名单"));
    }

    [Fact]
    public async Task CleanExecutor_ExecuteAsync_OnlyDeletesItemsWithDeleteActionAndReportsMissingPaths()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "WinDiskCleanerTests", Guid.NewGuid().ToString("N"));
        var recycleRoot = Path.Combine(tempRoot, "recycle");
        Directory.CreateDirectory(tempRoot);
        var deleteFile = Path.Combine(tempRoot, "Cache", "delete.tmp");
        var keepFile = Path.Combine(tempRoot, "Cache", "keep.tmp");
        var highRiskDeleteFile = Path.Combine(tempRoot, "Program Files", "danger.tmp");
        var deleteDirectory = Path.Combine(tempRoot, "Cache", "delete-dir");
        Directory.CreateDirectory(Path.GetDirectoryName(deleteFile)!);
        Directory.CreateDirectory(Path.GetDirectoryName(highRiskDeleteFile)!);
        Directory.CreateDirectory(deleteDirectory);
        await File.WriteAllTextAsync(deleteFile, "12345");
        await File.WriteAllTextAsync(keepFile, "67890");
        await File.WriteAllTextAsync(highRiskDeleteFile, "danger");
        await File.WriteAllTextAsync(Path.Combine(deleteDirectory, "nested.tmp"), "abc");
        var executor = new CleanExecutor(recycleRoot, Path.Combine(tempRoot, "clean.log"));

        var result = await executor.ExecuteAsync(new List<AISuggestionItem>
        {
            new() { Path = deleteFile, Action = "delete", EstimatedSpace = 5 },
            new() { Path = deleteDirectory, Action = "delete", EstimatedSpace = 3 },
            new() { Path = keepFile, Action = "keep", EstimatedSpace = 5 },
            new() { Path = highRiskDeleteFile, Action = "delete", EstimatedSpace = 6 },
            new() { Path = Path.Combine(tempRoot, "Cache", "missing.tmp"), Action = "delete", EstimatedSpace = 1 }
        });

        Assert.Equal(2, result.Succeeded);
        Assert.Equal(2, result.Failed);
        Assert.Equal(8, result.FreedBytes);
        Assert.False(File.Exists(deleteFile));
        Assert.False(Directory.Exists(deleteDirectory));
        Assert.True(File.Exists(keepFile));
        Assert.True(File.Exists(highRiskDeleteFile));
        Assert.True(Directory.EnumerateFileSystemEntries(recycleRoot).Any());
        Assert.Contains(result.Errors, error => error.Contains("非 Low 风险"));
        Assert.Contains(result.Errors, error => error.Contains("路径不存在"));
        Assert.True(File.Exists(Path.Combine(tempRoot, "clean.log")));
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
