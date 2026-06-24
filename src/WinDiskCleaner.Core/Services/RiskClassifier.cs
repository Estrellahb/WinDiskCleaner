using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Services;

public class RiskClassifier
{
    private readonly List<RiskRule> _rules;

    public RiskClassifier()
    {
        _rules = LoadBuiltInRules();
    }

    public RiskLevel Classify(string path, bool isDirectory)
    {
        foreach (var rule in _rules)
        {
            foreach (var pattern in rule.PathPatterns)
            {
                if (path.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return rule.RiskLevel;
                }
            }
        }

        if (isDirectory)
        {
            return RiskLevel.Medium;
        }

        return RiskLevel.Unknown;
    }

    private static List<RiskRule> LoadBuiltInRules()
    {
        return new List<RiskRule>
        {
            new()
            {
                Id = "temp",
                Name = "临时文件",
                PathPatterns = new() { @"\Temp", @"\TMP" },
                RiskLevel = RiskLevel.Low,
                Category = "temp",
                Action = "delete",
                Description = "临时文件可安全清理"
            },
            new()
            {
                Id = "cache",
                Name = "缓存",
                PathPatterns = new() { "Cache", "cache" },
                RiskLevel = RiskLevel.Low,
                Category = "cache",
                Action = "delete",
                Description = "缓存文件可清理"
            },
            new()
            {
                Id = "dmp",
                Name = "崩溃转储",
                PathPatterns = new() { ".dmp" },
                RiskLevel = RiskLevel.Low,
                Category = "dump",
                Action = "delete",
                Description = "崩溃转储可删除"
            },
            new()
            {
                Id = "downloads",
                Name = "下载目录",
                PathPatterns = new() { @"\Downloads", @"/Downloads" },
                RiskLevel = RiskLevel.Medium,
                Category = "user",
                Action = "confirm",
                Description = "用户下载文件需确认"
            },
            new()
            {
                Id = "desktop",
                Name = "桌面",
                PathPatterns = new() { @"\Desktop", @"/Desktop" },
                RiskLevel = RiskLevel.Medium,
                Category = "user",
                Action = "confirm",
                Description = "桌面文件需人工确认"
            },
            new()
            {
                Id = "programs",
                Name = "程序目录",
                PathPatterns = new() { @"\Program Files", @"/Program Files", @"\Program Files (x86)", @"/Program Files (x86)" },
                RiskLevel = RiskLevel.High,
                Category = "system",
                Action = "forbid",
                Description = "程序目录属于高风险清理范围"
            },
            new()
            {
                Id = "windows",
                Name = "Windows 系统目录",
                PathPatterns = new() { @"\Windows", @"/Windows" },
                RiskLevel = RiskLevel.Forbidden,
                Category = "system",
                Action = "forbid",
                Description = "系统目录禁止清理"
            }
        };
    }
}
