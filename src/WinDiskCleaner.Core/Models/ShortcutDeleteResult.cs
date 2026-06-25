namespace WinDiskCleaner.Core.Models;

public class ShortcutDeleteResult
{
    public List<string> DeletedPaths { get; set; } = new();
    public List<ShortcutDeleteFailure> Failures { get; set; } = new();
}

public class ShortcutDeleteFailure
{
    public string Path { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
}
