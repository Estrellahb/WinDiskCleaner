using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Interfaces;

public interface IShortcutReportExporter
{
    string ExportCsv(IEnumerable<ShortcutItem> items);
    string ExportHtml(IEnumerable<ShortcutItem> items);
}
