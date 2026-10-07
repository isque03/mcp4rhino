using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Host;
using MCP4Rhino.Logic;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using static MCP4Rhino.Tools.SurfaceBrepHelpers;
using static MCP4Rhino.Tools.ToolHelpers;

namespace MCP4Rhino.Tools;

/// <summary>
/// P0 NURBS surface create/edit tools (Loft/Sweep/Revolve/Fillet/Blend/Offset/Trim/Join + edge helpers).
/// </summary>
[ExcludeFromCodeCoverage]
internal static class SurfaceFoundationTools
{
    private const string IdsDesc = "GUIDs: comma-separated string or JSON array";

    public static object[] ListTools() => Tools;

    public static readonly object[] Tools =
    [
        ToolC("create_plane_surface", "Create a rectangular planar surface (Rhino Plane).",
            ("origin", "any", "Corner [x,y,z] (default origin)"),
            ("width", "number", "Size along plane X (default 1)"),
            ("height", "number", "Size along plane Y (default 1)"),
            ("normal", "any", "Plane normal [x,y,z] (default +Z when omitted)"),
            ("centered", "boolean", "Treat origin as center (default false)")),
        ToolC("create_srf_pt", "Create a surface from 3 or 4 corner points (Rhino SrfPt).",
            ("points*", "array", "3 or 4 [x,y,z] corners in order")),
        Tool("create_planar_surface", "Create planar surface(s) from closed planar curves (Rhino PlanarSrf).",
            ("ids*", "any", IdsDesc),
            ("keep_inputs", "boolean", "Keep input curves (default true)")),
        Tool("create_edge_surface", "Create a surface from 2–4 edge curves (Rhino EdgeSrf).",
            ("ids*", "any", IdsDesc),
            ("keep_inputs", "boolean", "Keep input curves (default true)")),
        Tool("loft_surface", "Loft a surface through ordered profile curves (Rhino Loft).",
            ("ids*", "any", IdsDesc),
            ("style", "string", "normal|loose|straight|tight|uniform (default normal)"),
            ("closed", "boolean", "Closed loft (default false)"),
            ("keep_inputs", "boolean", "Keep input curves (default true)")),
        Tool("sweep1_surface", "Sweep sections along one rail (Rhino Sweep1).",
            ("rail_id*", "string", "Rail curve GUID"),
            ("section_ids*", "any", IdsDesc),
            ("closed", "boolean", "Closed sweep (default false)"),
            ("keep_inputs", "boolean", "Keep input curves (default true)")),
        Tool("sweep2_surface", "Sweep sections along two rails (Rhino Sweep2).",
            ("rail_a_id*", "string", "First rail GUID"),
            ("rail_b_id*", "string", "Second rail GUID"),
            ("section_ids*", "any", IdsDesc),
            ("closed", "boolean", "Closed sweep (default false)"),
            ("keep_inputs", "boolean", "Keep input curves (default true)")),
        Tool("revolve_surface", "Revolve a profile about an axis (Rhino Revolve).",
            ("profile_id*", "string", "Profile curve GUID"),
            ("axis_start*", "any", "[x,y,z] axis start"),
            ("axis_end*", "any", "[x,y,z] axis end"),
            ("angle_deg", "number", "Revolution angle degrees (default 360)"),
            ("keep_input", "boolean", "Keep profile curve (default true)")),
        Tool("rail_revolve_surface", "Revolve a profile about an axis while following a rail (Rhino RailRevolve).",
            ("profile_id*", "string", "Profile curve GUID"),
            ("rail_id*", "string", "Rail curve GUID"),
            ("axis_start*", "any", "[x,y,z]"),
            ("axis_end*", "any", "[x,y,z]"),
            ("scale_height", "boolean", "Scale height (default false)"),
            ("keep_inputs", "boolean", "Keep input curves (default true)")),
        Tool("network_surface", "Create a surface from a U/V curve network (Rhino NetworkSrf).",
            ("u_ids*", "any", "U-direction curve GUIDs"),
            ("v_ids*", "any", "V-direction curve GUIDs"),
            ("continuity", "string", "position|tangency|curvature for edge match (default tangency)"),
            ("keep_inputs", "boolean", "Keep input curves (default true)")),
        Tool("patch_surface", "Fit a surface through curves and/or points (Rhino Patch).",
            ("ids*", "any", "Curve and/or point object GUIDs"),
            ("u_spans", "integer", "U spans (default 10)"),
            ("v_spans", "integer", "V spans (default 10)"),
            ("keep_inputs", "boolean", "Keep inputs (default true)")),
        Tool("pipe_surface", "Create a pipe surface along a curve (Rhino Pipe).",
            ("curve_id*", "string", "Center curve GUID"),
            ("radius*", "number", "Pipe radius"),
            ("cap", "boolean", "Cap ends (default true)"),
            ("keep_input", "boolean", "Keep center curve (default true)")),
        Tool("extrude_curve_along_curve", "Extrude a profile along a path via CreateFromSweep (Rhino ExtrudeCrvAlongCrv).",
            ("profile_id*", "string", "Profile curve GUID"),
            ("path_id*", "string", "Path curve GUID"),
            ("cap", "boolean", "Cap if closed planar profile (default true)"),
            ("keep_inputs", "boolean", "Keep input curves (default true)")),
        Tool("fillet_surfaces", "Constant-radius fillet between two surfaces (Rhino FilletSrf).",
            ("surface_a_id*", "string", "First surface/brep GUID"),
            ("surface_b_id*", "string", "Second surface/brep GUID"),
            ("radius*", "number", "Fillet radius"),
            ("pick_point_a", "any", "[x,y,z] near the fillet face on A (required if A is multi-face)"),
            ("pick_point_b", "any", "[x,y,z] near the fillet face on B (required if B is multi-face)"),
            ("face_index_a", "integer", "Face index on A (alternative to pick_point_a)"),
            ("face_index_b", "integer", "Face index on B (alternative to pick_point_b)"),
            ("extend", "boolean", "Extend fillet (default true)"),
            ("keep_inputs", "boolean", "Keep input surfaces (default true); false trims and replaces")),
        Tool("blend_surfaces", "Blend surface between two surface edges (Rhino BlendSrf).",
            ("surface_a_id*", "string", "First surface/brep GUID"),
            ("surface_b_id*", "string", "Second surface/brep GUID"),
            ("edge_index_a", "integer", "Edge index on A (from list_surface_edges)"),
            ("edge_index_b", "integer", "Edge index on B"),
            ("pick_point_a", "any", "[x,y,z] alternative to edge_index_a"),
            ("pick_point_b", "any", "[x,y,z] alternative to edge_index_b"),
            ("continuity", "string", "position|tangency|curvature (default tangency)"),
            ("keep_inputs", "boolean", "Keep input surfaces (default true)")),
        Tool("chamfer_surfaces", "Chamfer (ruled) surface between two surfaces (Rhino ChamferSrf).",
            ("surface_a_id*", "string", "First surface/brep GUID"),
            ("surface_b_id*", "string", "Second surface/brep GUID"),
            ("distance_a*", "number", "Distance along A"),
            ("distance_b", "number", "Distance along B (default = distance_a)"),
            ("pick_point_a", "any", "[x,y,z] near chamfer face on A (required if A is multi-face)"),
            ("pick_point_b", "any", "[x,y,z] near chamfer face on B (required if B is multi-face)"),
            ("face_index_a", "integer", "Face index on A (alternative to pick_point_a)"),
            ("face_index_b", "integer", "Face index on B (alternative to pick_point_b)"),
            ("keep_inputs", "boolean", "Keep input surfaces (default true); false trims and replaces")),
        Tool("offset_surface", "Offset a surface/brep (Rhino OffsetSrf).",
            ("id*", "string", "Surface/brep GUID"),
            ("distance*", "number", "Offset distance (non-zero)"),
            ("solid", "boolean", "Create solid offset (default false)"),
            ("keep_input", "boolean", "Keep original (default true)")),
        Tool("trim_surface", "Trim a surface/brep with a planar curve or surface cutter; keep piece near keep_point.",
            ("id*", "string", "Surface/brep GUID"),
            ("cutter_id*", "string", "Planar cutting curve or surface/brep GUID"),
            ("keep_point*", "any", "[x,y,z] on the piece to keep"),
            ("keep_input", "boolean", "Keep original (default false)")),
        Tool("split_surface", "Split a surface/brep with a planar curve or surface cutter.",
            ("id*", "string", "Surface/brep GUID"),
            ("cutter_id*", "string", "Planar cutting curve or surface/brep GUID"),
            ("keep_input", "boolean", "Keep original (default false)")),
        Tool("join_surfaces", "Join surfaces/breps into a polysurface (Rhino Join).",
            ("ids*", "any", IdsDesc),
            ("tolerance", "number", "Join tolerance (default model absolute)"),
            ("keep_inputs", "boolean", "Keep inputs (default false)")),
        Tool("cap_planar_holes", "Cap planar holes on a brep/polysurface (Rhino Cap).",
            ("id*", "string", "Brep/polysurface GUID"),
            ("keep_input", "boolean", "Keep original uncapped (default false)")),
        Tool("list_surface_edges", "List edges of a surface/brep with index, endpoints, and length.",
            ("id*", "string", "Surface/brep GUID")),
        Tool("dup_border", "Duplicate the outer border curve(s) of a surface/brep (Rhino DupBorder).",
            ("id*", "string", "Surface/brep GUID")),
        Tool("dup_edge", "Duplicate one edge as a curve (Rhino DupEdge).",
            ("id*", "string", "Surface/brep GUID"),
            ("edge_index", "integer", "Edge index from list_surface_edges"),
            ("pick_point", "any", "[x,y,z] alternative to edge_index")),
        Tool("extract_isocurve", "Extract an isocurve at a U or V parameter (Rhino ExtractIsoCurve).",
            ("id*", "string", "Surface/brep GUID"),
            ("direction*", "string", "u or v"),
            ("parameter*", "number", "Parameter along the direction"),
            ("face_index", "integer", "Face index for polysurfaces (default 0)")),
    ];

