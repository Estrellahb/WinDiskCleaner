using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Interfaces;

public interface IAIService
{
    Task<AISuggestion> AnalyzeReportAsync(ScanReport report);
}
