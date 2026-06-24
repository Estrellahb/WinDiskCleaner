using System.Net;
using System.Text;
using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Services;

public class DuplicateReportExporter
{
    public string GenerateCsv(List<DuplicateGroup> groups)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Hash,Path,Size,LastModified,IsRecommended,Selected");
        foreach (var group in groups)
        {
            foreach (var file in group.Files)
            {
                sb.AppendLine(string.Join(',',
                    EscapeCsv(group.Hash),
                    EscapeCsv(file.Path),
                    file.Size,
                    file.LastModified.ToString("O"),
                    file.IsRecommended,
                    file.Selected));
            }
        }

        return sb.ToString();
    }

    public string GenerateHtml(List<DuplicateGroup> groups)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!doctype html><html><head><meta charset=\"utf-8\"><title>Duplicate Files Report</title>");
        sb.AppendLine("<style>body{font-family:Segoe UI,Arial,sans-serif;margin:24px}table{border-collapse:collapse;width:100%;margin-bottom:24px}td,th{border:1px solid #ddd;padding:6px;text-align:left}.recommended{color:#107c10;font-weight:600}</style>");
        sb.AppendLine("</head><body><h1>Duplicate Files Report</h1>");
        foreach (var group in groups)
        {
            sb.AppendLine($"<h2>{WebUtility.HtmlEncode(group.Hash)}</h2>");
            sb.AppendLine($"<p>Total: {group.TotalSize} bytes; Wasted: {group.WastedSpace} bytes</p>");
            sb.AppendLine("<table><thead><tr><th>Path</th><th>Size</th><th>Last Modified</th><th>Recommended</th><th>Selected</th></tr></thead><tbody>");
            foreach (var file in group.Files)
            {
                var cls = file.IsRecommended ? " class=\"recommended\"" : string.Empty;
                sb.AppendLine($"<tr{cls}><td>{WebUtility.HtmlEncode(file.Path)}</td><td>{file.Size}</td><td>{file.LastModified:O}</td><td>{file.IsRecommended}</td><td>{file.Selected}</td></tr>");
            }

            sb.AppendLine("</tbody></table>");
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    public async Task SaveCsvAsync(List<DuplicateGroup> groups, string path)
    {
        await File.WriteAllTextAsync(path, GenerateCsv(groups), Encoding.UTF8);
    }

    public async Task SaveHtmlAsync(List<DuplicateGroup> groups, string path)
    {
        await File.WriteAllTextAsync(path, GenerateHtml(groups), Encoding.UTF8);
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
