using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using WinDiskCleaner.Core.Models;
using WinDiskCleaner.Core.Services;

namespace WinDiskCleaner.App.Controls;

public partial class TreemapControl : Canvas
{
    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(List<ScanNode>), typeof(TreemapControl),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnItemsSourceChanged));

    public List<ScanNode>? ItemsSource
    {
        get => (List<ScanNode>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    private List<TreemapRect> _rects = new();
    private ScanNode? _currentRoot;

    public event Action<ScanNode>? OnNodeClicked;
    public event Action<ScanNode>? OnNodeHovered;

    public TreemapControl()
    {
        Background = Brushes.Transparent;
        ClipToBounds = true;
    }

    public void SetData(List<ScanNode> nodes)
    {
        ItemsSource = nodes;
    }

    public void DrillUp()
    {
        if (_currentRoot is null)
        {
            return;
        }

        _currentRoot = null;
        _rects = (ItemsSource ?? new List<ScanNode>())
            .Select(node => new TreemapRect { Node = node, Size = node.Size })
            .ToList();
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (_rects.Count == 0)
        {
            return;
        }

        var totalWidth = ActualWidth;
        var totalHeight = ActualHeight;
        if (totalWidth <= 0 || totalHeight <= 0)
        {
            return;
        }

        LayoutSquarified(_rects, 0, 0, totalWidth, totalHeight);
        foreach (var rect in _rects)
        {
            var brush = GetRiskBrush(rect.Node.RiskLevel);
            dc.DrawRectangle(brush, new Pen(Brushes.White, 1), new Rect(rect.X, rect.Y, rect.Width, rect.Height));
            if (rect.Width > 60 && rect.Height > 20)
            {
                var text = string.IsNullOrWhiteSpace(rect.Node.Name) ? rect.Node.Path : rect.Node.Name;
                var ft = new FormattedText(
                    text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Microsoft YaHei"),
                    10,
                    Brushes.White,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(ft, new Point(rect.X + 3, rect.Y + 3));
            }
        }
    }

    private void LayoutSquarified(List<TreemapRect> rects, double x, double y, double w, double h)
    {
        var layout = TreemapLayoutCalculator.Calculate(rects.Select(rect => rect.Node), x, y, w, h);
        foreach (var item in layout)
        {
            var rect = rects.FirstOrDefault(candidate => ReferenceEquals(candidate.Node, item.Node));
            if (rect is null)
            {
                continue;
            }

            rect.X = item.X;
            rect.Y = item.Y;
            rect.Width = item.Width;
            rect.Height = item.Height;
        }
    }

    private static Brush GetRiskBrush(RiskLevel level) => level switch
    {
        RiskLevel.Low => new SolidColorBrush(Color.FromRgb(16, 124, 16)),
        RiskLevel.Medium => new SolidColorBrush(Color.FromRgb(255, 140, 0)),
        RiskLevel.High => new SolidColorBrush(Color.FromRgb(216, 59, 1)),
        RiskLevel.Forbidden => new SolidColorBrush(Color.FromRgb(232, 17, 35)),
        _ => new SolidColorBrush(Color.FromRgb(160, 160, 160))
    };

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(this);
        var hit = FindHit(pos);
        if (hit != null)
        {
            OnNodeClicked?.Invoke(hit.Node);
            if (hit.Node.Children.Count > 0)
            {
                _currentRoot = hit.Node;
                _rects = hit.Node.Children.Select(c => new TreemapRect { Node = c, Size = c.Size }).ToList();
                InvalidateVisual();
            }
        }

        base.OnMouseLeftButtonDown(e);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var pos = e.GetPosition(this);
        var hit = FindHit(pos);
        if (hit != null)
        {
            ToolTip = $"{hit.Node.Path}\n大小：{ReportGenerator.FormatSize(hit.Node.Size)}\n风险：{hit.Node.RiskLevel}";
            OnNodeHovered?.Invoke(hit.Node);
        }

        base.OnMouseMove(e);
    }

    private TreemapRect? FindHit(Point pos)
    {
        return _rects.FirstOrDefault(r => pos.X >= r.X && pos.X <= r.X + r.Width &&
                                          pos.Y >= r.Y && pos.Y <= r.Y + r.Height);
    }

    private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TreemapControl ctrl)
        {
            ctrl._currentRoot = null;
            ctrl._rects = e.NewValue is List<ScanNode> nodes
                ? nodes.Select(n => new TreemapRect { Node = n, Size = n.Size }).ToList()
                : new List<TreemapRect>();
            ctrl.InvalidateVisual();
        }
    }
}

public class TreemapRect
{
    public ScanNode Node { get; set; } = new();
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public long Size { get; set; }
}
