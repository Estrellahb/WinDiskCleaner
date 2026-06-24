namespace WinDiskCleaner.Core.Models;

public class AISuggestionItem
{
    public string Path { get; set; } = string.Empty;
    public string Action { get; set; } = "manual";
    public string Reason { get; set; } = string.Empty;
    public CleanRisk Risk { get; set; } = CleanRisk.High;
    public long EstimatedSpace { get; set; }
    public long SizeBytes
    {
        get => EstimatedSpace;
        set => EstimatedSpace = value;
    }

    public double Confidence { get; set; } = 1;
    public bool Selected { get; set; }
}
