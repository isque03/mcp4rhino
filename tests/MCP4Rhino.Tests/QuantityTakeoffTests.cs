using MCP4Rhino.Logic;
using MCP4Rhino.Logic.Models;
using Xunit;

namespace MCP4Rhino.Tests;

public class QuantityTakeoffTests
{
    [Fact]
    public void Rollup_Count_Area_Length()
    {
        var elements = new[]
        {
            new TaggedElement
            {
                Id = "1", Kind = "space",
                Tags = new() { ["mcp4:level"] = "L1", ["mcp4:area"] = "100" },
            },
            new TaggedElement
            {
                Id = "2", Kind = "space",
                Tags = new() { ["mcp4:level"] = "L1", ["mcp4:area"] = "50" },
            },
            new TaggedElement
            {
                Id = "3", Kind = "wall",
                Tags = new() { ["mcp4:level"] = "L1" },
                BBox = new BBox(0, 0, 0, 12, 0.5, 8),
            },
            new TaggedElement
            {
                Id = "4", Kind = "wall",
                Tags = new() { ["mcp4:level"] = "L2" },
                BBox = new BBox(0, 0, 0, 5, 0.5, 8),
            },
        };

        var rows = QuantityTakeoff.Compute(elements);
        Assert.Equal(3, rows.Count);
        var spaces = rows.Single(r => r.Kind == "space" && r.Level == "L1");
        Assert.Equal(2, spaces.Count);
        Assert.Equal(150, spaces.Area);

        var l1Only = QuantityTakeoff.Compute(elements, levelFilter: "L1");
        Assert.All(l1Only, r => Assert.Equal("L1", r.Level));

        var walls = QuantityTakeoff.Compute(elements, kindFilter: "wall");
        Assert.All(walls, r => Assert.Equal("wall", r.Kind));
    }
}

public class BboxClashTests
{
    [Fact]
    public void Hard_And_Soft_Clash()
    {
        var a = new BBox(0, 0, 0, 10, 10, 10);
        var b = new BBox(5, 5, 5, 15, 15, 15);
        var c = new BBox(20, 20, 20, 30, 30, 30);

        var hard = BboxClash.Detect([("a", a)], [("b", b), ("c", c)]);
        Assert.Single(hard);
        Assert.Equal("hard", hard[0].Type);

        var near = new BBox(10.5, 0, 0, 12, 1, 1);
        var soft = BboxClash.Detect([("a", a)], [("n", near)], clearance: 1);
        Assert.Single(soft);
        Assert.Equal("soft", soft[0].Type);

        var none = BboxClash.Detect([("a", a)], [("c", c)]);
        Assert.Empty(none);
    }

    [Fact]
    public void Skips_Same_Id()
    {
        var box = new BBox(0, 0, 0, 1, 1, 1);
        Assert.Empty(BboxClash.Detect([("x", box)], [("x", box)]));
    }
}
