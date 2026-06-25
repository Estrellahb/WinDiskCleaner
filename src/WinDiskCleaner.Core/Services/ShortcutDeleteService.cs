using WinDiskCleaner.Core.Interfaces;
using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Services;

public class ShortcutDeleteService : IShortcutDeleteService
{
    private readonly List<string> _allowedRoots;

    public ShortcutDeleteService()
        : this(GetDefaultAllowedRoots())
    {
    }

    public ShortcutDeleteService(IEnumerable<string> allowedRoots)
    {
        _allowedRoots = allowedRoots
            .Where(root => !string.IsNullOrWhiteSpace(root))
            .Select(root => Path.GetFullPath(root))
            .Select(root => root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public Task<ShortcutDeleteResult> DeleteSelectedInvalidShortcutsAsync(IEnumerable<ShortcutItem> items, CancellationToken ct)
    {
        var result = new ShortcutDeleteResult();

        foreach (var item in items.Where(item => item.Selected && !item.TargetExists))
        {
            ct.ThrowIfCancellationRequested();

            if (!string.Equals(Path.GetExtension(item.ShortcutPath), ".lnk", StringComparison.OrdinalIgnoreCase))
            {
                result.Failures.Add(new ShortcutDeleteFailure { Path = item.ShortcutPath, Reason = "Only .lnk shortcut files can be deleted." });
                continue;
            }

            if (!IsUnderAllowedRoot(item.ShortcutPath))
            {
                result.Failures.Add(new ShortcutDeleteFailure { Path = item.ShortcutPath, Reason = "Shortcut path is outside allowed scan roots." });
                continue;
            }

            try
            {
                ct.ThrowIfCancellationRequested();
                if (!File.Exists(item.ShortcutPath))
                {
                    result.Failures.Add(new ShortcutDeleteFailure { Path = item.ShortcutPath, Reason = "Shortcut file does not exist." });
                    continue;
                }

                File.Delete(item.ShortcutPath);
                result.DeletedPaths.Add(item.ShortcutPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                result.Failures.Add(new ShortcutDeleteFailure { Path = item.ShortcutPath, Reason = ex.Message });
            }
        }

        return Task.FromResult(result);
    }

    private bool IsUnderAllowedRoot(string shortcutPath)
    {
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(shortcutPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }

        return _allowedRoots.Any(root => fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> GetDefaultAllowedRoots()
    {
        return new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu),
            Environment.GetFolderPath(Environment.SpecialFolder.Startup)
        };
    }
}
