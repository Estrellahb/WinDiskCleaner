using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Services;

public class ReportGenerator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public string GenerateJson(ScanReport report)
    {
        return JsonSerializer.Serialize(report, JsonOptions);
    }

    public async Task SaveToFileAsync(ScanReport report, string filePath)
    {
        var json = GenerateJson(report);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, json);
    }

    public string GenerateHtmlReport(ScanReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
        sb.AppendLine("<title>磁盘扫描报告</title>");
        sb.AppendLine("<style>body{font-family:微软雅黑;margin:20px}");
        sb.AppendLine(".low{color:green}.medium{color:orange}.high{color:red}.forbidden{color:darkred}");
        sb.AppendLine("table{border-collapse:collapse;width:100%}th,td{border:1px solid #ddd;padding:8px;text-align:left}");
        sb.AppendLine("th{background:#f5f5f5}</style></head><body>");
        sb.AppendLine($"<h1>磁盘扫描报告 - {EscapeHtml(report.Drive)}</h1>");
        sb.AppendLine($"<p>扫描时间：{report.ScanTime:yyyy-MM-dd HH:mm:ss}</p>");
        sb.AppendLine($"<p>总容量：{FormatSize(report.TotalSize)} | 已用：{FormatSize(report.UsedSize)} | 剩余：{FormatSize(report.FreeSize)}</p>");
        sb.AppendLine("<h2>可安全清理</h2>");
        sb.AppendLine($"<p class='low'>{FormatSize(report.EstimatedSafeClean)}</p>");
        sb.AppendLine("<h2>需确认清理</h2>");
        sb.AppendLine($"<p class='medium'>{FormatSize(report.EstimatedConfirmClean)}</p>");
        sb.AppendLine("<h2>Top 大目录</h2><table><tr><th>路径</th><th>大小</th><th>风险</th></tr>");
        foreach (var dir in report.TopDirectories)
        {
            sb.AppendLine($"<tr><td>{EscapeHtml(dir.Path)}</td><td>{FormatSize(dir.Size)}</td><td class='{dir.RiskLevel.ToString().ToLowerInvariant()}'>{dir.RiskLevel}</td></tr>");
        }

        sb.AppendLine("</table>");
        sb.AppendLine("<h2>Top 大文件</h2><table><tr><th>路径</th><th>大小</th><th>风险</th></tr>");
        foreach (var file in report.TopFiles)
        {
            sb.AppendLine($"<tr><td>{EscapeHtml(file.Path)}</td><td>{FormatSize(file.Size)}</td><td class='{file.RiskLevel.ToString().ToLowerInvariant()}'>{file.RiskLevel}</td></tr>");
        }

        sb.AppendLine("</table>");
        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    public string GenerateMarkdownReport(ScanReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 磁盘扫描报告 - {report.Drive}");
        sb.AppendLine($"扫描时间：{report.ScanTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine("| 项目 | 大小 |");
        sb.AppendLine("|------|------|");
        sb.AppendLine($"| 总容量 | {FormatSize(report.TotalSize)} |");
        sb.AppendLine($"| 已用 | {FormatSize(report.UsedSize)} |");
        sb.AppendLine($"| 剩余 | {FormatSize(report.FreeSize)} |");
        sb.AppendLine();
        sb.AppendLine($"## 可安全清理：{FormatSize(report.EstimatedSafeClean)}");
        sb.AppendLine($"## 需确认清理：{FormatSize(report.EstimatedConfirmClean)}");
        sb.AppendLine();
        sb.AppendLine("## Top 大目录");
        sb.AppendLine("| 路径 | 大小 | 风险 |");
        sb.AppendLine("|------|------|------|");
        foreach (var dir in report.TopDirectories)
        {
            sb.AppendLine($"| {dir.Path} | {FormatSize(dir.Size)} | {dir.RiskLevel} |");
        }

        sb.AppendLine();
        sb.AppendLine("## Top 大文件");
        sb.AppendLine("| 路径 | 大小 | 风险 |");
        sb.AppendLine("|------|------|------|");
        foreach (var file in report.TopFiles)
        {
            sb.AppendLine($"| {file.Path} | {FormatSize(file.Size)} | {file.RiskLevel} |");
        }

        return sb.ToString();
    }

    public static string FormatSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        var unitIndex = 0;
        double size = bytes;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:F2} {units[unitIndex]}";
    }

    private static string EscapeHtml(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&#39;");
    }
}
