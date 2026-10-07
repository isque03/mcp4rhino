using MCP4Rhino.Logic;
using Xunit;

namespace MCP4Rhino.Tests;

public class CurveOpsTests
{
    [Fact]
    public void NormalizeSplitParameters_SortsAndDedupes()
    {
        var ts = CurveOps.NormalizeSplitParameters([3, 1, 2, 1]);
        Assert.Equal(new[] { 1.0, 2.0, 3.0 }, ts);
    }

    [Fact]
    public void NormalizeSplitParameters_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() => CurveOps.NormalizeSplitParameters([]));
    }

    [Fact]
    public void OrderedDomain_SwapsReversed()
    {
        var (t0, t1) = CurveOps.OrderedDomain(5, 1);
        Assert.Equal(1, t0);
        Assert.Equal(5, t1);
    }

    [Fact]
    public void SelectUnambiguousNearest_PicksClosest()
    {
        Assert.Equal(1, CurveOps.SelectUnambiguousNearest([10, 1, 8], ambiguityTol: 0.5));
    }

    [Fact]
    public void SelectUnambiguousNearest_Ambiguous_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            CurveOps.SelectUnambiguousNearest([1.0, 1.0, 5], ambiguityTol: 0.1));
    }

    [Fact]
    public void SelectUnambiguousNearest_Empty_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            CurveOps.SelectUnambiguousNearest([], 0.1));
    }

    [Fact]
    public void SelectUnambiguousNearest_TooFar_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            CurveOps.SelectUnambiguousNearest([10, 20], ambiguityTol: 0.1, maxDistance: 1));
    }
}
