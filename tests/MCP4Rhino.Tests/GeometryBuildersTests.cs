using MCP4Rhino.Logic;
using Xunit;

namespace MCP4Rhino.Tests;

/// <summary>
/// GeometryBuilders needs Rhino native <c>rhcommon_c</c> (not available in plain <c>dotnet test</c>).
/// Class is <see cref="System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute"/>'d;
/// this suite documents the constraint rather than exercising natives.
/// </summary>
public class GeometryBuildersTests
{
    [Fact]
    public void GeometryBuilders_IsExcludedFromCoverageGate()
    {
        var attrs = typeof(GeometryBuilders).GetCustomAttributes(typeof(System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverageAttribute), false);
        Assert.NotEmpty(attrs);
    }
}
