using Rhino;

namespace MCP4Rhino.Tools;

/// <summary>Native read helpers shared by explicit tools and resource projections.</summary>
internal static class RhinoReadModel
{
    public static string UnitName(RhinoDoc document) => document.ModelUnitSystem.ToString();
    public static object Units(RhinoDoc document) => new
    {
        model = UnitName(document), absolute_tolerance = document.ModelAbsoluteTolerance,
        angle_tolerance_radians = document.ModelAngleToleranceRadians,
    };
}