    public static string? TryCall(string name, JsonObject args) => name switch
    {
        "create_plane_surface" => CreatePlaneSurface(args),
        "create_srf_pt" => CreateSrfPt(args),
        "create_planar_surface" => CreatePlanarSurface(args),
        "create_edge_surface" => CreateEdgeSurface(args),
        "loft_surface" => LoftSurface(args),
        "sweep1_surface" => Sweep1Surface(args),
        "sweep2_surface" => Sweep2Surface(args),
        "revolve_surface" => RevolveSurface(args),
        "rail_revolve_surface" => RailRevolveSurface(args),
        "network_surface" => NetworkSurface(args),
        "patch_surface" => PatchSurface(args),
        "pipe_surface" => PipeSurface(args),
        "extrude_curve_along_curve" => ExtrudeAlongCurve(args),
        "fillet_surfaces" => FilletSurfaces(args),
        "blend_surfaces" => BlendSurfaces(args),
        "chamfer_surfaces" => ChamferSurfaces(args),
        "offset_surface" => OffsetSurface(args),
        "trim_surface" => TrimSurface(args),
        "split_surface" => SplitSurface(args),
        "join_surfaces" => JoinSurfaces(args),
        "cap_planar_holes" => CapPlanarHoles(args),
        "list_surface_edges" => ListSurfaceEdges(args),
        "dup_border" => DupBorder(args),
        "dup_edge" => DupEdge(args),
        "extract_isocurve" => ExtractIsocurve(args),
        _ => null,
    };

