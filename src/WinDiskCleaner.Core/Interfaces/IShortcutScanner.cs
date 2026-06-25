using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Interfaces;

public interface IShortcutScanner
{
    Task<List<ShortcutItem>> ScanShortcutsAsync(IProgress<ScanProgress>? progress, CancellationToken ct);
}

public interface IShortcutParser
{
    ShortcutTarget Parse(string shortcutPath);
}
