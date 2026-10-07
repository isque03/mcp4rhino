using System.Diagnostics.CodeAnalysis;
using Rhino.Geometry;

namespace MCP4Rhino.Logic;

/// <summary>
/// Headless RhinoCommon geometry builders (no document).
/// Requires Rhino native libs at runtime — excluded from the unit-coverage gate.
/// </summary>
[ExcludeFromCodeCoverage]
public static class GeometryBuilders
{
    public static Brep? ToBrep(GeometryBase? g) => g switch
    {
        Brep b => b.DuplicateBrep(),
        Extrusion e => e.ToBrep(),
        Surface s => Brep.CreateFromSurface(s),
        _ => null,
    };

    public static Brep? ExtrudeProfile(Curve profile, Vector3d dir, double tol, bool cap = true)
    {
        var srf = Surface.CreateExtrusion(profile, dir);
        if (srf is null) return null;
        var brep = Brep.CreateFromSurface(srf);
        if (brep is null) return null;
        if (cap && profile.IsClosed)
        {
            var capped = brep.CapPlanarHoles(tol);
            if (capped is not null) brep = capped;
        }
        if (brep.IsSolid && brep.SolidOrientation == BrepSolidOrientation.Inward)
            brep.Flip();
        return brep;
    }

    public static PolylineCurve ClosedPolyline(IEnumerable<Point3d> pts, double tol)
    {
        var list = pts.ToList();
        if (list.Count < 3) throw new ArgumentException("A closed boundary needs at least 3 points.");
        if (list[0].DistanceTo(list[^1]) > tol) list.Add(list[0]);
        var crv = new PolylineCurve(list);
        if (!crv.IsValid || !crv.IsClosed) throw new ArgumentException("Invalid closed boundary.");
        return crv;
    }

    public static Brep OrientedBox(Point3d baseCenter, Vector3d dir, double width, double depth, double height)
    {
        dir.Z = 0;
        if (!dir.Unitize()) dir = Vector3d.XAxis;
        var side = new Vector3d(-dir.Y, dir.X, 0);
        var plane = new Plane(baseCenter, dir, side);
        var box = new Box(plane,
            new Interval(-width / 2, width / 2),
            new Interval(-depth / 2, depth / 2),
            new Interval(0, height));
        return box.ToBrep() ?? throw new InvalidOperationException("Failed to build box.");
    }

    public static Brep SideProfileSolid(Point3d origin, Vector3d run, double width, IList<(double u, double v)> profile, double tol)
    {
        run.Z = 0;
        if (!run.Unitize()) throw new ArgumentException("Run direction has zero horizontal length.");
        var side = new Vector3d(-run.Y, run.X, 0);
        var start = origin - side * (width / 2);
        var pts = profile.Select(p => start + run * p.u + Vector3d.ZAxis * p.v).ToList();
        var crv = ClosedPolyline(pts, tol);
        return ExtrudeProfile(crv, side * width, tol) ?? throw new InvalidOperationException("Profile extrusion failed.");
    }

    public static string Classify(GeometryBase? geometry) => geometry switch
    {
        null => "unknown",
        Brep => "brep",
        Extrusion => "extrude",
        Mesh => "mesh",
        Curve => "curve",
        Point => "point",
        Surface => "surface",
        _ => geometry.GetType().Name.ToLowerInvariant(),
    };
}
