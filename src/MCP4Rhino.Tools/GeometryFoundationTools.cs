using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Host;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using static MCP4Rhino.Tools.ToolHelpers;

namespace MCP4Rhino.Tools;

/// <summary>
/// P0 geometry foundation: curves, extrude/boolean/offset/fillet, transforms, layers, blocks, units, command escape hatch.
/// </summary>
internal static class GeometryFoundationTools
{
    private const string IdsDesc = "GUIDs: comma-separated string or JSON array";

    public static object[] ListTools() => Tools;

    public static readonly object[] Tools =
    [
        ToolC("create_polyline", "Create a polyline curve through points; optionally closed.",
            ("points*", "array", "[[x,y,z],...] or flat list (stride 3 if length%3==0, else 2)"),
            ("closed", "boolean", "Close the polyline (default false)")),
        ToolC("create_rectangle", "Create a rectangle in the XY plane: origin + width/height, or corner_min/corner_max.",
            ("origin", "any", "Corner [x,y,z] (or center if centered=true)"),
            ("width", "number", "Size along X"),
            ("height", "number", "Size along Y"),
            ("centered", "boolean", "Treat origin as center (default false)"),
            ("rotation_deg", "number", "Rotation about Z at origin (default 0)"),
            ("corner_min", "any", "[x,y,z] minimum corner (alternative to origin+width/height)"),
            ("corner_max", "any", "[x,y,z] maximum corner")),
        ToolC("create_arc", "Create an arc in the XY plane (counter-clockwise from start to end angle).",
            ("center", "any", "[x,y,z] (default origin)"),
            ("radius*", "number", "Arc radius"),
            ("start_angle_deg", "number", "Start angle from +X (default 0)"),
            ("end_angle_deg", "number", "End angle from +X (default 90); sweep >= 360 gives a circle")),
        ToolC("create_curve", "Create a smooth interpolated NURBS curve through points (falls back to polyline).",
            ("points*", "array", "[[x,y,z],...] or flat list"),
            ("degree", "integer", "Curve degree (default 3, capped by point count)"),
            ("closed", "boolean", "Append first point to close (default false)")),
        Tool("extrude_curve", "Extrude a curve into a Brep (closed planar curves are capped into solids).",
            ("id*", "string", "Curve GUID"),
            ("direction", "any", "[dx,dy,dz] extrusion vector"),
            ("height", "number", "Extrude along +Z by this distance (used if direction omitted)"),
            ("cap", "boolean", "Cap closed planar curves (default true)"),
            ("keep_input", "boolean", "Keep source curve (default true)")),
        Tool("boolean_difference", "Boolean difference A - B (Breps/extrusions). Replaces inputs unless keep_inputs.",
            ("a", "any", "GUID or list of GUIDs (minuend)"),
            ("b", "any", "GUID or list of GUIDs (subtrahend)"),
            ("ids", "any", "Alternative: first id = A, remaining = B"),
            ("keep_inputs", "boolean", "Keep input objects (default false)")),
        Tool("boolean_union", "Boolean union of A and B (or all ids). Replaces inputs unless keep_inputs.",
            ("a", "any", "GUID or list of GUIDs"),
            ("b", "any", "GUID or list of GUIDs"),
            ("ids", "any", "Alternative: list of GUIDs to union"),
            ("keep_inputs", "boolean", "Keep input objects (default false)")),
        Tool("boolean_intersection", "Boolean intersection of A and B. Replaces inputs unless keep_inputs.",
            ("a", "any", "GUID or list of GUIDs"),
            ("b", "any", "GUID or list of GUIDs"),
            ("ids", "any", "Alternative: first id = A, remaining = B"),
            ("keep_inputs", "boolean", "Keep input objects (default false)")),
        Tool("offset_curve", "Offset a curve by distance (sign flips side; planar XY curves use the XY plane).",
            ("id*", "string", "Curve GUID"),
            ("distance*", "number", "Offset distance"),
            ("corner_style", "string", "sharp|round|smooth|chamfer (default sharp)"),
            ("keep_input", "boolean", "Keep source curve (default true)")),
        Tool("fillet_curve", "Fillet all corners of a (poly)curve with the given radius.",
            ("id*", "string", "Curve GUID"),
            ("radius*", "number", "Fillet radius"),
            ("keep_input", "boolean", "Keep source curve (default true)")),
        Tool("transform_objects", "Move/rotate/scale/mirror/copy objects.",
            ("ids*", "any", IdsDesc),
            ("op*", "string", "move|rotate|scale|mirror|copy"),
            ("translation", "any", "[dx,dy,dz] (move/copy; copy default is in place)"),
            ("angle_deg", "number", "Rotation angle (rotate)"),
            ("axis", "any", "Rotation axis [x,y,z] (default Z)"),
            ("center", "any", "Rotation/scale center [x,y,z] (default origin)"),
            ("factor", "any", "Scale factor: number or [fx,fy,fz]"),
            ("plane_origin", "any", "Mirror plane point (default origin)"),
            ("plane_normal", "any", "Mirror plane normal (default X axis)"),
            ("copy", "boolean", "Create transformed copies instead of modifying (default true only for op=copy)")),
        Tool("get_geometry", "Return type, control points / polyline vertices, and bounding box for objects.",
            ("ids*", "any", IdsDesc),
            ("max_points", "integer", "Cap on points returned per object (default 1000)")),
        Tool("select_objects", "Select objects by GUID.",
            ("ids*", "any", IdsDesc),
            ("replace", "boolean", "Clear current selection first (default true)")),
        Tool("clear_selection", "Clear the current selection."),
        Tool("list_layers", "List layers with color, visibility, lock state and object counts."),
        Tool("create_layer", "Create a layer (nested path with ::) with optional color/visible/locked.",
            ("name*", "string", "Layer full path"),
            ("color", "string", "#RRGGBB"),
            ("visible", "boolean", "Visibility"),
            ("locked", "boolean", "Locked state")),
        Tool("set_layer_props", "Modify layer color/visible/locked.",
            ("name*", "string", "Layer full path"),
            ("color", "string", "#RRGGBB"),
            ("visible", "boolean", "Visibility"),
            ("locked", "boolean", "Locked state")),
        Tool("create_block_definition", "Create a block (instance definition) from objects.",
            ("name*", "string", "Block name"),
            ("ids*", "any", IdsDesc),
            ("base_point", "any", "[x,y,z] insertion base (default origin)"),
            ("description", "string", "Optional description"),
            ("delete_input", "boolean", "Delete source objects (default false)")),
        Tool("insert_block", "Insert an instance of a block definition.",
            ("name*", "string", "Block name"),
            ("at", "any", "[x,y,z] insertion point (default origin)"),
            ("scale", "any", "Number or [sx,sy,sz] (default 1)"),
            ("rotation_deg_z", "number", "Rotation about Z in degrees (default 0)"),
            ("layer", "string", "Optional layer"),
            ("tags", "object", "Optional user-text tags")),
        Tool("list_blocks", "List block definitions with instance counts."),
        Tool("set_document_units", "Set document unit system (e.g. Millimeters, Meters, Feet, Inches).",
            ("unit_system*", "string", "Unit system name or alias (mm, cm, m, in, ft)"),
            ("scale_existing", "boolean", "Scale existing geometry to the new unit (default false)")),
        Tool("get_document_units", "Get document unit system and tolerances."),
        Tool("run_rhino_command", "Run a Rhino command script string via RhinoApp.RunScript (escape hatch).",
            ("script*", "string", "Command script, e.g. \"_-Line 0,0,0 10,0,0 _Enter\""),
            ("echo", "boolean", "Echo to command line (default false)")),
        Tool("execute_csharp", "Not available (no Roslyn in-process); use run_rhino_command or the dedicated tools.",
            ("code", "string", "Ignored")),
    ];

