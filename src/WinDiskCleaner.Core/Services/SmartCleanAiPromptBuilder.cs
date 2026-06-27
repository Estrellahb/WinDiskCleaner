using System.Text;
using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Services;

public static class SmartCleanAiPromptBuilder
{
    private const int DefaultMaxGroups = 30;
    private const int MaxSamplesPerGroup = 3;

    public static string Build(ScanReport report, IReadOnlyList<SmartCleanCandidateGroup> groups, int maxCharacters = 40_000)
    {
        maxCharacters = Math.Max(800, maxCharacters);
        var sb = new StringBuilder();
        sb.AppendLine("请判断以下 Windows 清理候选组是否适合删除。只根据给出的摘要判断，不要臆测不存在的文件。");
        sb.AppendLine("重要安全规则：");
        sb.AppendLine("- 只返回 JSON，不要返回 Markdown 或额外说明。");
        sb.AppendLine("- 只能引用候选组里的 groupId，不要返回完整路径，也不要创造新路径。");
        sb.AppendLine("- 本地规则为 high/forbidden 的内容不能建议自动删除。");
        sb.AppendLine("- delete 仅用于低风险缓存、临时文件、崩溃转储等可重新生成内容；用户文件请使用 manual。");
        sb.AppendLine();
        sb.AppendLine("输出格式：");
        sb.AppendLine("{\"suggestions\":[{\"groupId\":\"候选组ID\",\"action\":\"delete/manual/keep\",\"risk\":\"low/medium/high\",\"reason\":\"简短理由\",\"confidence\":0.0}]}");
        sb.AppendLine();
        sb.AppendLine("扫描摘要：");
        sb.AppendLine($"- 磁盘：{SmartCleanCandidateGrouper.DesensitizePath(report.Drive)}");
        sb.AppendLine($"- 总容量：{ReportGenerator.FormatSize(report.TotalSize)}");
        sb.AppendLine($"- 已用空间：{ReportGenerator.FormatSize(report.UsedSize)}");
        sb.AppendLine($"- 可用空间：{ReportGenerator.FormatSize(report.FreeSize)}");
        sb.AppendLine($"- 候选组数量：{groups.Count}");
        sb.AppendLine();
        sb.AppendLine("候选清理组：");

        var appended = 0;
        foreach (var group in groups.OrderByDescending(g => g.TotalBytes).Take(DefaultMaxGroups))
        {
            var block = BuildGroupBlock(group, appended + 1);
            if (sb.Length + block.Length + 80 > maxCharacters)
            {
                sb.AppendLine($"- 其余 {groups.Count - appended} 个候选组因长度限制省略；请仅基于已列出 groupId 判断。");
                break;
            }

            sb.Append(block);
            appended++;
        }

        var prompt = sb.ToString();
        if (prompt.Length <= maxCharacters)
        {
            return prompt;
        }

        return prompt[..maxCharacters];
    }

    private static string BuildGroupBlock(SmartCleanCandidateGroup group, int index)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{index}. {group.DisplayName}");
        sb.AppendLine($"- groupId: {group.GroupId}");
        sb.AppendLine($"- 类型: {group.Category}");
        sb.AppendLine($"- 本地风险: {group.RiskLevel}");
        sb.AppendLine($"- 本地建议: {group.ActionHint}");
        sb.AppendLine($"- 路径模式: {group.PathPattern}");
        sb.AppendLine($"- 数量: {group.ItemCount}");
        sb.AppendLine($"- 总大小: {ReportGenerator.FormatSize(group.TotalBytes)}");
        foreach (var sample in group.SamplePaths.Take(MaxSamplesPerGroup))
        {
            sb.AppendLine($"- 脱敏样本: {sample}");
        }

        sb.AppendLine();
        return sb.ToString();
    }
}
