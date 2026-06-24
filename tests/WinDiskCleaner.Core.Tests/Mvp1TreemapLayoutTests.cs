using WinDiskCleaner.Core.Models;
using WinDiskCleaner.Core.Services;

namespace WinDiskCleaner.Core.Tests;

public class Mvp1TreemapLayoutTests
{
    [Fact]
    public void CalculateLayout_UsesEntireBoundsWithoutOverlap()
    {
        var nodes = new List<ScanNode>
        {
            new() { Name = "A", Path = "A", Size = 60, RiskLevel = RiskLevel.Low },
            new() { Name = "B", Path = "B", Size = 30, RiskLevel = RiskLevel.Medium },
            new() { Name = "C", Path = "C", Size = 10, RiskLevel = RiskLevel.High }
        };

        var rects = TreemapLayoutCalculator.Calculate(nodes, 0, 0, 100, 80);

        Assert.Equal(3, rects.Count);
        Assert.All(rects, rect =>
        {
            Assert.True(rect.Width > 0);
            Assert.True(rect.Height > 0);
            Assert.True(rect.X >= 0);
            Assert.True(rect.Y >= 0);
            Assert.True(rect.X + rect.Width <= 100.0001);
            Assert.True(rect.Y + rect.Height <= 80.0001);
        });
        AssertNoOverlap(rects);
        Assert.InRange(rects.Sum(r => r.Width * r.Height), 7999.0, 8000.1);
    }

    [Fact]
    public void CalculateLayout_AreaIsProportionalToNodeSize()
    {
        var nodes = new List<ScanNode>
        {
            new() { Name = "Large", Path = "Large", Size = 75 },
            new() { Name = "Small", Path = "Small", Size = 25 }
        };

        var rects = TreemapLayoutCalculator.Calculate(nodes, 0, 0, 200, 100);

        var largeArea = rects.Single(r => r.Node.Name == "Large").Width * rects.Single(r => r.Node.Name == "Large").Height;
        var smallArea = rects.Single(r => r.Node.Name == "Small").Width * rects.Single(r => r.Node.Name == "Small").Height;
        var ratio = largeArea / (largeArea + smallArea);

        Assert.InRange(ratio, 0.73, 0.77);
    }

    [Fact]
    public void CalculateLayout_IgnoresNonPositiveNodes()
    {
        var nodes = new List<ScanNode>
        {
            new() { Name = "Zero", Path = "Zero", Size = 0 },
            new() { Name = "Negative", Path = "Negative", Size = -1 },
            new() { Name = "Real", Path = "Real", Size = 10 }
        };

        var rects = TreemapLayoutCalculator.Calculate(nodes, 0, 0, 50, 50);

        var rect = Assert.Single(rects);
        Assert.Equal("Real", rect.Node.Name);
        Assert.Equal(50, rect.Width, precision: 3);
        Assert.Equal(50, rect.Height, precision: 3);
    }

    private static void AssertNoOverlap(IReadOnlyList<TreemapLayoutRect> rects)
    {
        for (var i = 0; i < rects.Count; i++)
        {
            for (var j = i + 1; j < rects.Count; j++)
            {
                var a = rects[i];
                var b = rects[j];
                var separated = a.X + a.Width <= b.X + 0.0001
                    || b.X + b.Width <= a.X + 0.0001
                    || a.Y + a.Height <= b.Y + 0.0001
                    || b.Y + b.Height <= a.Y + 0.0001;
                Assert.True(separated, $"Rectangles {a.Node.Name} and {b.Node.Name} overlap.");
            }
        }
    }
}
