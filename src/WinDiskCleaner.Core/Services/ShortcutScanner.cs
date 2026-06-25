using WinDiskCleaner.Core.Interfaces;
using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Services;

public class ShortcutScanner : IShortcutScanner
{
    private readonly IReadOnlyList<ShortcutScanRoot> _roots;
    private readonly IShortcutParser _parser;
    private readonly Func<string, IEnumerable<string>> _enumerateFileSystemEntries;

    public ShortcutScanner()
        : this(GetDefaultRoots(), new WindowsShortcutParser())
    {
    }

    public ShortcutScanner(IEnumerable<ShortcutScanRoot> roots, IShortcutParser parser)
        : this(roots, parser, Directory.EnumerateFileSystemEntries)
    {
    }

    public ShortcutScanner(
        IEnumerable<ShortcutScanRoot> roots,
        IShortcutParser parser,
        Func<string, IEnumerable<string>> enumerateFileSystemEntries)
    {
        _roots = roots.ToList();
        _parser = parser;
        _enumerateFileSystemEntries = enumerateFileSystemEntries;
    }

    public Task<List<ShortcutItem>> ScanShortcutsAsync(IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var items = new List<ShortcutItem>();
            long filesScanned = 0;
            long directoriesScanned = 0;

            foreach (var root in _roots.Where(root => Directory.Exists(root.Path)))
            {
                ScanDirectory(root.Path, root.SourceDirectory, items, progress, ct, ref filesScanned, ref directoriesScanned);
            }

            return items.OrderBy(item => item.ShortcutPath, StringComparer.OrdinalIgnoreCase).ToList();
        }, ct);
    }

    private void ScanDirectory(
        string directory,
        string sourceDirectory,
        List<ShortcutItem> items,
        IProgress<ScanProgress>? progress,
        CancellationToken ct,
        ref long filesScanned,
        ref long directoriesScanned)
    {
        ct.ThrowIfCancellationRequested();
        directoriesScanned++;
        progress?.Report(new ScanProgress { CurrentPath = directory, DirectoriesScanned = directoriesScanned, FilesScanned = filesScanned });

        IEnumerable<string> entries;
        try
        {
            entries = _enumerateFileSystemEntries(directory).ToList();
        }
        catch (UnauthorizedAccessException)
        {
            return;
        }
        catch (IOException)
        {
            return;
        }

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

            if (Directory.Exists(entry))
            {
                ScanDirectory(entry, sourceDirectory, items, progress, ct, ref filesScanned, ref directoriesScanned);
                continue;
            }

            filesScanned++;
            if (!string.Equals(Path.GetExtension(entry), ".lnk", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ShortcutTarget target;
            try
            {
                target = _parser.Parse(entry);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                target = new ShortcutTarget(string.Empty, string.Empty);
            }

            var info = new FileInfo(entry);
            items.Add(new ShortcutItem
            {
                Name = Path.GetFileName(entry),
                ShortcutPath = entry,
                TargetPath = target.TargetPath,
                Arguments = target.Arguments,
                TargetExists = !string.IsNullOrWhiteSpace(target.TargetPath) && (File.Exists(target.TargetPath) || Directory.Exists(target.TargetPath)),
                SourceDirectory = sourceDirectory,
                FileSize = info.Exists ? info.Length : 0,
                LastModified = info.Exists ? info.LastWriteTime : default
            });
        }
    }

    private static IReadOnlyList<ShortcutScanRoot> GetDefaultRoots()
    {
        var roots = new List<ShortcutScanRoot>();
        Add(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "当前用户桌面");
        Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "公共桌面");
        Add(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "当前用户开始菜单");
        Add(Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu), "公共开始菜单");
        Add(Environment.GetFolderPath(Environment.SpecialFolder.Startup), "启动项");
        return roots;

        void Add(string path, string source)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                roots.Add(new ShortcutScanRoot(path, source));
            }
        }
    }

    private sealed class WindowsShortcutParser : IShortcutParser
    {
        public ShortcutTarget Parse(string shortcutPath)
        {
            if (!OperatingSystem.IsWindows())
            {
                var lines = File.ReadAllLines(shortcutPath);
                return new ShortcutTarget(lines.FirstOrDefault() ?? string.Empty, lines.Skip(1).FirstOrDefault() ?? string.Empty);
            }

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType is null)
            {
                throw new InvalidOperationException("WScript.Shell is not available.");
            }

            dynamic? shell = null;
            dynamic? shortcut = null;
            try
            {
                shell = Activator.CreateInstance(shellType) ?? throw new InvalidOperationException("WScript.Shell could not be created.");
                shortcut = shell.CreateShortcut(shortcutPath);
                return new ShortcutTarget((string)(shortcut.TargetPath ?? string.Empty), (string)(shortcut.Arguments ?? string.Empty));
            }
            finally
            {
                if (shortcut is not null)
                {
                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shortcut);
                }

                if (shell is not null)
                {
                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);
                }
            }
        }
    }
}
