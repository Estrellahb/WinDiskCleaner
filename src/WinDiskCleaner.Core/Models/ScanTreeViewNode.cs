using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WinDiskCleaner.Core.Models;

public class ScanTreeViewNode : INotifyPropertyChanged
{
    private readonly ScanNode _sourceNode;
    private readonly long _totalSize;
    private bool _childrenLoaded;
    private bool _isExpanded;

    private ScanTreeViewNode()
    {
        _sourceNode = new ScanNode { Name = "加载中...", Path = string.Empty, IsDirectory = true };
        _totalSize = 1;
        Name = "加载中...";
        Path = string.Empty;
        RiskLevel = RiskLevel.Unknown;
        IsPlaceholder = true;
    }

    private ScanTreeViewNode(ScanNode sourceNode, long totalSize)
    {
        _sourceNode = sourceNode;
        _totalSize = totalSize <= 0 ? 1 : totalSize;
        Name = string.IsNullOrWhiteSpace(sourceNode.Name) ? System.IO.Path.GetFileName(sourceNode.Path.TrimEnd('\\', '/')) : sourceNode.Name;
        if (string.IsNullOrWhiteSpace(Name))
        {
            Name = sourceNode.Path;
        }

        Path = sourceNode.Path;
        Size = sourceNode.Size;
        FileCount = sourceNode.FileCount;
        RiskLevel = sourceNode.RiskLevel;
        IsDirectory = sourceNode.IsDirectory;
    }

    public string Name { get; }
    public string Path { get; }
    public long Size { get; }
    public long FileCount { get; }
    public RiskLevel RiskLevel { get; }
    public bool IsDirectory { get; }
    public bool IsPlaceholder { get; }
    public ObservableCollection<ScanTreeViewNode> Children { get; } = new();
    public bool HasUnrealizedChildren => !_childrenLoaded && _sourceNode.Children.Any(child => child.Size > 0 || child.IsDirectory);
    public bool HasChildren => Children.Count > 0 || HasUnrealizedChildren;
    public string SizeText => FormatSize(Size);
    public string FileCountText => IsDirectory ? $"{FileCount} 个文件" : "文件";
    public string PercentText => $"{Size * 100d / _totalSize:F1}%";
    public string KindLabel => IsDirectory ? "目录" : "文件";
    public string RiskColor => RiskLevel switch
    {
        RiskLevel.Low => "#107C10",
        RiskLevel.Medium => "#F9C74F",
        RiskLevel.High => "#FF8C00",
        RiskLevel.Forbidden => "#E81123",
        _ => "#8A8A8A"
    };
    public string DetailText => $"{Path}\n大小：{SizeText}\n文件数：{FileCount}\n风险：{RiskLevel}";

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            if (value)
            {
                EnsureChildrenLoaded();
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(HasChildren));
            OnPropertyChanged(nameof(HasUnrealizedChildren));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static ScanTreeViewNode FromScanNode(ScanNode node, long totalSize, int defaultExpandDepth)
    {
        var item = new ScanTreeViewNode(node, totalSize);
        if (defaultExpandDepth > 0)
        {
            item.LoadChildren(defaultExpandDepth - 1);
            item._isExpanded = item.Children.Count > 0;
        }
        else if (item.HasUnrealizedChildren)
        {
            item.Children.Add(new ScanTreeViewNode());
        }

        return item;
    }

    public void EnsureChildrenLoaded()
    {
        if (_childrenLoaded)
        {
            return;
        }

        Children.Clear();
        LoadChildren(defaultExpandDepth: 0);
        OnPropertyChanged(nameof(Children));
        OnPropertyChanged(nameof(HasChildren));
        OnPropertyChanged(nameof(HasUnrealizedChildren));
    }

    private void LoadChildren(int defaultExpandDepth)
    {
        if (_childrenLoaded)
        {
            return;
        }

        var childTotal = _sourceNode.Children.Sum(child => child.Size);
        if (childTotal <= 0)
        {
            childTotal = Size > 0 ? Size : 1;
        }

        foreach (var child in _sourceNode.Children
            .Where(child => child.Size > 0 || child.IsDirectory)
            .OrderByDescending(child => child.Size)
            .ThenBy(child => child.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            Children.Add(FromScanNode(child, childTotal, defaultExpandDepth));
        }

        _childrenLoaded = true;
    }

    private static string FormatSize(long bytes)
    {
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        double size = bytes;
        var index = 0;
        while (size >= 1024 && index < suffixes.Length - 1)
        {
            size /= 1024;
            index++;
        }

        return $"{size:F1} {suffixes[index]}";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
