using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Interfaces;

public interface ICleanExecutor
{
    Task<CleanResult> ExecuteAsync(List<AISuggestionItem> items, bool toRecycleBin = true);
}

public class CleanResult
{
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public long FreedBytes { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> SucceededPaths { get; set; } = new();
}
