namespace WinDiskCleaner.Core.Models;

public class ShortcutItem
{
    public string Name { get; set; } = string.Empty;
    public string ShortcutPath { get; set; } = string.Empty;
    public string TargetPath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public bool TargetExists { get; set; }
    public string SourceDirectory { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public DateTime LastModified { get; set; }
    public bool Selected { get; set; }
}
