namespace MCP4Rhino.Logic;

/// <summary>Simple 3D vector for JSON parsing without RhinoCommon at test time.</summary>
public readonly record struct Vec3(double X, double Y, double Z)
{
    public static Vec3 Origin => new(0, 0, 0);
}