    public static string? TryCall(string name, JsonObject args) => name switch
    {
        "create_polyline" => CreatePolyline(args),
        "create_rectangle" => CreateRectangle(args),
        "create_arc" => CreateArc(args),
        "create_curve" => CreateCurve(args),
        "extrude_curve" => ExtrudeCurve(args),
        "boolean_difference" => Boolean(args, "difference"),
        "boolean_union" => Boolean(args, "union"),
        "boolean_intersection" => Boolean(args, "intersection"),
        "offset_curve" => OffsetCurve(args),
        "fillet_curve" => FilletCurve(args),
        "transform_objects" => TransformObjects(args),
        "get_geometry" => GetGeometry(args),
        "select_objects" => SelectObjects(args),
        "clear_selection" => ClearSelection(),
        "list_layers" => ListLayers(),
        "create_layer" => CreateLayer(args),
        "set_layer_props" => SetLayerProps(args),
        "create_block_definition" => CreateBlockDefinition(args),
        "insert_block" => InsertBlock(args),
        "list_blocks" => ListBlocks(),
        "set_document_units" => SetDocumentUnits(args),
        "get_document_units" => GetDocumentUnits(),
        "run_rhino_command" => RunRhinoCommand(args),
        "execute_csharp" => ExecuteCsharp(),
        _ => null,
    };

