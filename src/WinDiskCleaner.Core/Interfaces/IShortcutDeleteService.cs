using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Interfaces;

public interface IShortcutDeleteService
{
    Task<ShortcutDeleteResult> DeleteSelectedInvalidShortcutsAsync(IEnumerable<ShortcutItem> items, CancellationToken ct);
}
