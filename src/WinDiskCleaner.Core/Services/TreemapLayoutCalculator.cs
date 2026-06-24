using WinDiskCleaner.Core.Models;

namespace WinDiskCleaner.Core.Services;

public static class TreemapLayoutCalculator
{
    public static List<TreemapLayoutRect> Calculate(IEnumerable<ScanNode> nodes, double x, double y, double width, double height)
    {
        var items = nodes
            .Where(node => node.Size > 0)
            .OrderByDescending(node => node.Size)
            .Select(node => new LayoutItem(node, node.Size))
            .ToList();

        if (items.Count == 0 || width <= 0 || height <= 0)
        {
            return new List<TreemapLayoutRect>();
        }

        var totalSize = items.Sum(item => item.Size);
        var totalArea = width * height;
        foreach (var item in items)
        {
            item.Area = item.Size / totalSize * totalArea;
        }

        var results = new List<TreemapLayoutRect>();
        Squarify(items, new List<LayoutItem>(), new TreemapBounds(x, y, width, height), results);
        return results;
    }

    private static void Squarify(List<LayoutItem> remaining, List<LayoutItem> row, TreemapBounds bounds, List<TreemapLayoutRect> results)
    {
        if (remaining.Count == 0)
        {
            if (row.Count > 0)
            {
                LayoutRow(row, bounds, results, out _);
            }

            return;
        }

        var next = remaining[0];
        if (row.Count == 0 || WorstAspectRatio(row.Concat(new[] { next }), ShortSide(bounds)) <= WorstAspectRatio(row, ShortSide(bounds)))
        {
            row.Add(next);
            remaining.RemoveAt(0);
            Squarify(remaining, row, bounds, results);
            return;
        }

        LayoutRow(row, bounds, results, out var nextBounds);
        Squarify(remaining, new List<LayoutItem>(), nextBounds, results);
    }

    private static void LayoutRow(List<LayoutItem> row, TreemapBounds bounds, List<TreemapLayoutRect> results, out TreemapBounds nextBounds)
    {
        var rowArea = row.Sum(item => item.Area);
        if (rowArea <= 0)
        {
            nextBounds = bounds;
            return;
        }

        if (bounds.Width >= bounds.Height)
        {
            var rowWidth = Math.Min(bounds.Width, rowArea / bounds.Height);
            var y = bounds.Y;
            foreach (var item in row)
            {
                var itemHeight = item.Area / rowWidth;
                results.Add(new TreemapLayoutRect(item.Node, bounds.X, y, rowWidth, itemHeight));
                y += itemHeight;
            }

            nextBounds = new TreemapBounds(bounds.X + rowWidth, bounds.Y, Math.Max(0, bounds.Width - rowWidth), bounds.Height);
        }
        else
        {
            var rowHeight = Math.Min(bounds.Height, rowArea / bounds.Width);
            var x = bounds.X;
            foreach (var item in row)
            {
                var itemWidth = item.Area / rowHeight;
                results.Add(new TreemapLayoutRect(item.Node, x, bounds.Y, itemWidth, rowHeight));
                x += itemWidth;
            }

            nextBounds = new TreemapBounds(bounds.X, bounds.Y + rowHeight, bounds.Width, Math.Max(0, bounds.Height - rowHeight));
        }
    }

    private static double WorstAspectRatio(IEnumerable<LayoutItem> row, double side)
    {
        var rowList = row.ToList();
        if (rowList.Count == 0 || side <= 0)
        {
            return double.PositiveInfinity;
        }

        var sum = rowList.Sum(item => item.Area);
        var min = rowList.Min(item => item.Area);
        var max = rowList.Max(item => item.Area);
        if (sum <= 0 || min <= 0)
        {
            return double.PositiveInfinity;
        }

        var sideSquared = side * side;
        return Math.Max(sideSquared * max / (sum * sum), (sum * sum) / (sideSquared * min));
    }

    private static double ShortSide(TreemapBounds bounds) => Math.Min(bounds.Width, bounds.Height);

    private sealed class LayoutItem
    {
        public LayoutItem(ScanNode node, double size)
        {
            Node = node;
            Size = size;
        }

        public ScanNode Node { get; }
        public double Size { get; }
        public double Area { get; set; }
    }

    private readonly record struct TreemapBounds(double X, double Y, double Width, double Height);
}

public sealed record TreemapLayoutRect(ScanNode Node, double X, double Y, double Width, double Height);
