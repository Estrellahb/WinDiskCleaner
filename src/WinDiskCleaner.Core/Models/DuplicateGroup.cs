namespace WinDiskCleaner.Core.Models;

public class DuplicateGroup
{
    public string Hash { get; set; } = string.Empty;
    public long TotalSize { get; set; }
    public long WastedSpace { get; set; }
    public List<DuplicateFileInfo> Files { get; set; } = new();
}
