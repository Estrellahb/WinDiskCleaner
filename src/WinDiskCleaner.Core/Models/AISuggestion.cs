namespace WinDiskCleaner.Core.Models;

public class AISuggestion
{
    public string Summary { get; set; } = string.Empty;
    public DateTime AnalyzedAt { get; set; } = DateTime.Now;
    public List<AISuggestionItem> Suggestions { get; set; } = new();

    public List<AISuggestionItem> Items
    {
        get => Suggestions;
        set => Suggestions = value;
    }
}
