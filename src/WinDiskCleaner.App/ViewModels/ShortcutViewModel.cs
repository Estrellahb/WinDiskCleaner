using System.Collections.ObjectModel;
using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.App.ViewModels;

public class ShortcutViewModel
{
    private List<ShortcutItem> _allShortcuts = new();
    private string _searchQuery = string.Empty;

    public ObservableCollection<ShortcutItem> Shortcuts { get; } = new();
    public ObservableCollection<string> OperationLogs { get; } = new();

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            _searchQuery = value ?? string.Empty;
            ApplyFilter();
        }
    }

    public int TotalCount => _allShortcuts.Count;
    public int ValidCount => _allShortcuts.Count(item => item.TargetExists);
    public int InvalidCount => _allShortcuts.Count(item => !item.TargetExists);
    public string StatisticsText => $"总计 {TotalCount} / 有效 {ValidCount} / 失效 {InvalidCount}";

    public void LoadShortcuts(IEnumerable<ShortcutItem> items)
    {
        _allShortcuts = items.ToList();
        ApplyFilter();
    }

    public List<ShortcutItem> GetAllShortcuts() => _allShortcuts;

    public List<ShortcutItem> GetSelectedInvalidShortcuts()
    {
        return _allShortcuts.Where(item => item.Selected && !item.TargetExists).ToList();
    }

    public void RemoveDeleted(IEnumerable<string> deletedPaths)
    {
        var deleted = deletedPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _allShortcuts = _allShortcuts.Where(item => !deleted.Contains(item.ShortcutPath)).ToList();
        ApplyFilter();
    }

    public void SelectInvalid()
    {
        foreach (var item in _allShortcuts)
        {
            item.Selected = !item.TargetExists;
        }

        ApplyFilter();
    }

    public void InvertInvalidSelection()
    {
        foreach (var item in _allShortcuts.Where(item => !item.TargetExists))
        {
            item.Selected = !item.Selected;
        }

        ApplyFilter();
    }

    public void SortByPath()
    {
        _allShortcuts = _allShortcuts.OrderBy(item => item.ShortcutPath, StringComparer.OrdinalIgnoreCase).ToList();
        ApplyFilter();
    }

    public void SortByStatus()
    {
        _allShortcuts = _allShortcuts.OrderBy(item => item.TargetExists).ThenBy(item => item.ShortcutPath, StringComparer.OrdinalIgnoreCase).ToList();
        ApplyFilter();
    }

    public void AddLog(string message)
    {
        OperationLogs.Insert(0, $"{DateTime.Now:HH:mm:ss} {message}");
        while (OperationLogs.Count > 100)
        {
            OperationLogs.RemoveAt(OperationLogs.Count - 1);
        }
    }

    private void ApplyFilter()
    {
        var query = _searchQuery.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _allShortcuts
            : _allShortcuts.Where(item =>
                Contains(item.Name, query) ||
                Contains(item.ShortcutPath, query) ||
                Contains(item.TargetPath, query) ||
                Contains(item.SourceDirectory, query)).ToList();

        Shortcuts.Clear();
        foreach (var item in filtered)
        {
            Shortcuts.Add(item);
        }
    }

    private static bool Contains(string value, string query)
    {
        return value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}
