using WinDiskCleaner.Core.Models;
using WinDiskCleaner.Core.Services;

namespace WinDiskCleaner.Core.Tests;

public class SmartCleanAiInputTests
{
    [Fact]
    public void CandidateGrouper_GroupsLowRiskItems_AndKeepsLocalPathsOutOfSamples()
    {
        var tempPath = Path.Combine("C:", "Users", "Alice", "AppData", "Local", "Temp", "a.tmp");
        var report = new ScanReport
        {
            LowRiskItems =
            {
                new ScanNode { Path = tempPath, Name = "a.tmp", Size = 100, IsDirectory = false, RiskLevel = RiskLevel.Low },
                new ScanNode { Path = Path.Combine("C:", "Users", "Alice", "AppData", "Local", "Temp", "b.tmp"), Name = "b.tmp", Size = 200, IsDirectory = false, RiskLevel = RiskLevel.Low }
            }
        };

        var groups = SmartCleanCandidateGrouper.Group(report);

        var group = Assert.Single(groups);
        Assert.StartsWith("temp-", group.GroupId);
        Assert.Equal(2, group.ItemCount);
        Assert.Equal(300, group.TotalBytes);
        Assert.Contains(tempPath, group.LocalPaths);
        Assert.DoesNotContain("Alice", string.Join('\n', group.SamplePaths));
        Assert.Contains("%USERPROFILE%", string.Join('\n', group.SamplePaths));
    }

    [Fact]
    public void PromptBuilder_BuildsBoundedSummary_WithoutFullReportJsonOrFullPaths()
    {
        var report = new ScanReport
        {
            Drive = @"C:\",
            TotalSize = 1_000,
            UsedSize = 900,
            FreeSize = 100
        };
        for (var i = 0; i < 40; i++)
        {
            report.LowRiskItems.Add(new ScanNode
            {
                Path = $@"C:\Users\Alice\AppData\Local\Temp\item-{i}.tmp",
                Name = $"item-{i}.tmp",
                Size = 100 + i,
                IsDirectory = false,
                RiskLevel = RiskLevel.Low
            });
        }

        var groups = SmartCleanCandidateGrouper.Group(report);
        var prompt = SmartCleanAiPromptBuilder.Build(report, groups, maxCharacters: 1_200);

        Assert.True(prompt.Length <= 1_200);
        Assert.Contains("groupId", prompt);
        Assert.Contains("不要返回完整路径", prompt);
        Assert.DoesNotContain("\"topDirectories\"", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("\"lowRiskItems\"", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\Users\\Alice", prompt);
        Assert.Contains("%USERPROFILE%", prompt);
    }

    [Fact]
    public void AiService_MapsKnownGroupIdsToLocalLowRiskPaths_AndIgnoresUnknownGroups()
    {
        var report = new ScanReport
        {
            LowRiskItems =
            {
                new ScanNode { Path = @"C:\Users\Alice\AppData\Local\Temp\a.tmp", Name = "a.tmp", Size = 100, IsDirectory = false, RiskLevel = RiskLevel.Low }
            }
        };
        var groups = SmartCleanCandidateGrouper.Group(report);
        var knownGroupId = Assert.Single(groups).GroupId;
        var aiSuggestion = new AISuggestion
        {
            Suggestions =
            {
                new AISuggestionItem { GroupId = knownGroupId, Action = "delete", Reason = "safe", Confidence = 0.9 },
                new AISuggestionItem { GroupId = "unknown-group", Action = "delete", Path = @"C:\Windows\System32", Reason = "bad", Confidence = 1 }
            }
        };

        var mapped = AIService.MapGroupSuggestionsToLocalItems(aiSuggestion, groups);

        var item = Assert.Single(mapped.Items);
        Assert.Equal(knownGroupId, item.GroupId);
        Assert.Equal(@"C:\Users\Alice\AppData\Local\Temp\a.tmp", item.Path);
        Assert.Equal("delete", item.Action);
        Assert.Equal(100, item.EstimatedSpace);
    }

    [Fact]
    public async Task CleanExecutor_RejectsAiSuggestedPath_WhenItIsNotLowRiskAllowedPath()
    {
        var root = Path.Combine(Path.GetTempPath(), "WinDiskCleanerAiSafety", Guid.NewGuid().ToString("N"));
        var recycleRoot = Path.Combine(Path.GetTempPath(), "WinDiskCleanerAiSafetyRecycle", Guid.NewGuid().ToString("N"));
        var logPath = Path.Combine(Path.GetTempPath(), "WinDiskCleanerAiSafetyLogs", Guid.NewGuid().ToString("N"), "clean.log");
        var windowsDir = Path.Combine(root, "Windows");
        Directory.CreateDirectory(windowsDir);
        var protectedFile = Path.Combine(windowsDir, "kernel32.dll");
        await File.WriteAllTextAsync(protectedFile, "do not delete");

        try
        {
            var executor = new CleanExecutor(recycleRoot, logPath);
            var result = await executor.ExecuteAsync(
                new List<AISuggestionItem>
                {
                    new() { Path = protectedFile, Action = "delete", EstimatedSpace = 13 }
                },
                toRecycleBin: true,
                allowedLowRiskPaths: new[] { protectedFile });

            Assert.Equal(0, result.Succeeded);
            Assert.Equal(1, result.Failed);
            Assert.True(File.Exists(protectedFile));
            Assert.Contains(result.Errors, error => error.Contains("非 Low 风险项", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
            if (Directory.Exists(recycleRoot)) Directory.Delete(recycleRoot, true);
        }
    }
}
