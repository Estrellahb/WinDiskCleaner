using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Interfaces;

public interface IDuplicateFinder
{
    Task<List<DuplicateGroup>> FindDuplicatesAsync(
        List<string> scanPaths,
        IProgress<ScanProgress>? progress,
        CancellationToken ct);
}
