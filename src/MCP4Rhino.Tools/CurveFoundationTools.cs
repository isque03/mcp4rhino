using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Host;
using MCP4Rhino.Logic;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using static MCP4Rhino.Tools.ToolHelpers;

namespace MCP4Rhino.Tools;

/// <summary>
/// Standard toolbar curve/line create + edit tools (Point/Line/Circle/Ellipse/Polygon/Join/Trim/Split/Explode).
/// </summary>
[ExcludeFromCodeCoverage]
internal static class CurveFoundationTools
{
    private const string IdsDesc = "GUIDs: comma-separated string or JSON array";

    public static object[] ListTools() => Tools;

    public static readonly object[] Tools =
    [
        ToolC("create_point", "Create a point object (Rhino Point).",
            ("point*", "any", "[x,y,z] or {x,y,z}")),
        ToolC("create_line", "Create a line segment between two points (Rhino Line).",
            ("start", "any", "[x,y,z] start (or use from)"),
            ("end", "any", "[x,y,z] end (or use to)"),
            ("from", "any", "Alias for start"),
            ("to", "any", "Alias for end")),
        ToolC("create_circle", "Create a circle curve (Rhino Circle).",
            ("center*", "any", "[x,y,z]"),
            ("radius*", "number", "Radius"),
            ("normal", "any", "Plane normal [x,y,z] (default +Z when omitted)")),
        ToolC("create_ellipse", "Create an ellipse curve in a plane (Rhino Ellipse).",
            ("center*", "any", "[x,y,z]"),
            ("radius_x*", "number", "Semi-axis along plane X"),
            ("radius_y*", "number", "Semi-axis along plane Y"),
            ("normal", "any", "Plane normal [x,y,z] (default +Z when omitted)")),
        ToolC("create_polygon", "Create a regular polygon polyline (Rhino Polygon).",
            ("center*", "any", "[x,y,z]"),
            ("radius*", "number", "Distance from center to vertex (or to flat if flat_to_x)"),
            ("sides*", "integer", "Number of sides (≥ 3)"),
            ("flat_to_x", "boolean", "If true, a flat side faces plane +X; else a vertex on +X (default false)"),
            ("normal", "any", "Plane normal [x,y,z] (default +Z when omitted)")),
        Tool("join_curves", "Join curve objects end-to-end (Rhino Join).",
            ("ids*", "any", IdsDesc),
            ("tolerance", "number", "Join tolerance (default model absolute tolerance)"),
            ("keep_inputs", "boolean", "Keep input curves (default false)")),
        Tool("trim_curve", "Trim a curve by domain [t0,t1] to keep, or by a cutter curve (requires keep_point when multiple segments).",
            ("curve_id*", "string", "Curve GUID to trim"),
            ("domain", "array", "[t0,t1] parameter interval to keep"),
            ("cutter_id", "string", "Cutting curve GUID (alternative to domain)"),
            ("keep_point", "any", "[x,y,z] required in cutter mode when split yields >1 segment"),
            ("keep_input", "boolean", "Keep original curve (default false)")),
        Tool("split_curve", "Split a curve at parameters or by a cutter curve (Rhino Split).",
            ("curve_id*", "string", "Curve GUID"),
            ("parameters", "array", "Parameter t values to split at"),
            ("cutter_id", "string", "Cutting curve GUID (alternative to parameters)"),
            ("keep_input", "boolean", "Keep original curve (default false)")),
        Tool("explode_objects", "Explode polycurves, polylines, extrusions, polysurfaces, or block instances (Rhino Explode).",
            ("ids*", "any", IdsDesc),
            ("keep_inputs", "boolean", "Keep original objects (default false)")),
    ];

    public static string? TryCall(string name, JsonObject args) => name switch
    {
        "create_point" => CreatePoint(args),
        "create_line" => CreateLine(args),
        "create_circle" => CreateCircle(args),
        "create_ellipse" => CreateEllipse(args),
        "create_polygon" => CreatePolygon(args),
        "join_curves" => JoinCurves(args),
        "trim_curve" => TrimCurve(args),
        "split_curve" => SplitCurve(args),
        "explode_objects" => ExplodeObjects(args),
        _ => null,
    };

    private static Guid AddCurveOrThrow(RhinoDoc doc, Curve crv, ObjectAttributes attrs, string what = "curve")
    {
        var id = doc.Objects.AddCurve(crv, attrs);
        if (id == Guid.Empty) throw new InvalidOperationException($"Failed to add {what}.");
        return id;
    }

