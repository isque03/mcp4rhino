using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Rhino.Geometry.Intersect;

namespace MCP4Rhino.Grasshopper;

internal static class GeometrySummary
{
    public static object Read(IGH_GeometricGoo value, double tolerance)
    {
        var geometry = value.ScriptVariable() as GeometryBase;
        var measurements = new Dictionary<string, object?>
        {
            ["closed"] = null, ["planar"] = null, ["self_intersecting"] = null,
            ["solid"] = null, ["length"] = null, ["area"] = null, ["volume"] = null,
        };
        if (value.IsValid && geometry is { IsValid: true }) Measure(geometry, measurements, tolerance);
        var valid = value.IsValid && (geometry?.IsValid ?? true);
        return new
        {
            type = value.TypeName, geometry_type = geometry?.GetType().Name, valid,
            invalid_reason = valid ? null : value.IsValidWhyNot,
            bounds = ValueCodec.Bounds(value.Boundingbox), reference_id = value.ReferenceID,
            measurements, unsupported_measurements = measurements.Where(p => p.Value is null).Select(p => p.Key).ToArray(),
        };
    }

    private static void Measure(GeometryBase geometry, Dictionary<string, object?> values, double tolerance)
    {
        switch (geometry)
        {
            case Curve curve:
                values["closed"] = curve.IsClosed; values["planar"] = curve.IsPlanar(tolerance);
                using (var intersections = Intersection.CurveSelf(curve, tolerance))
                    values["self_intersecting"] = intersections is null ? null : intersections.Count > 0;
                values["length"] = Finite(curve.GetLength());
                break;
            case Brep brep:
                values["solid"] = brep.IsSolid;
                using (var area = AreaMassProperties.Compute(brep)) values["area"] = Finite(area?.Area);
                if (brep.IsSolid)
                    using (var volume = VolumeMassProperties.Compute(brep)) values["volume"] = Finite(volume?.Volume);
                break;
            case Mesh mesh:
                values["closed"] = mesh.IsClosed; values["solid"] = mesh.IsSolid;
                break;
            case Extrusion extrusion:
                values["solid"] = extrusion.IsSolid;
                break;
        }
    }

    private static double? Finite(double? value) => value is { } number && double.IsFinite(number) ? number : null;
}
