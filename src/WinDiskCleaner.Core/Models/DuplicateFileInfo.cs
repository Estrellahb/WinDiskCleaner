namespace WinDiskCleaner.Core.Models;

public class DuplicateFileInfo
{
    public string Path { get; set; } = string.Empty;
    public long Size { get; set; }
    public DateTime LastModified { get; set; }
    public bool IsRecommended { get; set; }
    public bool Selected { get; set; }
}
