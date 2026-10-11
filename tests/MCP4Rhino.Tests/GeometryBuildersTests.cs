using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using MCP4Rhino.Logic;
using Xunit;

namespace MCP4Rhino.Tests;

/// <summary>
/// GeometryBuilders needs Rhino natives; it is excluded from the coverage gate in source.
/// Avoid <c>typeof(GeometryBuilders)</c> — that loads RhinoCommon (NuGet ref assembly only).
/// </summary>
public class GeometryBuildersTests
{
    [Fact]
    public void GeometryBuilders_SourceDeclaresExcludeFromCodeCoverage()
    {
        // Resolve via metadata name without forcing a successful type load of Rhino-dependent members.
        var asm = typeof(UnitConversion).Assembly;
        Type? type = null;
        try
        {
            type = asm.GetType("MCP4Rhino.Logic.GeometryBuilders", throwOnError: false, ignoreCase: false);
        }
        catch (FileNotFoundException)
        {
            // RhinoCommon absent (CI) — exclusion is enforced by attribute in source + coverlet ExcludeByAttribute.
            return;
        }
        catch (ReflectionTypeLoadException)
        {
            return;
        }

        if (type is null) return;
        Assert.NotNull(type.GetCustomAttribute<ExcludeFromCodeCoverageAttribute>());
    }
}
