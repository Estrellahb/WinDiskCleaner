namespace WinDiskCleaner.Core.Models;

public sealed record ShortcutTarget(string TargetPath, string Arguments);

public sealed record ShortcutScanRoot(string Path, string SourceDirectory);