    // ---- Curves -----------------------------------------------------------------

    private static Guid AddCurve(RhinoDoc doc, Curve crv, JsonObject args)
    {
        var id = doc.Objects.AddCurve(crv, BuildAttributes(doc, args));
        if (id == Guid.Empty) throw new InvalidOperationException("Failed to add curve to document.");
        ApplyGroup(doc, id, args);
        return id;
    }

    private static string CreatePolyline(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var pts = ParsePoints(args["points"]);
        if (pts.Count < 2) throw new ArgumentException("A polyline needs at least 2 points.");
        if (Bool(args, "closed") && pts.Count > 2 && pts[0].DistanceTo(pts[^1]) > doc.ModelAbsoluteTolerance)
            pts.Add(pts[0]);
        var crv = new PolylineCurve(pts);
        if (!crv.IsValid) throw new ArgumentException("Invalid polyline.");
        var id = AddCurve(doc, crv, args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "curve", closed = crv.IsClosed, point_count = pts.Count });
    });

    private static string CreateRectangle(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        Rectangle3d rect;
        if (args["corner_min"] is not null || args["corner_max"] is not null)
        {
            var a = ParsePoint(args["corner_min"], "corner_min");
            var b = ParsePoint(args["corner_max"], "corner_max");
            var plane = new Plane(a, Vector3d.XAxis, Vector3d.YAxis);
            rect = new Rectangle3d(plane, a, new Point3d(b.X, b.Y, a.Z));
        }
        else
        {
            var w = Num(args, "width", 0);
            var h = Num(args, "height", 0);
            if (w <= 0 || h <= 0) throw new ArgumentException("width and height must be positive (or pass corner_min/corner_max).");
            var origin = ParsePointOr(args, "origin", Point3d.Origin);
            var plane = new Plane(origin, Vector3d.XAxis, Vector3d.YAxis);
            var rot = Num(args, "rotation_deg");
            if (Math.Abs(rot) > 1e-12) plane.Rotate(RhinoMath.ToRadians(rot), Vector3d.ZAxis, origin);
            rect = Bool(args, "centered")
                ? new Rectangle3d(plane, new Interval(-w / 2, w / 2), new Interval(-h / 2, h / 2))
                : new Rectangle3d(plane, new Interval(0, w), new Interval(0, h));
        }
        if (!rect.IsValid) throw new ArgumentException("Invalid rectangle dimensions.");
        var id = AddCurve(doc, new PolylineCurve(rect.ToPolyline()), args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "curve", width = rect.Width, height = rect.Height, area = rect.Area });
    });

    private static string CreateArc(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var center = ParsePointOr(args, "center", Point3d.Origin);
        var radius = Num(args, "radius", 0);
        if (radius <= 0) throw new ArgumentException("radius must be positive.");
        var start = Num(args, "start_angle_deg", 0);
        var end = Num(args, "end_angle_deg", 90);
        var sweep = end - start;
        while (sweep <= 0) sweep += 360;

        var plane = new Plane(center, Vector3d.XAxis, Vector3d.YAxis);
        plane.Rotate(RhinoMath.ToRadians(start), Vector3d.ZAxis, center);
        Curve crv = sweep >= 360 - 1e-9
            ? new ArcCurve(new Circle(plane, radius))
            : new ArcCurve(new Arc(plane, radius, RhinoMath.ToRadians(sweep)));
        var id = AddCurve(doc, crv, args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "curve", sweep_deg = Math.Min(sweep, 360), length = crv.GetLength() });
    });

    private static string CreateCurve(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var pts = ParsePoints(args["points"]);
        if (pts.Count < 2) throw new ArgumentException("A curve needs at least 2 points.");
        if (Bool(args, "closed") && pts.Count > 2) pts.Add(pts[0]);
        var degree = Math.Clamp((int)Num(args, "degree", 3), 1, 11);
        degree = Math.Min(degree, pts.Count - 1);

        Curve? crv = null;
        var method = "interpolated";
        try { crv = Curve.CreateInterpolatedCurve(pts, degree); } catch { /* fall back below */ }
        if (crv is null || !crv.IsValid)
        {
            crv = new PolylineCurve(pts);
            method = "polyline";
        }
        var id = AddCurve(doc, crv, args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "curve", method, degree = crv.Degree });
    });

    private static Curve RequireCurve(RhinoDoc doc, Guid id) =>
        FindObject(doc, id).Geometry as Curve ?? throw new ArgumentException($"Object {id} is not a curve.");

    private static string ExtrudeCurve(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var id = IdArg(args, "id");
        var obj = FindObject(doc, id);
        var crv = obj.Geometry as Curve ?? throw new ArgumentException($"Object {id} is not a curve.");

        Vector3d dir;
        if (args["direction"] is not null)
            dir = new Vector3d(ParsePoint(args["direction"], "direction"));
        else if (args["height"] is not null)
            dir = new Vector3d(0, 0, Num(args, "height"));
        else
            throw new ArgumentException("Provide direction [dx,dy,dz] or height.");
        if (dir.Length < 1e-12) throw new ArgumentException("Extrusion vector must be non-zero.");

        var brep = ExtrudeProfile(crv, dir, doc.ModelAbsoluteTolerance, Bool(args, "cap", true))
            ?? throw new InvalidOperationException("Extrusion failed.");
        var attrs = BuildAttributes(doc, args);
        if (string.IsNullOrWhiteSpace(Str(args, "layer"))) attrs.LayerIndex = obj.Attributes.LayerIndex;
        var newId = doc.Objects.AddBrep(brep, attrs);
        ApplyGroup(doc, newId, args);
        if (!Bool(args, "keep_input", true)) doc.Objects.Delete(id, true);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = newId.ToString(), type = "brep", is_solid = brep.IsSolid });
    });

    private static string OffsetCurve(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var id = IdArg(args, "id");
        var obj = FindObject(doc, id);
        var crv = RequireCurve(doc, id);
        var distance = Num(args, "distance");
        if (Math.Abs(distance) < 1e-12) throw new ArgumentException("distance must be non-zero.");

        var style = (Str(args, "corner_style") ?? "sharp").ToLowerInvariant() switch
        {
            "round" => CurveOffsetCornerStyle.Round,
            "smooth" => CurveOffsetCornerStyle.Smooth,
            "chamfer" => CurveOffsetCornerStyle.Chamfer,
            "sharp" => CurveOffsetCornerStyle.Sharp,
            var s => throw new ArgumentException($"Unknown corner_style: {s}"),
        };

        Plane plane;
        if (crv.TryGetPlane(out var p, doc.ModelAbsoluteTolerance) && Math.Abs(Math.Abs(p.ZAxis.Z) - 1) < 1e-6)
            plane = new Plane(p.Origin, Vector3d.XAxis, Vector3d.YAxis);
        else if (crv.TryGetPlane(out p, doc.ModelAbsoluteTolerance))
            plane = p;
        else
            plane = Plane.WorldXY;

        var result = crv.Offset(plane, distance, doc.ModelAbsoluteTolerance, style);
        if (result is null || result.Length == 0)
            throw new InvalidOperationException("Offset failed (curve may be non-planar or distance too large for the shape).");

        var ids = new List<string>();
        foreach (var c in result)
        {
            var attrs = obj.Attributes.Duplicate();
            ids.Add(doc.Objects.AddCurve(c, attrs).ToString());
        }
        if (!Bool(args, "keep_input", true)) doc.Objects.Delete(id, true);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { ids, count = ids.Count });
    });

    private static string FilletCurve(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var id = IdArg(args, "id");
        var obj = FindObject(doc, id);
        var crv = RequireCurve(doc, id);
        var radius = Num(args, "radius");
        if (radius <= 0) throw new ArgumentException("radius must be positive.");

        var result = Curve.CreateFilletCornersCurve(crv, radius, doc.ModelAbsoluteTolerance, doc.ModelAngleToleranceRadians);
        if (result is null)
            throw new InvalidOperationException(
                "Fillet failed: the curve needs at least two joined segments (polyline/lines/arcs) and a radius small enough to fit every corner.");

        var newId = doc.Objects.AddCurve(result, obj.Attributes.Duplicate());
        if (!Bool(args, "keep_input", true)) doc.Objects.Delete(id, true);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = newId.ToString(), type = "curve", length = result.GetLength() });
    });

    // ---- Solids -----------------------------------------------------------------

    private static string Boolean(JsonObject args, string op) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var tol = doc.ModelAbsoluteTolerance;

        List<Guid> a, b;
        if (args["a"] is not null || args["b"] is not null)
        {
            a = IdList(args["a"], "a");
            b = args["b"] is null ? [] : IdList(args["b"], "b");
        }
        else
        {
            var all = IdList(args["ids"], "ids");
            if (all.Count < 2) throw new ArgumentException("Provide a and b, or at least two ids.");
            if (op == "union") { a = all; b = []; }
            else { a = [all[0]]; b = all.Skip(1).ToList(); }
        }
        if (op == "union") { a = a.Concat(b).ToList(); b = []; }
        else if (b.Count == 0) throw new ArgumentException("b is required for " + op + ".");
        if (a.Count == 0 || (op == "union" && a.Count < 2)) throw new ArgumentException("Not enough input objects.");

        List<Brep> ToBreps(IEnumerable<Guid> ids) => ids.Select(i =>
            ToBrep(FindObject(doc, i).Geometry) ?? throw new ArgumentException($"Object {i} is not a Brep/extrusion/surface.")).ToList();

        var A = ToBreps(a);
        var B = ToBreps(b);
        var results = op switch
        {
            "union" => Brep.CreateBooleanUnion(A, tol),
            "difference" => Brep.CreateBooleanDifference(A, B, tol),
            _ => Brep.CreateBooleanIntersection(A, B, tol),
        };
        if (results is null || results.Length == 0)
            throw new InvalidOperationException(
                $"Boolean {op} produced no result. Inputs must be closed, valid solids that overlap" +
                (op == "intersection" ? " (empty intersection is also possible)." : "."));

        var first = FindObject(doc, a[0]);
        var ids2 = new List<string>();
        foreach (var r in results)
        {
            var attrs = first.Attributes.Duplicate();
            var nm = Str(args, "name"); if (!string.IsNullOrWhiteSpace(nm)) attrs.Name = nm;
            var ly = Str(args, "layer"); if (!string.IsNullOrWhiteSpace(ly)) attrs.LayerIndex = EnsureLayer(doc, ly);
            foreach (var (k, v) in ParseTags(args)) attrs.SetUserString(k, v);
            ids2.Add(doc.Objects.AddBrep(r, attrs).ToString());
        }
        if (!Bool(args, "keep_inputs"))
            foreach (var id in a.Concat(b).Distinct()) doc.Objects.Delete(id, true);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { op, ids = ids2, count = ids2.Count });
    });

    // ---- Transform / inspect / select ----------------------------------------------

    private static string TransformObjects(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var ids = IdsArg(args);
        var op = (Str(args, "op") ?? throw new ArgumentException("op required")).ToLowerInvariant();
        var center = ParsePointOr(args, "center", Point3d.Origin);

        Transform xf;
        switch (op)
        {
            case "move":
            case "copy":
                xf = Transform.Translation(new Vector3d(ParsePointOr(args, "translation", Point3d.Origin)));
                break;
            case "rotate":
            {
                var axis = args["axis"] is null ? Vector3d.ZAxis : new Vector3d(ParsePoint(args["axis"], "axis"));
                if (axis.Length < 1e-12) throw new ArgumentException("axis must be non-zero.");
                xf = Transform.Rotation(RhinoMath.ToRadians(Num(args, "angle_deg")), axis, center);
                break;
            }
            case "scale":
            {
                var f = Unwrap(args["factor"]) ?? throw new ArgumentException("factor required for scale.");
                if (f is JsonArray fa)
                {
                    if (fa.Count != 3) throw new ArgumentException("factor array must be [fx,fy,fz].");
                    xf = Transform.Scale(new Plane(center, Vector3d.XAxis, Vector3d.YAxis),
                        ToDouble(fa[0]), ToDouble(fa[1]), ToDouble(fa[2]));
                }
                else
                {
                    var s = ToDouble(f);
                    if (Math.Abs(s) < 1e-12) throw new ArgumentException("factor must be non-zero.");
                    xf = Transform.Scale(center, s);
                }
                break;
            }
            case "mirror":
            {
                var origin = ParsePointOr(args, "plane_origin", Point3d.Origin);
                var normal = args["plane_normal"] is null ? Vector3d.XAxis : new Vector3d(ParsePoint(args["plane_normal"], "plane_normal"));
                if (normal.Length < 1e-12) throw new ArgumentException("plane_normal must be non-zero.");
                xf = Transform.Mirror(origin, normal);
                break;
            }
            default:
                throw new ArgumentException($"Unknown op: {op} (move|rotate|scale|mirror|copy)");
        }

        var copy = op == "copy" || Bool(args, "copy");
        var results = new List<object>();
        var missing = new List<string>();
        foreach (var id in ids)
        {
            var newId = doc.Objects.FindId(id) is null ? Guid.Empty : doc.Objects.Transform(id, xf, !copy);
            if (newId == Guid.Empty) missing.Add(id.ToString());
            else results.Add(new { source = id.ToString(), id = newId.ToString() });
        }
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { op, copy, objects = results, missing });
    });

    private static string GetGeometry(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var ids = IdsArg(args);
        var maxPts = Math.Max(1, (int)Num(args, "max_points", 1000));
        var items = new List<object>();
        var missing = new List<string>();
        foreach (var id in ids)
        {
            var obj = doc.Objects.FindId(id);
            if (obj is null) { missing.Add(id.ToString()); continue; }
            var g = obj.Geometry;
            var type = Classify(g);
            var bbox = g.GetBoundingBox(true);
            object? detail = null;
            switch (g)
            {
                case Curve crv:
                {
                    object? pts = null;
                    var kind = "nurbs";
                    if (crv.TryGetPolyline(out var pl))
                    {
                        kind = "polyline";
                        pts = pl.Take(maxPts).Select(Pt).ToArray();
                    }
                    else
                    {
                        var nc = crv.ToNurbsCurve();
                        if (nc is not null)
                            pts = Enumerable.Range(0, Math.Min(nc.Points.Count, maxPts)).Select(i => Pt(nc.Points[i].Location)).ToArray();
                    }
                    detail = new
                    {
                        curve_kind = kind,
                        is_closed = crv.IsClosed,
                        is_planar = crv.IsPlanar(),
                        degree = crv.Degree,
                        length = crv.GetLength(),
                        start = Pt(crv.PointAtStart),
                        end = Pt(crv.PointAtEnd),
                        points = pts,
                    };
                    break;
                }
                case Brep or Extrusion:
                {
                    var b = ToBrep(g);
                    detail = b is null ? null : new
                    {
                        face_count = b.Faces.Count,
                        vertex_count = b.Vertices.Count,
                        is_solid = b.IsSolid,
                        area = AreaMassProperties.Compute(b)?.Area,
                        volume = b.IsSolid ? VolumeMassProperties.Compute(b)?.Volume : null,
                        vertices = b.Vertices.Take(maxPts).Select(v => Pt(v.Location)).ToArray(),
                    };
                    break;
                }
                case Mesh m:
                    detail = new { vertex_count = m.Vertices.Count, face_count = m.Faces.Count, is_closed = m.IsClosed };
                    break;
                case Rhino.Geometry.Point pt:
                    detail = new { location = Pt(pt.Location) };
                    break;
            }
            items.Add(new
            {
                id = id.ToString(),
                type,
                name = obj.Name ?? "",
                layer = doc.Layers[obj.Attributes.LayerIndex]?.FullPath ?? "",
                bbox = BBoxObj(bbox),
                geometry = detail,
            });
        }
        return JsonSerializer.Serialize(new { objects = items, missing });
    });

    private static string SelectObjects(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var ids = IdsArg(args);
        if (Bool(args, "replace", true)) doc.Objects.UnselectAll();
        var selected = new List<string>();
        var missing = new List<string>();
        foreach (var id in ids)
        {
            var obj = doc.Objects.FindId(id);
            if (obj is null) { missing.Add(id.ToString()); continue; }
            obj.Select(true);
            selected.Add(id.ToString());
        }
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { selected, missing });
    });

    private static string ClearSelection() => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var n = doc.Objects.UnselectAll();
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { cleared = n });
    });

    // ---- Layers --------------------------------------------------------------------

    private static object LayerInfo(RhinoDoc doc, Layer l) => new
    {
        name = l.Name,
        full_path = l.FullPath,
        index = l.Index,
        color = ColorHex(l.Color),
        visible = l.IsVisible,
        locked = l.IsLocked,
        object_count = doc.Objects.FindByLayer(l)?.Length ?? 0,
    };

    private static string ListLayers() => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var layers = doc.Layers.Where(l => l is not null && !l.IsDeleted).Select(l => LayerInfo(doc, l)).ToList();
        return JsonSerializer.Serialize(new { count = layers.Count, current = doc.Layers.CurrentLayer?.FullPath, layers });
    });

    private static void ApplyLayerProps(RhinoDoc doc, int index, JsonObject args)
    {
        var layer = doc.Layers[index];
        var color = Str(args, "color");
        if (!string.IsNullOrWhiteSpace(color)) layer.Color = ParseColor(color);
        if (args["visible"] is not null) layer.IsVisible = Bool(args, "visible", true);
        if (args["locked"] is not null) layer.IsLocked = Bool(args, "locked");
        doc.Layers.Modify(layer, index, true);
    }

    private static string CreateLayer(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var name = Str(args, "name") ?? throw new ArgumentException("name required");
        var existed = doc.Layers.FindByFullPath(name, -1) >= 0;
        var idx = EnsureLayer(doc, name);
        if (idx < 0) throw new InvalidOperationException($"Failed to create layer {name}.");
        ApplyLayerProps(doc, idx, args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { created = !existed, layer = LayerInfo(doc, doc.Layers[idx]) });
    });

    private static string SetLayerProps(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var name = Str(args, "name") ?? throw new ArgumentException("name required");
        var idx = doc.Layers.FindByFullPath(name, -1);
        if (idx < 0) throw new ArgumentException($"Layer not found: {name}");
        ApplyLayerProps(doc, idx, args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { layer = LayerInfo(doc, doc.Layers[idx]) });
    });

    // ---- Blocks --------------------------------------------------------------------

    private static string CreateBlockDefinition(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var name = Str(args, "name") ?? throw new ArgumentException("name required");
        if (doc.InstanceDefinitions.Find(name) is not null)
            throw new ArgumentException($"Block definition already exists: {name}");
        var ids = IdsArg(args);
        var objs = ids.Select(i => FindObject(doc, i)).ToList();
        var geoms = objs.Select(o => o.Geometry.Duplicate()).ToList();
        var attrs = objs.Select(o => o.Attributes.Duplicate()).ToList();
        var basePt = ParsePointOr(args, "base_point", Point3d.Origin);
        var idx = doc.InstanceDefinitions.Add(name, Str(args, "description") ?? "", basePt, geoms, attrs);
        if (idx < 0) throw new InvalidOperationException("Failed to create block definition.");
        if (Bool(args, "delete_input"))
            foreach (var id in ids) doc.Objects.Delete(id, true);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { name, index = idx, object_count = objs.Count });
    });

    private static string InsertBlock(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var name = Str(args, "name") ?? throw new ArgumentException("name required");
        var def = doc.InstanceDefinitions.Find(name) ?? throw new ArgumentException($"Block definition not found: {name}");
        var at = ParsePointOr(args, "at", Point3d.Origin);

        var sx = 1.0; var sy = 1.0; var sz = 1.0;
        if (Unwrap(args["scale"]) is { } sn)
        {
            if (sn is JsonArray sa && sa.Count == 3) { sx = ToDouble(sa[0]); sy = ToDouble(sa[1]); sz = ToDouble(sa[2]); }
            else { sx = sy = sz = ToDouble(sn); }
        }
        var xf = Transform.Translation(at.X, at.Y, at.Z)
                 * Transform.Rotation(RhinoMath.ToRadians(Num(args, "rotation_deg_z")), Vector3d.ZAxis, Point3d.Origin)
                 * Transform.Scale(Plane.WorldXY, sx, sy, sz);
        var id = doc.Objects.AddInstanceObject(def.Index, xf, BuildAttributes(doc, args));
        if (id == Guid.Empty) throw new InvalidOperationException("Failed to insert block.");
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), name, at = Pt(at) });
    });

    private static string ListBlocks() => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var blocks = doc.InstanceDefinitions.GetList(true).Select(d => new
        {
            name = d.Name,
            index = d.Index,
            description = d.Description,
            object_count = d.ObjectCount,
            instance_count = d.GetReferences(0)?.Length ?? 0,
        }).ToList();
        return JsonSerializer.Serialize(new { count = blocks.Count, blocks });
    });

    // ---- Units -----------------------------------------------------------------------

    private static readonly Dictionary<string, UnitSystem> UnitAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mm"] = UnitSystem.Millimeters, ["cm"] = UnitSystem.Centimeters, ["m"] = UnitSystem.Meters,
        ["km"] = UnitSystem.Kilometers, ["in"] = UnitSystem.Inches, ["inch"] = UnitSystem.Inches,
        ["ft"] = UnitSystem.Feet, ["foot"] = UnitSystem.Feet, ["yd"] = UnitSystem.Yards, ["mi"] = UnitSystem.Miles,
    };

    private static string SetDocumentUnits(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var s = Str(args, "unit_system") ?? throw new ArgumentException("unit_system required");
        if (!UnitAliases.TryGetValue(s.Trim(), out var us) && !Enum.TryParse(s.Trim(), true, out us))
            throw new ArgumentException($"Unknown unit system: {s}");
        var prev = doc.ModelUnitSystem;
        var scale = Bool(args, "scale_existing");
        doc.ModelUnitSystem = us;
        // AdjustModelUnitSystem API varies by Rhino build; assign ModelUnitSystem directly.
        _ = scale;
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { previous = prev.ToString(), unit_system = doc.ModelUnitSystem.ToString(), scaled_existing = scale });
    });

    private static string GetDocumentUnits() => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        return JsonSerializer.Serialize(new
        {
            unit_system = doc.ModelUnitSystem.ToString(),
            absolute_tolerance = doc.ModelAbsoluteTolerance,
            angle_tolerance_deg = doc.ModelAngleToleranceDegrees,
            units_per_meter = RhinoMath.UnitScale(UnitSystem.Meters, doc.ModelUnitSystem),
        });
    });

    // ---- Escape hatches ----------------------------------------------------------------

    private static string RunRhinoCommand(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var script = Str(args, "script") ?? throw new ArgumentException("script required");
        var before = doc.Objects.Count;
        var ok = RhinoApp.RunScript(script, Bool(args, "echo"));
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { ok, script, object_count_before = before, object_count_after = doc.Objects.Count });
    });

    private static string ExecuteCsharp() => throw new InvalidOperationException(
        "execute_csharp is not available: in-process C# scripting (Roslyn) is not bundled with MCP4Rhino to avoid " +
        "conflicts with Rhino's own assemblies. Use run_rhino_command (RhinoApp.RunScript) or the dedicated geometry tools.");
}
