using MCP4Rhino.Logic.Models;
using Xunit;

namespace MCP4Rhino.Tests;

public class BBoxTests
{
    [Fact]
    public void Inflate_Intersects_Sizes()
    {
        var b = new BBox(0, 0, 0, 1, 2, 3);
        Assert.True(b.IsValid);
        Assert.Equal(1, b.SizeX);
        Assert.Equal(2, b.SizeY);
        Assert.Equal(3, b.SizeZ);

        var big = b.Inflate(1);
        Assert.Equal(-1, big.MinX);
        Assert.True(big.Intersects(new BBox(1.5, 0, 0, 2, 1, 1)));
        Assert.False(b.Intersects(new BBox(2, 0, 0, 3, 1, 1)));
        Assert.False(new BBox(1, 0, 0, 0, 1, 1).IsValid);
    }
}
