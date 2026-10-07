using MCP4Rhino.Logic;
using Xunit;

namespace MCP4Rhino.Tests;

public class SurfaceOpsTests
{
    [Theory]
    [InlineData("tangency", SurfaceOps.Continuity.Tangency)]
    [InlineData("G1", SurfaceOps.Continuity.Tangency)]
    [InlineData("curvature", SurfaceOps.Continuity.Curvature)]
    [InlineData("g2", SurfaceOps.Continuity.Curvature)]
    [InlineData("position", SurfaceOps.Continuity.Position)]
    public void ParseContinuity_Known(string input, SurfaceOps.Continuity expected) =>
        Assert.Equal(expected, SurfaceOps.ParseContinuity(input));

    [Fact]
    public void ParseContinuity_Null_UsesFallback() =>
        Assert.Equal(SurfaceOps.Continuity.Tangency, SurfaceOps.ParseContinuity(null));

    [Fact]
    public void ParseContinuity_Unknown_Throws() =>
        Assert.Throws<ArgumentException>(() => SurfaceOps.ParseContinuity("smooth"));

    [Theory]
    [InlineData("loose", SurfaceOps.LoftStyle.Loose)]
    [InlineData("straight", SurfaceOps.LoftStyle.Straight)]
    [InlineData("normal", SurfaceOps.LoftStyle.Normal)]
    public void ParseLoftStyle_Known(string input, SurfaceOps.LoftStyle expected) =>
        Assert.Equal(expected, SurfaceOps.ParseLoftStyle(input));

    [Fact]
    public void ParseLoftStyle_Unknown_Throws() =>
        Assert.Throws<ArgumentException>(() => SurfaceOps.ParseLoftStyle("bezier"));

    [Fact]
    public void SelectEdgeIndex_PicksClosest() =>
        Assert.Equal(2, SurfaceOps.SelectEdgeIndex([9, 8, 1, 7], ambiguityTol: 0.1));

    [Fact]
    public void SelectEdgeIndex_Ambiguous_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            SurfaceOps.SelectEdgeIndex([1.0, 1.0, 5], ambiguityTol: 0.05));

    [Fact]
    public void SelectFaceIndex_PicksClosest() =>
        Assert.Equal(1, SurfaceOps.SelectFaceIndex([5, 0.5, 4], ambiguityTol: 0.1));

    [Fact]
    public void SelectFaceIndex_Ambiguous_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            SurfaceOps.SelectFaceIndex([1.0, 1.02, 9], ambiguityTol: 0.05));

    [Fact]
    public void SelectFaceIndex_Empty_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            SurfaceOps.SelectFaceIndex([], ambiguityTol: 0.1));

    [Fact]
    public void RequireEdgeIndex_OutOfRange_Throws() =>
        Assert.Throws<ArgumentException>(() => SurfaceOps.RequireEdgeIndex(3, 3));

    [Fact]
    public void RequireEdgeIndex_Ok() =>
        Assert.Equal(1, SurfaceOps.RequireEdgeIndex(1, 3));

    [Fact]
    public void RequireFaceIndex_OutOfRange_Throws() =>
        Assert.Throws<ArgumentException>(() => SurfaceOps.RequireFaceIndex(2, 2));

    [Fact]
    public void RequireFaceIndex_Ok() =>
        Assert.Equal(0, SurfaceOps.RequireFaceIndex(0, 2));

    [Fact]
    public void RequirePlanarCutter_False_Throws() =>
        Assert.Throws<ArgumentException>(() => SurfaceOps.RequirePlanarCutter(false));

    [Fact]
    public void RequirePlanarCutter_True_Ok() =>
        SurfaceOps.RequirePlanarCutter(true);

    [Fact]
    public void CollectTrimReplacePieces_OrdersPrimaryThenTrims()
    {
        var pieces = SurfaceOps.CollectTrimReplacePieces(
            primary: ["f0", "f1"],
            trimA: ["a0"],
            trimB: ["b0", "b1"]);
        Assert.Equal(["f0", "f1", "a0", "b0", "b1"], pieces);
    }

    [Fact]
    public void CollectTrimReplacePieces_SkipsNullTrims()
    {
        var pieces = SurfaceOps.CollectTrimReplacePieces(
            primary: ["f0"],
            trimA: null,
            trimB: null);
        Assert.Equal(["f0"], pieces);
    }

    [Fact]
    public void CollectTrimReplacePieces_EmptyPrimary_Throws() =>
        Assert.Throws<ArgumentException>(() =>
            SurfaceOps.CollectTrimReplacePieces<string>([], null, null));

    [Fact]
    public void RequirePositive_Zero_Throws() =>
        Assert.Throws<ArgumentException>(() => SurfaceOps.RequirePositive(0, "radius"));

    [Fact]
    public void RequireCount_TooFew_Throws() =>
        Assert.Throws<ArgumentException>(() => SurfaceOps.RequireCount(1, 2, "loft"));
}
