using System.Globalization;
using System.Net;
using System.Text;
using WinDiskCleaner.Core.Interfaces;
using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Services;

public class ShortcutReportExporter : IShortcutReportExporter
{
    public string ExportCsv(IEnumerable<ShortcutItem> items)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Name,ShortcutPath,TargetPath,Arguments,TargetExists,SourceDirectory,FileSize,LastModified");

        foreach (var item in items)
        {
            builder.AppendLine(string.Join(",", new[]
            {
                Csv(item.Name),
                Csv(item.ShortcutPath),
                Csv(item.TargetPath),
                Csv(item.Arguments),
                Csv(item.TargetExists ? "有效" : "失效"),
                Csv(item.SourceDirectory),
                Csv(item.FileSize.ToString(CultureInfo.InvariantCulture)),
                Csv(item.LastModified.ToString("O", CultureInfo.InvariantCulture))
            }));
        }

        return builder.ToString();
    }

    public string ExportHtml(IEnumerable<ShortcutItem> items)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><title>Shortcut Report</title></head><body>");
        builder.AppendLine("<h1>快捷方式检测报告</h1>");
        builder.AppendLine("<table><thead><tr><th>名称</th><th>路径</th><th>目标路径</th><th>参数</th><th>状态</th><th>来源目录</th><th>大小</th><th>修改时间</th></tr></thead><tbody>");

        foreach (var item in items)
        {
            builder.Append("<tr>");
            builder.Append(Cell(item.Name));
            builder.Append(Cell(item.ShortcutPath));
            builder.Append(Cell(item.TargetPath));
            builder.Append(Cell(item.Arguments));
            builder.Append(Cell(item.TargetExists ? "有效" : "失效"));
            builder.Append(Cell(item.SourceDirectory));
            builder.Append(Cell(item.FileSize.ToString(CultureInfo.InvariantCulture)));
            builder.Append(Cell(item.LastModified.ToString("O", CultureInfo.InvariantCulture)));
            builder.AppendLine("</tr>");
        }

        builder.AppendLine("</tbody></table></body></html>");
        return builder.ToString();
    }

    private static string Cell(string value) => $"<td>{WebUtility.HtmlEncode(value)}</td>";

    private static string Csv(string value)
    {
        var sanitized = value;
        var firstEffective = sanitized.FirstOrDefault(ch => !char.IsWhiteSpace(ch) && !char.IsControl(ch));
        if (firstEffective != default && "=+-@".Contains(firstEffective))
        {
            sanitized = "'" + sanitized;
        }

        return "\"" + sanitized.Replace("\"", "\"\"") + "\"";
    }
}
