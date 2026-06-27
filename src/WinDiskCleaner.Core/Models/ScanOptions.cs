namespace WinDiskCleaner.Core.Models;

public class ScanOptions
{
    public static readonly IReadOnlyList<string> DefaultSkippedDirectoryNames = new[]
    {
        "System Volume Information",
        "$Recycle.Bin",
        "Windows",
        "Program Files",
        "Program Files (x86)",
        "ProgramData"
    };

    public List<string> SkippedDirectoryNames { get; set; } = DefaultSkippedDirectoryNames.ToList();

    public static ScanOptions Default => new();
}
