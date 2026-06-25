using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WinDiskCleaner.Core.Models;
using WinDiskCleaner.Core.Services;

namespace WinDiskCleaner.App.ViewModels;

public class RegistryViewModel : INotifyPropertyChanged
{
    private readonly List<InstalledSoftware> _allSoftware = new();
    private string _searchQuery = string.Empty;

    public ObservableCollection<InstalledSoftwareRow> Software { get; } = new();
    public ObservableCollection<RegistryBackup> BackupHistory { get; } = new();
    public ObservableCollection<string> OperationLogs { get; } = new();

    public List<string> BackupBranches { get; } = RegistryScanner.UninstallBranches.ToList();

    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery == value)
            {
                return;
            }

            _searchQuery = value;
            OnPropertyChanged();
            ApplyFilterAndSort();
        }
    }

    public string SelectedBackupBranch { get; set; }

    public RegistryViewModel()
    {
        SelectedBackupBranch = BackupBranches.First();
    }

    public void LoadSoftware(IEnumerable<InstalledSoftware> software)
    {
        _allSoftware.Clear();
        _allSoftware.AddRange(software);
        ApplyFilterAndSort();
    }

    public void SortByName() => ApplyFilterAndSort(item => item.DisplayName);

    public void SortByPublisher() => ApplyFilterAndSort(item => item.Publisher);

    public void SortBySize() => ApplyFilterAndSort(item => item.EstimatedSize, descending: true);

    public void LoadBackupHistory(IEnumerable<RegistryBackup> backups)
    {
        BackupHistory.Clear();
        foreach (var backup in backups)
        {
            BackupHistory.Add(backup);
        }
    }

    public void AddLog(string message)
    {
        OperationLogs.Insert(0, $"{DateTime.Now:HH:mm:ss} {message}");
    }

    private void ApplyFilterAndSort<TKey>(Func<InstalledSoftware, TKey>? sortKey = null, bool descending = false)
    {
        var query = _searchQuery.Trim();
        IEnumerable<InstalledSoftware> filtered = _allSoftware;
        if (!string.IsNullOrWhiteSpace(query))
        {
            filtered = filtered.Where(item => Contains(item.DisplayName, query)
                || Contains(item.Publisher, query)
                || Contains(item.DisplayVersion, query)
                || Contains(item.InstallLocation, query));
        }

        if (sortKey is not null)
        {
            filtered = descending ? filtered.OrderByDescending(sortKey) : filtered.OrderBy(sortKey);
        }
        else
        {
            filtered = filtered.OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase);
        }

        Software.Clear();
        foreach (var item in filtered)
        {
            Software.Add(new InstalledSoftwareRow(item));
        }
    }

    private void ApplyFilterAndSort()
    {
        ApplyFilterAndSort<string>(item => item.DisplayName);
    }

    private static bool Contains(string value, string query)
    {
        return value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public class InstalledSoftwareRow
{
    private readonly InstalledSoftware _source;

    public InstalledSoftwareRow(InstalledSoftware source)
    {
        _source = source;
    }

    public string DisplayName => string.IsNullOrWhiteSpace(_source.DisplayName) ? "（无名称）" : _source.DisplayName;
    public string DisplayVersion => _source.DisplayVersion;
    public string Publisher => _source.Publisher;
    public string InstallLocation => _source.InstallLocation;
    public string EstimatedSizeText => ReportGenerator.FormatSize(_source.EstimatedSize);
    public string RegistryPath => _source.RegistryPath;
    public bool IsInvalid => !_source.IsValid;
    public bool IsOrphan => _source.IsOrphan;
    public string StatusText => !_source.IsValid ? "无效项" : _source.IsOrphan ? "残留项" : "正常";
}
