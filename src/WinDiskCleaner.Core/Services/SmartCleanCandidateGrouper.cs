using System.Security.Cryptography;
using System.Text;
using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Services;

public static class SmartCleanCandidateGrouper
{
    private const int MaxSamplesPerGroup = 3;

    public static List<SmartCleanCandidateGroup> Group(ScanReport report)
    {
        var nodes = report.LowRiskItems
            .Concat(report.MediumRiskItems)
            .Where(node => !string.IsNullOrWhiteSpace(node.Path))
            .ToList();

        return nodes
            .GroupBy(node => BuildGroupKey(node))
            .Select(group => BuildGroup(group.Key, group.ToList()))
            .OrderByDescending(group => group.TotalBytes)
            .ThenBy(group => group.GroupId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static SmartCleanCandidateGroup BuildGroup(string key, List<ScanNode> nodes)
    {
        var first = nodes[0];
        var category = DetectCategory(first.Path);
        var risk = nodes.Select(node => node.RiskLevel).DefaultIfEmpty(RiskLevel.Unknown).Max();
        var prefix = category.ToLowerInvariant();
        var hash = ShortHash(key);
        var samples = nodes
            .OrderByDescending(node => node.Size)
            .Take(MaxSamplesPerGroup)
            .Select(node => DesensitizePath(node.Path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SmartCleanCandidateGroup
        {
            GroupId = $"{prefix}-{hash}",
            DisplayName = BuildDisplayName(category, risk),
            Category = category,
            RiskLevel = risk,
            ActionHint = risk == RiskLevel.Low ? "delete" : "manual",
            TotalBytes = nodes.Sum(node => Math.Max(0, node.Size)),
            ItemCount = nodes.Count,
            PathPattern = BuildPathPattern(first.Path),
            SamplePaths = samples,
            LocalPaths = nodes.Select(node => node.Path).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            Nodes = nodes
        };
    }

    private static string BuildGroupKey(ScanNode node)
    {
        return $"{DetectCategory(node.Path)}|{node.RiskLevel}|{BuildPathPattern(node.Path)}";
    }

    private static string BuildDisplayName(string category, RiskLevel risk)
    {
        return category switch
        {
            "temp" => "临时文件",
            "cache" => "缓存文件",
            "dump" => "崩溃转储",
            "download" => "下载目录项目",
            "desktop" => "桌面项目",
            _ => risk == RiskLevel.Low ? "低风险清理项" : "需要确认的清理项"
        };
    }

    private static string DetectCategory(string path)
    {
        var normalized = path.Replace('\\', '/');
        if (normalized.Contains("/Temp/", StringComparison.OrdinalIgnoreCase) || normalized.EndsWith("/Temp", StringComparison.OrdinalIgnoreCase)) return "temp";
        if (normalized.Contains("/TMP/", StringComparison.OrdinalIgnoreCase) || normalized.EndsWith("/TMP", StringComparison.OrdinalIgnoreCase)) return "temp";
        if (normalized.Contains("/Cache/", StringComparison.OrdinalIgnoreCase) || normalized.EndsWith("/Cache", StringComparison.OrdinalIgnoreCase)) return "cache";
        if (normalized.EndsWith(".dmp", StringComparison.OrdinalIgnoreCase)) return "dump";
        if (normalized.Contains("/Downloads/", StringComparison.OrdinalIgnoreCase) || normalized.EndsWith("/Downloads", StringComparison.OrdinalIgnoreCase)) return "download";
        if (normalized.Contains("/Desktop/", StringComparison.OrdinalIgnoreCase) || normalized.EndsWith("/Desktop", StringComparison.OrdinalIgnoreCase)) return "desktop";
        return "unknown";
    }

    public static string BuildPathPattern(string path)
    {
        var desensitized = DesensitizePath(path).Replace('\\', '/');
        var directory = Path.GetDirectoryName(desensitized)?.Replace('\\', '/') ?? desensitized;
        return string.IsNullOrWhiteSpace(directory) ? desensitized : $"{directory}/*";
    }

    public static string DesensitizePath(string path)
    {
        var result = path;
        var normalized = result.Replace('\\', '/');
        var userMarker = "/Users/";
        var userIndex = normalized.IndexOf(userMarker, StringComparison.OrdinalIgnoreCase);
        if (userIndex >= 0)
        {
            var userStart = userIndex + userMarker.Length;
            var userEnd = normalized.IndexOf('/', userStart);
            if (userEnd > userStart)
            {
                var prefixLength = userStart;
                var suffix = normalized[userEnd..];
                var prefix = normalized[..prefixLength];
                normalized = prefix + "%USERPROFILE%" + suffix;
                var driveEnd = prefix.IndexOf(userMarker, StringComparison.OrdinalIgnoreCase);
                normalized = driveEnd >= 0 ? normalized[(driveEnd + 1)..] : normalized;
            }
        }

        result = normalized.Replace('/', Path.DirectorySeparatorChar);
        return result;
    }

    private static string ShortHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.ToLowerInvariant()));
        return Convert.ToHexString(bytes, 0, 4).ToLowerInvariant();
    }
}