    // ---- create -----------------------------------------------------------------

    private static string CreatePlaneSurface(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var origin = ParsePointOr(args, "origin", Point3d.Origin);
        var w = Num(args, "width", 1);
        var h = Num(args, "height", 1);
        SurfaceOps.RequirePositive(w, "width");
        SurfaceOps.RequirePositive(h, "height");
        var plane = PlaneFromNormal(origin, args);
        var rect = Bool(args, "centered")
            ? new Rectangle3d(plane, new Interval(-w / 2, w / 2), new Interval(-h / 2, h / 2))
            : new Rectangle3d(plane, new Interval(0, w), new Interval(0, h));
        if (!rect.IsValid) throw new ArgumentException("Invalid plane dimensions.");
        var srf = NurbsSurface.CreateFromCorners(rect.Corner(0), rect.Corner(1), rect.Corner(2), rect.Corner(3))
            ?? throw new InvalidOperationException("Failed to create plane surface.");
        var brep = Brep.CreateFromSurface(srf) ?? throw new InvalidOperationException("Failed to convert plane to brep.");
        var id = AddSurfaceResult(doc, brep, args, "plane surface");
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "brep", width = w, height = h });
    });

    private static string CreateSrfPt(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var pts = ParsePoints(args["points"] ?? throw new ArgumentException("points required"), "points");
        if (pts.Count is not (3 or 4)) throw new ArgumentException("create_srf_pt needs 3 or 4 corner points.");
        NurbsSurface? srf = pts.Count == 3
            ? NurbsSurface.CreateFromCorners(pts[0], pts[1], pts[2])
            : NurbsSurface.CreateFromCorners(pts[0], pts[1], pts[2], pts[3]);
        if (srf is null || !srf.IsValid) throw new InvalidOperationException("Failed to create surface from points.");
        var brep = Brep.CreateFromSurface(srf) ?? throw new InvalidOperationException("Failed to convert surface to brep.");
        var id = AddSurfaceResult(doc, brep, args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "brep", corners = pts.Count });
    });

    private static string CreatePlanarSurface(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var ids = IdsArg(args);
        SurfaceOps.RequireCount(ids.Count, 1, "create_planar_surface");
        var curves = ids.Select(i => RequireCurve(doc, i)).ToArray();
        var breps = Brep.CreatePlanarBreps(curves, doc.ModelAbsoluteTolerance);
        if (breps is null || breps.Length == 0)
            throw new InvalidOperationException("PlanarSrf failed — curves must be closed and planar.");
        var outIds = breps.Select(b => AddSurfaceResult(doc, b, args, "planar surface")).ToList();
        MaybeDelete(doc, ids, Bool(args, "keep_inputs", true));
        doc.Views.Redraw();
        return SerializeIds(outIds);
    });

    private static string CreateEdgeSurface(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var ids = IdsArg(args);
        if (ids.Count is < 2 or > 4) throw new ArgumentException("create_edge_surface needs 2–4 curve ids.");
        var curves = ids.Select(i => RequireCurve(doc, i)).ToArray();
        var brep = Brep.CreateEdgeSurface(curves);
        if (brep is null || !brep.IsValid)
            throw new InvalidOperationException("EdgeSrf failed — curves must form a closed loop of 2–4 edges.");
        var id = AddSurfaceResult(doc, brep, args, "edge surface");
        MaybeDelete(doc, ids, Bool(args, "keep_inputs", true));
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "brep" });
    });

    private static string LoftSurface(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var ids = IdsArg(args);
        SurfaceOps.RequireCount(ids.Count, 2, "loft_surface");
        var curves = ids.Select(i => RequireCurve(doc, i)).ToList();
        var style = MapLoft(SurfaceOps.ParseLoftStyle(Str(args, "style")));
        var closed = Bool(args, "closed");
        var breps = Brep.CreateFromLoft(curves, Point3d.Unset, Point3d.Unset, style, closed);
        if (breps is null || breps.Length == 0)
            throw new InvalidOperationException("Loft failed — check curve order and seams.");
        var outIds = breps.Select(b => AddSurfaceResult(doc, b, args, "loft")).ToList();
        MaybeDelete(doc, ids, Bool(args, "keep_inputs", true));
        doc.Views.Redraw();
        return SerializeIds(outIds);
    });

    private static string Sweep1Surface(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var rail = RequireCurve(doc, IdArg(args, "rail_id"));
        var sectionIds = SectionIds(args);
        SurfaceOps.RequireCount(sectionIds.Count, 1, "sweep1_surface");
        var sections = sectionIds.Select(i => RequireCurve(doc, i)).ToArray();
        var breps = Brep.CreateFromSweep(rail, sections, Bool(args, "closed"), doc.ModelAbsoluteTolerance);
        if (breps is null || breps.Length == 0)
            throw new InvalidOperationException("Sweep1 failed.");
        var outIds = breps.Select(b => AddSurfaceResult(doc, b, args, "sweep1")).ToList();
        var all = sectionIds.Append(IdArg(args, "rail_id"));
        MaybeDelete(doc, all, Bool(args, "keep_inputs", true));
        doc.Views.Redraw();
        return SerializeIds(outIds);
    });

    private static string Sweep2Surface(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var railA = RequireCurve(doc, IdArg(args, "rail_a_id"));
        var railB = RequireCurve(doc, IdArg(args, "rail_b_id"));
        var sectionIds = SectionIds(args);
        SurfaceOps.RequireCount(sectionIds.Count, 1, "sweep2_surface");
        var sections = sectionIds.Select(i => RequireCurve(doc, i)).ToArray();
        var breps = Brep.CreateFromSweep(railA, railB, sections, Bool(args, "closed"), doc.ModelAbsoluteTolerance);
        if (breps is null || breps.Length == 0)
            throw new InvalidOperationException("Sweep2 failed.");
        var outIds = breps.Select(b => AddSurfaceResult(doc, b, args, "sweep2")).ToList();
        var all = sectionIds.Append(IdArg(args, "rail_a_id")).Append(IdArg(args, "rail_b_id"));
        MaybeDelete(doc, all, Bool(args, "keep_inputs", true));
        doc.Views.Redraw();
        return SerializeIds(outIds);
    });

    private static string RevolveSurface(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var profileId = IdArg(args, "profile_id");
        var profile = RequireCurve(doc, profileId);
        var a0 = ParsePoint(args["axis_start"] ?? throw new ArgumentException("axis_start required"), "axis_start");
        var a1 = ParsePoint(args["axis_end"] ?? throw new ArgumentException("axis_end required"), "axis_end");
        var axis = new Line(a0, a1);
        if (axis.Length < doc.ModelAbsoluteTolerance)
            throw new ArgumentException("axis_start and axis_end are coincident.");
        var angle = Num(args, "angle_deg", 360);
        if (Math.Abs(angle) < 1e-9) throw new ArgumentException("angle_deg must be non-zero.");
        var rev = RevSurface.Create(profile, axis, 0, RhinoMath.ToRadians(angle))
            ?? throw new InvalidOperationException("Revolve failed.");
        var brep = Brep.CreateFromRevSurface(rev, true, true)
            ?? throw new InvalidOperationException("Failed to convert revolve to brep.");
        var id = AddSurfaceResult(doc, brep, args, "revolve");
        MaybeDelete(doc, [profileId], Bool(args, "keep_input", true));
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "brep", angle_deg = angle });
    });

    private static string RailRevolveSurface(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var profileId = IdArg(args, "profile_id");
        var railId = IdArg(args, "rail_id");
        var profile = RequireCurve(doc, profileId);
        var rail = RequireCurve(doc, railId);
        var a0 = ParsePoint(args["axis_start"] ?? throw new ArgumentException("axis_start required"), "axis_start");
        var a1 = ParsePoint(args["axis_end"] ?? throw new ArgumentException("axis_end required"), "axis_end");
        var axis = new Line(a0, a1);
        if (axis.Length < doc.ModelAbsoluteTolerance)
            throw new ArgumentException("axis_start and axis_end are coincident.");
        var srf = NurbsSurface.CreateRailRevolvedSurface(profile, rail, axis, Bool(args, "scale_height"))
            ?? throw new InvalidOperationException("RailRevolve failed.");
        var brep = Brep.CreateFromSurface(srf) ?? throw new InvalidOperationException("RailRevolve conversion failed.");
        var id = AddSurfaceResult(doc, brep, args, "rail_revolve");
        MaybeDelete(doc, [profileId, railId], Bool(args, "keep_inputs", true));
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "brep" });
    });

    private static string NetworkSurface(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var uIds = IdList(args["u_ids"] ?? throw new ArgumentException("u_ids required"), "u_ids");
        var vIds = IdList(args["v_ids"] ?? throw new ArgumentException("v_ids required"), "v_ids");
        SurfaceOps.RequireCount(uIds.Count, 1, "network_surface u_ids");
        SurfaceOps.RequireCount(vIds.Count, 1, "network_surface v_ids");
        var uCurves = uIds.Select(i => RequireCurve(doc, i)).ToArray();
        var vCurves = vIds.Select(i => RequireCurve(doc, i)).ToArray();
        var cont = (int)SurfaceOps.ParseContinuity(Str(args, "continuity"));
        var srf = NurbsSurface.CreateNetworkSurface(
            uCurves, cont, cont, vCurves, cont, cont,
            doc.ModelAbsoluteTolerance, doc.ModelAbsoluteTolerance, doc.ModelAngleToleranceRadians,
            out var error);
        if (srf is null || !srf.IsValid)
            throw new InvalidOperationException($"NetworkSrf failed (error {error}).");
        var brep = Brep.CreateFromSurface(srf) ?? throw new InvalidOperationException("NetworkSrf conversion failed.");
        var id = AddSurfaceResult(doc, brep, args, "network");
        MaybeDelete(doc, uIds.Concat(vIds), Bool(args, "keep_inputs", true));
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "brep" });
    });

    private static string PatchSurface(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var ids = IdsArg(args);
        SurfaceOps.RequireCount(ids.Count, 1, "patch_surface");
        var geom = ids.Select(i => FindObject(doc, i).Geometry.Duplicate()).ToList();
        var uSpans = Math.Max(1, (int)Num(args, "u_spans", 10));
        var vSpans = Math.Max(1, (int)Num(args, "v_spans", 10));
        var brep = Brep.CreatePatch(geom, uSpans, vSpans, doc.ModelAbsoluteTolerance)
            ?? throw new InvalidOperationException("Patch failed.");
        var id = AddSurfaceResult(doc, brep, args, "patch");
        MaybeDelete(doc, ids, Bool(args, "keep_inputs", true));
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "brep", u_spans = uSpans, v_spans = vSpans });
    });

    private static string PipeSurface(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var curveId = IdArg(args, "curve_id");
        var rail = RequireCurve(doc, curveId);
        var radius = SurfaceOps.RequirePositive(Num(args, "radius"), "radius");
        var cap = Bool(args, "cap", true)
            ? PipeCapMode.Flat
            : PipeCapMode.None;
        var breps = Brep.CreatePipe(
            rail, radius, false, cap, true,
            doc.ModelAbsoluteTolerance, doc.ModelAngleToleranceRadians);
        if (breps is null || breps.Length == 0)
            throw new InvalidOperationException("Pipe failed.");
        var outIds = breps.Select(b => AddSurfaceResult(doc, b, args, "pipe")).ToList();
        MaybeDelete(doc, [curveId], Bool(args, "keep_input", true));
        doc.Views.Redraw();
        return SerializeIds(outIds);
    });

    private static string ExtrudeAlongCurve(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var profileId = IdArg(args, "profile_id");
        var pathId = IdArg(args, "path_id");
        var profile = RequireCurve(doc, profileId);
        var path = RequireCurve(doc, pathId);
        var swept = Brep.CreateFromSweep(path, profile, false, doc.ModelAbsoluteTolerance);
        if (swept is null || swept.Length == 0 || swept[0] is null || !swept[0].IsValid)
            throw new InvalidOperationException("Extrude along curve failed (CreateFromSweep).");
        var brep = swept[0];
        if (Bool(args, "cap", true) && profile.IsClosed)
        {
            var capped = brep.CapPlanarHoles(doc.ModelAbsoluteTolerance);
            if (capped is not null) brep = capped;
        }
        var id = AddSurfaceResult(doc, brep, args, "extrude_along");
        MaybeDelete(doc, [profileId, pathId], Bool(args, "keep_inputs", true));
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "brep", is_solid = brep.IsSolid });
    });

    // ---- edit -------------------------------------------------------------------

    private static string FilletSurfaces(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var idA = IdArg(args, "surface_a_id");
        var idB = IdArg(args, "surface_b_id");
        var a = RequireBrep(doc, idA);
        var b = RequireBrep(doc, idB);
        var radius = SurfaceOps.RequirePositive(Num(args, "radius"), "radius");
        var faceA = ResolveFace(a, args, "pick_point_a", "face_index_a");
        var faceB = ResolveFace(b, args, "pick_point_b", "face_index_b");
        var pickA = args["pick_point_a"] is null ? DefaultPick(a) : ParsePoint(args["pick_point_a"], "pick_point_a");
        var pickB = args["pick_point_b"] is null ? DefaultPick(b) : ParsePoint(args["pick_point_b"], "pick_point_b");
        var uvA = ClosestFaceUv(faceA, pickA);
        var uvB = ClosestFaceUv(faceB, pickB);
        var extend = Bool(args, "extend", true);
        var trim = !Bool(args, "keep_inputs", true);
        Brep[]? fillets;
        Brep[]? trimA = null;
        Brep[]? trimB = null;
        if (trim)
        {
            fillets = Brep.CreateFilletSurface(
                faceA, uvA, faceB, uvB, radius, trim: true, extend, doc.ModelAbsoluteTolerance,
                out trimA, out trimB);
        }
        else
        {
            fillets = Brep.CreateFilletSurface(faceA, uvA, faceB, uvB, radius, extend, doc.ModelAbsoluteTolerance);
        }
        if (fillets is null || fillets.Length == 0)
            throw new InvalidOperationException("FilletSrf failed — surfaces may not meet or radius too large.");
        if (!trim)
        {
            var keepIds = fillets.Select(f => AddSurfaceResult(doc, f, args, "fillet")).ToList();
            doc.Views.Redraw();
            return SerializeIds(keepIds);
        }

        var pieces = SurfaceOps.CollectTrimReplacePieces(fillets, trimA, trimB);
        var outIds = AddBrepsThenMaybeDelete(doc, pieces, args, [idA, idB], deleteInputs: true, "fillet");
        doc.Views.Redraw();
        return SerializeIds(outIds);
    });

    private static string BlendSurfaces(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var idA = IdArg(args, "surface_a_id");
        var idB = IdArg(args, "surface_b_id");
        var a = RequireBrep(doc, idA);
        var b = RequireBrep(doc, idB);
        var eiA = ResolveEdgeIndex(a, args, "edge_index_a", "pick_point_a");
        var eiB = ResolveEdgeIndex(b, args, "edge_index_b", "pick_point_b");
        var edgeA = a.Edges[eiA];
        var edgeB = b.Edges[eiB];
        var faceA = FaceForEdge(a, edgeA);
        var faceB = FaceForEdge(b, edgeB);
        var cont = MapCont(SurfaceOps.ParseContinuity(Str(args, "continuity")));
        var blends = Brep.CreateBlendSurface(
            faceA, edgeA, edgeA.Domain, false, cont,
            faceB, edgeB, edgeB.Domain, false, cont);
        if (blends is null || blends.Length == 0)
            throw new InvalidOperationException("BlendSrf failed — check edge indices / continuity.");
        var outIds = blends.Select(bl => AddSurfaceResult(doc, bl, args, "blend")).ToList();
        MaybeDelete(doc, [idA, idB], Bool(args, "keep_inputs", true));
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { ids = outIds.Select(g => g.ToString()).ToArray(), count = outIds.Count, type = "brep", edge_index_a = eiA, edge_index_b = eiB });
    });

    private static string ChamferSurfaces(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var idA = IdArg(args, "surface_a_id");
        var idB = IdArg(args, "surface_b_id");
        var a = RequireBrep(doc, idA);
        var b = RequireBrep(doc, idB);
        var dA = SurfaceOps.RequirePositive(Num(args, "distance_a"), "distance_a");
        var dB = args["distance_b"] is null ? dA : SurfaceOps.RequirePositive(Num(args, "distance_b"), "distance_b");
        var faceA = ResolveFace(a, args, "pick_point_a", "face_index_a");
        var faceB = ResolveFace(b, args, "pick_point_b", "face_index_b");
        var pickA = args["pick_point_a"] is null ? DefaultPick(a) : ParsePoint(args["pick_point_a"], "pick_point_a");
        var pickB = args["pick_point_b"] is null ? DefaultPick(b) : ParsePoint(args["pick_point_b"], "pick_point_b");
        var uvA = ClosestFaceUv(faceA, pickA);
        var uvB = ClosestFaceUv(faceB, pickB);
        var trim = !Bool(args, "keep_inputs", true);
        Brep[]? chamfers;
        Brep[]? trimA = null;
        Brep[]? trimB = null;
        if (trim)
        {
            chamfers = Brep.CreateChamferSurface(
                faceA, uvA, dA, faceB, uvB, dB, trim: true, extend: true, doc.ModelAbsoluteTolerance,
                out trimA, out trimB);
        }
        else
        {
            chamfers = Brep.CreateChamferSurface(faceA, uvA, dA, faceB, uvB, dB, extend: true, doc.ModelAbsoluteTolerance);
        }
        if (chamfers is null || chamfers.Length == 0)
            throw new InvalidOperationException("ChamferSrf failed.");
        if (!trim)
        {
            var keepIds = chamfers.Select(c => AddSurfaceResult(doc, c, args, "chamfer")).ToList();
            doc.Views.Redraw();
            return SerializeIds(keepIds);
        }

        var pieces = SurfaceOps.CollectTrimReplacePieces(chamfers, trimA, trimB);
        var outIds = AddBrepsThenMaybeDelete(doc, pieces, args, [idA, idB], deleteInputs: true, "chamfer");
        doc.Views.Redraw();
        return SerializeIds(outIds);
    });

    private static string OffsetSurface(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var id = IdArg(args, "id");
        var brep = RequireBrep(doc, id);
        var distance = Num(args, "distance");
        if (Math.Abs(distance) < 1e-12) throw new ArgumentException("distance must be non-zero.");
        var solid = Bool(args, "solid");
        var offset = Brep.CreateOffsetBrep(brep, distance, solid, true, doc.ModelAbsoluteTolerance, out var blends, out var walls);
        if (offset is null || offset.Length == 0)
            throw new InvalidOperationException("OffsetSrf failed.");
        var outIds = new List<Guid>();
        foreach (var o in offset) outIds.Add(AddSurfaceResult(doc, o, args, "offset"));
        if (blends is not null)
            foreach (var o in blends) outIds.Add(AddSurfaceResult(doc, o, args, "offset_blend"));
        if (walls is not null)
            foreach (var o in walls) outIds.Add(AddSurfaceResult(doc, o, args, "offset_wall"));
        MaybeDelete(doc, [id], Bool(args, "keep_input", true));
        doc.Views.Redraw();
        return SerializeIds(outIds);
    });

    private static string TrimSurface(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var id = IdArg(args, "id");
        var cutterId = IdArg(args, "cutter_id");
        var brep = RequireBrep(doc, id);
        var cutter = FindObject(doc, cutterId).Geometry
            ?? throw new ArgumentException("cutter_id has no geometry.");
        var keepPt = ParsePoint(args["keep_point"] ?? throw new ArgumentException("keep_point required"), "keep_point");
        var parts = SplitWithCutter(brep, cutter, doc.ModelAbsoluteTolerance);
        var dists = parts.Select(p => p.ClosestPoint(keepPt).DistanceTo(keepPt)).ToArray();
        var diag = brep.GetBoundingBox(true).Diagonal.Length;
        var idx = CurveOps.SelectUnambiguousNearest(
            dists,
            ambiguityTol: Math.Max(doc.ModelAbsoluteTolerance * 10, 1e-6),
            maxDistance: Math.Max(diag * 0.5, doc.ModelAbsoluteTolerance * 100));
        var kept = parts[idx];
        var newId = AddSurfaceResult(doc, kept, args, "trim");
        MaybeDelete(doc, [id], Bool(args, "keep_input"));
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = newId.ToString(), type = "brep", pieces = parts.Length });
    });

    private static string SplitSurface(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var id = IdArg(args, "id");
        var cutterId = IdArg(args, "cutter_id");
        var brep = RequireBrep(doc, id);
        var cutter = FindObject(doc, cutterId).Geometry
            ?? throw new ArgumentException("cutter_id has no geometry.");
        var parts = SplitWithCutter(brep, cutter, doc.ModelAbsoluteTolerance);
        var outIds = new List<Guid>();
        foreach (var p in parts)
            outIds.Add(AddSurfaceResult(doc, p, args, "split piece"));
        MaybeDelete(doc, [id], Bool(args, "keep_input"));
        doc.Views.Redraw();
        return SerializeIds(outIds);
    });

    private static string JoinSurfaces(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var ids = IdsArg(args);
        SurfaceOps.RequireCount(ids.Count, 2, "join_surfaces");
        var tol = args["tolerance"] is null ? doc.ModelAbsoluteTolerance : Num(args, "tolerance");
        var breps = ids.Select(i => RequireBrep(doc, i)).ToArray();
        var joined = Brep.JoinBreps(breps, tol);
        if (joined is null || joined.Length == 0)
            throw new InvalidOperationException("Join failed — edges may be out of tolerance.");
        var outIds = joined.Select(b => AddSurfaceResult(doc, b, args, "joined surface")).ToList();
        MaybeDelete(doc, ids, Bool(args, "keep_inputs"));
        doc.Views.Redraw();
        return SerializeIds(outIds);
    });

    private static string CapPlanarHoles(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var id = IdArg(args, "id");
        var brep = RequireBrep(doc, id);
        var capped = brep.CapPlanarHoles(doc.ModelAbsoluteTolerance)
            ?? throw new InvalidOperationException("Cap failed — no planar holes or invalid brep.");
        var newId = AddSurfaceResult(doc, capped, args, "capped");
        MaybeDelete(doc, [id], Bool(args, "keep_input"));
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = newId.ToString(), type = "brep", is_solid = capped.IsSolid });
    });

    private static string ListSurfaceEdges(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var brep = RequireBrep(doc, IdArg(args, "id"));
        var edges = new List<object>();
        for (var i = 0; i < brep.Edges.Count; i++)
        {
            var e = brep.Edges[i];
            var crv = e.EdgeCurve;
            edges.Add(new
            {
                index = i,
                length = crv is null ? (double?)null : Math.Round(crv.GetLength(), 6),
                start = crv is null ? null : Pt(crv.PointAtStart),
                end = crv is null ? null : Pt(crv.PointAtEnd),
                naked = e.Valence == EdgeAdjacency.Naked,
            });
        }
        return JsonSerializer.Serialize(new { count = edges.Count, edges });
    });

    private static string DupBorder(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var brep = RequireBrep(doc, IdArg(args, "id"));
        var loops = brep.DuplicateNakedEdgeCurves(true, true);
        if (loops is null || loops.Length == 0)
            throw new InvalidOperationException("DupBorder found no naked edges.");
        var outIds = new List<Guid>();
        foreach (var c in loops)
            outIds.Add(AddCurveOrThrow(doc, c, BuildAttributes(doc, args), "border"));
        foreach (var id in outIds) ApplyGroup(doc, id, args);
        doc.Views.Redraw();
        return SerializeIds(outIds, "curve");
    });

    private static string DupEdge(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var brep = RequireBrep(doc, IdArg(args, "id"));
        var ei = ResolveEdgeIndex(brep, args, "edge_index", "pick_point");
        var crv = brep.Edges[ei].DuplicateCurve()
            ?? throw new InvalidOperationException($"Failed to duplicate edge {ei}.");
        var id = AddCurveOrThrow(doc, crv, BuildAttributes(doc, args), "edge");
        ApplyGroup(doc, id, args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "curve", edge_index = ei });
    });

    private static string ExtractIsocurve(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var brep = RequireBrep(doc, IdArg(args, "id"));
        var faceIndex = (int)Num(args, "face_index");
        if (faceIndex < 0 || faceIndex >= brep.Faces.Count)
            throw new ArgumentException($"face_index {faceIndex} out of range.");
        var face = brep.Faces[faceIndex];
        var dir = (Str(args, "direction") ?? throw new ArgumentException("direction required")).Trim().ToLowerInvariant();
        var param = Num(args, "parameter");
        Curve? crv = dir switch
        {
            "u" => face.IsoCurve(0, param),
            "v" => face.IsoCurve(1, param),
            _ => throw new ArgumentException("direction must be 'u' or 'v'."),
        };
        if (crv is null || !crv.IsValid)
            throw new InvalidOperationException("ExtractIsoCurve failed for that parameter.");
        var id = AddCurveOrThrow(doc, crv, BuildAttributes(doc, args), "isocurve");
        ApplyGroup(doc, id, args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "curve", direction = dir, parameter = param });
    });
}