    private static Guid AddCurve(RhinoDoc doc, Curve crv, JsonObject args)
    {
        var id = AddCurveOrThrow(doc, crv, BuildAttributes(doc, args));
        ApplyGroup(doc, id, args);
        return id;
    }

    private static Plane PlaneFromNormal(Point3d origin, JsonObject args)
    {
        if (args["normal"] is null)
            return new Plane(origin, Vector3d.ZAxis);

        var n = new Vector3d(ParsePoint(args["normal"], "normal"));
        if (!n.Unitize())
            throw new ArgumentException("normal must be a non-zero vector (omit normal for World +Z).");
        var plane = new Plane(origin, n);
        if (!plane.IsValid) throw new ArgumentException("Invalid plane normal.");
        return plane;
    }

    private static string CreatePoint(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var p = ParsePoint(args["point"] ?? throw new ArgumentException("point required"), "point");
        var id = doc.Objects.AddPoint(p, BuildAttributes(doc, args));
        if (id == Guid.Empty) throw new InvalidOperationException("Failed to add point.");
        ApplyGroup(doc, id, args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "point", point = Pt(p) });
    });

    private static string CreateLine(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var startNode = args["start"] ?? args["from"] ?? throw new ArgumentException("start (or from) required");
        var endNode = args["end"] ?? args["to"] ?? throw new ArgumentException("end (or to) required");
        var a = ParsePoint(startNode, "start");
        var b = ParsePoint(endNode, "end");
        if (a.DistanceTo(b) < doc.ModelAbsoluteTolerance)
            throw new ArgumentException("Line start and end are coincident.");
        var id = AddCurve(doc, new LineCurve(a, b), args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "curve", length = a.DistanceTo(b), start = Pt(a), end = Pt(b) });
    });

    private static string CreateCircle(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var center = ParsePoint(args["center"] ?? throw new ArgumentException("center required"), "center");
        var radius = Num(args, "radius");
        if (radius <= 0) throw new ArgumentException("radius must be positive.");
        var circle = new Circle(PlaneFromNormal(center, args), radius);
        if (!circle.IsValid) throw new ArgumentException("Invalid circle.");
        var id = AddCurve(doc, new ArcCurve(circle), args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "curve", radius, circumference = circle.Circumference });
    });

    private static string CreateEllipse(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var center = ParsePoint(args["center"] ?? throw new ArgumentException("center required"), "center");
        var rx = Num(args, "radius_x");
        var ry = Num(args, "radius_y");
        if (rx <= 0 || ry <= 0) throw new ArgumentException("radius_x and radius_y must be positive.");
        var ellipse = new Ellipse(PlaneFromNormal(center, args), rx, ry);
        if (!ellipse.IsValid) throw new ArgumentException("Invalid ellipse.");
        var nurbs = ellipse.ToNurbsCurve() ?? throw new InvalidOperationException("Failed to build ellipse curve.");
        var id = AddCurve(doc, nurbs, args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "curve", radius_x = rx, radius_y = ry });
    });

    private static string CreatePolygon(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var center = ParsePoint(args["center"] ?? throw new ArgumentException("center required"), "center");
        var radius = Num(args, "radius");
        var sides = (int)Num(args, "sides");
        if (radius <= 0) throw new ArgumentException("radius must be positive.");
        if (sides < 3) throw new ArgumentException("sides must be ≥ 3.");
        var plane = PlaneFromNormal(center, args);
        // Prefer flat_to_x; accept legacy flat_to_top as alias.
        var flat = Bool(args, "flat_to_x") || Bool(args, "flat_to_top");
        var angle0 = flat ? Math.PI / sides : 0;
        var pts = new List<Point3d>(sides + 1);
        for (var i = 0; i < sides; i++)
        {
            var a = angle0 + i * (2 * Math.PI / sides);
            pts.Add(plane.PointAt(radius * Math.Cos(a), radius * Math.Sin(a)));
        }
        pts.Add(pts[0]);
        var crv = new PolylineCurve(pts);
        if (!crv.IsValid || !crv.IsClosed) throw new ArgumentException("Invalid polygon.");
        var id = AddCurve(doc, crv, args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "curve", sides, radius, closed = true, flat_to_x = flat });
    });

    private static string JoinCurves(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var ids = IdsArg(args);
        if (ids.Count < 2) throw new ArgumentException("join_curves needs at least two curve ids.");
        var tol = args["tolerance"] is null ? doc.ModelAbsoluteTolerance : Num(args, "tolerance");
        var curves = ids.Select(i =>
            FindObject(doc, i).Geometry as Curve ?? throw new ArgumentException($"Object {i} is not a curve.")).ToArray();
        var joined = Curve.JoinCurves(curves, tol);
        if (joined is null || joined.Length == 0)
            throw new InvalidOperationException("Join failed — curves may not touch within tolerance.");

        var first = FindObject(doc, ids[0]);
        var outIds = new List<Guid>();
        foreach (var c in joined)
        {
            var attrs = first.Attributes.Duplicate();
            var nm = Str(args, "name"); if (!string.IsNullOrWhiteSpace(nm)) attrs.Name = nm;
            var ly = Str(args, "layer"); if (!string.IsNullOrWhiteSpace(ly)) attrs.LayerIndex = EnsureLayer(doc, ly);
            outIds.Add(AddCurveOrThrow(doc, c, attrs, "joined curve"));
        }
        if (!Bool(args, "keep_inputs"))
            foreach (var id in ids) doc.Objects.Delete(id, true);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { ids = outIds.Select(g => g.ToString()).ToArray(), count = outIds.Count });
    });

    private static double[] IntersectionParams(Curve curve, Curve cutter, double tol)
    {
        var ccx = Rhino.Geometry.Intersect.Intersection.CurveCurve(curve, cutter, tol, tol);
        if (ccx is null || ccx.Count == 0) return [];
        return CurveOps.NormalizeSplitParameters(ccx.Select(e => e.ParameterA));
    }

    private static string TrimCurve(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var curveId = IdArg(args, "curve_id");
        var obj = FindObject(doc, curveId);
        var crv = obj.Geometry as Curve ?? throw new ArgumentException($"Object {curveId} is not a curve.");
        var tol = doc.ModelAbsoluteTolerance;
        Curve kept;

        if (args["domain"] is JsonArray dom && dom.Count >= 2)
        {
            var (t0, t1) = CurveOps.OrderedDomain(ToDouble(dom[0]), ToDouble(dom[1]));
            kept = crv.Trim(t0, t1) ?? throw new InvalidOperationException("Trim by domain failed.");
        }
        else if (args["cutter_id"] is not null)
        {
            var cutter = FindObject(doc, IdArg(args, "cutter_id")).Geometry as Curve
                ?? throw new ArgumentException("cutter_id is not a curve.");
            var ts = IntersectionParams(crv, cutter, tol);
            if (ts.Length == 0)
                throw new InvalidOperationException("No intersections between curve and cutter.");
            var parts = crv.Split(ts);
            if (parts is null || parts.Length == 0)
                throw new InvalidOperationException("Split at cutter intersections failed.");
            if (parts.Length == 1)
                kept = parts[0];
            else
            {
                if (args["keep_point"] is null)
                    throw new ArgumentException("keep_point is required when the cutter splits the curve into multiple segments.");
                var keepPt = ParsePoint(args["keep_point"], "keep_point");
                var dists = parts.Select(p => p.PointAt(p.Domain.Mid).DistanceTo(keepPt)).ToArray();
                var curveLen = Math.Max(crv.GetLength(), tol);
                var idx = CurveOps.SelectUnambiguousNearest(
                    dists,
                    ambiguityTol: Math.Max(tol * 10, 1e-6),
                    maxDistance: Math.Max(curveLen * 0.5, tol * 100));
                kept = parts[idx];
            }
        }
        else
            throw new ArgumentException("Provide domain [t0,t1] or cutter_id.");

        var newId = AddCurveOrThrow(doc, kept, obj.Attributes.Duplicate(), "trimmed curve");
        if (!Bool(args, "keep_input")) doc.Objects.Delete(curveId, true);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = newId.ToString(), type = "curve", length = kept.GetLength() });
    });

    private static string SplitCurve(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var curveId = IdArg(args, "curve_id");
        var obj = FindObject(doc, curveId);
        var crv = obj.Geometry as Curve ?? throw new ArgumentException($"Object {curveId} is not a curve.");
        var tol = doc.ModelAbsoluteTolerance;
        double[] ts;

        if (args["parameters"] is JsonArray arr && arr.Count > 0)
            ts = CurveOps.NormalizeSplitParameters(arr.Select(ToDouble));
        else if (args["cutter_id"] is not null)
        {
            var cutter = FindObject(doc, IdArg(args, "cutter_id")).Geometry as Curve
                ?? throw new ArgumentException("cutter_id is not a curve.");
            ts = IntersectionParams(crv, cutter, tol);
            if (ts.Length == 0)
                throw new InvalidOperationException("No intersections between curve and cutter.");
        }
        else
            throw new ArgumentException("Provide parameters or cutter_id.");

        var parts = crv.Split(ts);
        if (parts is null || parts.Length == 0)
            throw new InvalidOperationException("Split produced no segments.");

        var outIds = new List<Guid>();
        foreach (var p in parts)
            outIds.Add(AddCurveOrThrow(doc, p, obj.Attributes.Duplicate(), "split segment"));
        if (!Bool(args, "keep_input")) doc.Objects.Delete(curveId, true);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { ids = outIds.Select(g => g.ToString()).ToArray(), count = outIds.Count });
    });

    private static string ExplodeObjects(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var ids = IdsArg(args);
        var outIds = new List<Guid>();
        var deleted = new List<string>();

        foreach (var id in ids)
        {
            var obj = FindObject(doc, id);
            var attrs = obj.Attributes.Duplicate();
            var pieces = BuildExplodePieces(obj, attrs);
            if (pieces.Count == 0)
                throw new ArgumentException(
                    $"Object {id} cannot be exploded (need polycurve, polyline, extrusion, polysurface, or block instance).");

            // Two-phase: add all pieces, then delete source (no delete on partial add failure).
            var added = new List<Guid>();
            try
            {
                foreach (var (geo, a) in pieces)
                {
                    var nid = doc.Objects.Add(geo, a);
                    if (nid == Guid.Empty)
                        throw new InvalidOperationException($"Failed to add exploded piece of {id}.");
                    added.Add(nid);
                }
            }
            catch
            {
                foreach (var nid in added) doc.Objects.Delete(nid, true);
                throw;
            }

            outIds.AddRange(added);
            if (!Bool(args, "keep_inputs"))
            {
                doc.Objects.Delete(id, true);
                deleted.Add(id.ToString());
            }
        }

        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { ids = outIds.Select(g => g.ToString()).ToArray(), count = outIds.Count, deleted });
    });

    private static List<(GeometryBase Geo, ObjectAttributes Attrs)> BuildExplodePieces(RhinoObject obj, ObjectAttributes attrs)
    {
        var pieces = new List<(GeometryBase, ObjectAttributes)>();
        var g = obj.Geometry;

        switch (g)
        {
            case PolyCurve pc:
            {
                var segs = pc.Explode();
                if (segs is null || segs.Length == 0)
                    throw new InvalidOperationException($"PolyCurve explode produced no segments for {obj.Id}.");
                foreach (var s in segs)
                    pieces.Add((s, attrs.Duplicate()));
                break;
            }
            case Curve c when c.IsPolyline() || c is PolylineCurve:
            {
                if (!c.TryGetPolyline(out var pl) || pl.Count < 2)
                    throw new InvalidOperationException($"Polyline explode failed for {obj.Id}.");
                for (var i = 0; i < pl.Count - 1; i++)
                    pieces.Add((new LineCurve(pl[i], pl[i + 1]), attrs.Duplicate()));
                break;
            }
            case Extrusion ext:
            {
                var brep = ext.ToBrep(true) ?? throw new InvalidOperationException($"Extrusion→Brep failed for {obj.Id}.");
                AddBrepFaces(brep, attrs, pieces);
                break;
            }
            case Brep brep:
                AddBrepFaces(brep, attrs, pieces);
                break;
            case InstanceReferenceGeometry:
            {
                if (obj is not InstanceObject ig)
                    throw new ArgumentException($"Object {obj.Id} is not an explodable block instance.");
                var def = ig.InstanceDefinition
                    ?? throw new InvalidOperationException($"Block definition missing for {obj.Id}.");
                var xform = ig.InstanceXform;
                var defObjs = def.GetObjects();
                if (defObjs is null || defObjs.Length == 0)
                    throw new InvalidOperationException($"Block {def.Name} has no objects.");
                foreach (var src in defObjs)
                {
                    if (src?.Geometry is null)
                        throw new InvalidOperationException($"Block {def.Name} contains null geometry.");
                    var geo = src.Geometry.Duplicate();
                    if (!geo.Transform(xform))
                        throw new InvalidOperationException("Failed to transform exploded block piece.");
                    pieces.Add((geo, src.Attributes.Duplicate()));
                }
                break;
            }
        }

        return pieces;
    }

    private static void AddBrepFaces(Brep brep, ObjectAttributes attrs, List<(GeometryBase, ObjectAttributes)> pieces)
    {
        if (brep.Faces.Count == 0)
            throw new InvalidOperationException("Brep has no faces to explode.");
        for (var fi = 0; fi < brep.Faces.Count; fi++)
        {
            var faceBrep = brep.Faces[fi].DuplicateFace(false)
                ?? throw new InvalidOperationException($"Failed to duplicate brep face {fi}.");
            pieces.Add((faceBrep, attrs.Duplicate()));
        }
    }
}
