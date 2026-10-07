using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Logic;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using static MCP4Rhino.Tools.ToolHelpers;

namespace MCP4Rhino.Tools;

/// <summary>Shared helpers for <see cref="SurfaceFoundationTools"/>.</summary>
[ExcludeFromCodeCoverage]
internal static class SurfaceBrepHelpers
{
    public static Guid AddBrepOrThrow(RhinoDoc doc, Brep brep, ObjectAttributes attrs, string what = "surface")
    {
        var id = doc.Objects.AddBrep(brep, attrs);
        if (id == Guid.Empty) throw new InvalidOperationException($"Failed to add {what}.");
        return id;
    }

    public static Guid AddSurfaceResult(RhinoDoc doc, Brep brep, JsonObject args, string what = "surface")
    {
        var id = AddBrepOrThrow(doc, brep, BuildAttributes(doc, args), what);
        ApplyGroup(doc, id, args);
        return id;
    }

    public static Guid AddCurveOrThrow(RhinoDoc doc, Curve crv, ObjectAttributes attrs, string what = "curve")
    {
        var id = doc.Objects.AddCurve(crv, attrs);
        if (id == Guid.Empty) throw new InvalidOperationException($"Failed to add {what}.");
        return id;
    }

    public static Plane PlaneFromNormal(Point3d origin, JsonObject args)
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

    public static Curve RequireCurve(RhinoDoc doc, Guid id) =>
        FindObject(doc, id).Geometry as Curve ?? throw new ArgumentException($"Object {id} is not a curve.");

    public static Brep RequireBrep(RhinoDoc doc, Guid id)
    {
        var g = FindObject(doc, id).Geometry;
        return ToBrep(g) ?? throw new ArgumentException($"Object {id} is not a surface/brep/extrusion.");
    }

    public static List<Guid> SectionIds(JsonObject args, string key = "section_ids")
    {
        if (args[key] is null) throw new ArgumentException($"{key} required");
        return IdList(args[key], key);
    }

    public static LoftType MapLoft(SurfaceOps.LoftStyle style) => style switch
    {
        SurfaceOps.LoftStyle.Loose => LoftType.Loose,
        SurfaceOps.LoftStyle.Straight => LoftType.Straight,
        SurfaceOps.LoftStyle.Tight => LoftType.Tight,
        SurfaceOps.LoftStyle.Uniform => LoftType.Uniform,
        _ => LoftType.Normal,
    };

    public static BlendContinuity MapCont(SurfaceOps.Continuity c) => c switch
    {
        SurfaceOps.Continuity.Position => BlendContinuity.Position,
        SurfaceOps.Continuity.Curvature => BlendContinuity.Curvature,
        _ => BlendContinuity.Tangency,
    };

    public static string SerializeIds(IEnumerable<Guid> ids, string type = "brep") =>
        JsonSerializer.Serialize(new { ids = ids.Select(g => g.ToString()).ToArray(), count = ids.Count(), type });

    public static void MaybeDelete(RhinoDoc doc, IEnumerable<Guid> ids, bool keep)
    {
        if (keep) return;
        foreach (var id in ids) doc.Objects.Delete(id, true);
    }

    public static int ResolveEdgeIndex(Brep brep, JsonObject args, string indexKey, string pickKey)
    {
        if (args[indexKey] is not null)
            return SurfaceOps.RequireEdgeIndex((int)Num(args, indexKey), brep.Edges.Count);

        if (args[pickKey] is null)
            throw new ArgumentException($"Provide {indexKey} or {pickKey}.");

        var pt = ParsePoint(args[pickKey], pickKey);
        var tol = Math.Max(brep.GetBoundingBox(true).Diagonal.Length * 1e-6, 1e-6);
        var dists = brep.Edges.Select(e =>
        {
            var crv = e.EdgeCurve;
            if (crv is null) return double.PositiveInfinity;
            if (!crv.ClosestPoint(pt, out var t)) return double.PositiveInfinity;
            return crv.PointAt(t).DistanceTo(pt);
        }).ToArray();

        var maxDist = Math.Max(brep.GetBoundingBox(true).Diagonal.Length * 0.25, tol * 100);
        return SurfaceOps.SelectEdgeIndex(dists, ambiguityTol: Math.Max(tol * 10, 1e-6), maxDistance: maxDist);
    }

    public static Point3d DefaultPick(Brep brep) => brep.GetBoundingBox(true).Center;

    public static Point2d ClosestFaceUv(BrepFace face, Point3d pt)
    {
        if (!face.ClosestPoint(pt, out var u, out var v))
            throw new InvalidOperationException("Failed to project pick_point onto surface face.");
        return new Point2d(u, v);
    }

    /// <summary>
    /// Resolve a fillet/chamfer face: explicit <paramref name="faceIndexKey"/>, sole face,
    /// or unambiguous nearest face to <paramref name="pickKey"/>. Multi-face breps require a pick or index.
    /// </summary>
    public static BrepFace ResolveFace(Brep brep, JsonObject args, string pickKey, string faceIndexKey)
    {
        if (brep.Faces.Count == 0) throw new ArgumentException("Brep has no faces.");

        if (args[faceIndexKey] is not null)
        {
            var fi = SurfaceOps.RequireFaceIndex((int)Num(args, faceIndexKey), brep.Faces.Count);
            return brep.Faces[fi];
        }

        if (brep.Faces.Count == 1)
            return brep.Faces[0];

        if (args[pickKey] is null)
            throw new ArgumentException(
                $"Provide {faceIndexKey} or {pickKey} when the surface has multiple faces.");

        var pt = ParsePoint(args[pickKey], pickKey);
        var dists = new double[brep.Faces.Count];
        for (var i = 0; i < brep.Faces.Count; i++)
        {
            if (!brep.Faces[i].ClosestPoint(pt, out var u, out var v))
            {
                dists[i] = double.PositiveInfinity;
                continue;
            }
            dists[i] = brep.Faces[i].PointAt(u, v).DistanceTo(pt);
        }

        var diag = brep.GetBoundingBox(true).Diagonal.Length;
        var tol = Math.Max(diag * 1e-6, 1e-6);
        var idx = SurfaceOps.SelectFaceIndex(
            dists,
            ambiguityTol: Math.Max(tol * 10, 1e-6),
            maxDistance: Math.Max(diag * 0.25, tol * 100));
        return brep.Faces[idx];
    }

    public static BrepFace FaceForEdge(Brep brep, BrepEdge edge)
    {
        var tis = edge.AdjacentFaces();
        if (tis is null || tis.Length == 0)
            throw new InvalidOperationException("Edge has no adjacent faces.");
        return brep.Faces[tis[0]];
    }

    /// <summary>
    /// Add all replacement breps; on any Add failure delete what was added and leave inputs untouched.
    /// Only after every add succeeds, delete <paramref name="deleteIds"/> when <paramref name="deleteInputs"/> is true.
    /// </summary>
    public static List<Guid> AddBrepsThenMaybeDelete(
        RhinoDoc doc,
        IReadOnlyList<Brep> pieces,
        JsonObject args,
        IReadOnlyList<Guid> deleteIds,
        bool deleteInputs,
        string what)
    {
        var attrs = BuildAttributes(doc, args);
        var added = new List<Guid>(pieces.Count);
        try
        {
            foreach (var b in pieces)
            {
                var id = doc.Objects.AddBrep(b, attrs);
                if (id == Guid.Empty)
                    throw new InvalidOperationException($"Failed to add {what}.");
                ApplyGroup(doc, id, args);
                added.Add(id);
            }
        }
        catch
        {
            foreach (var id in added) doc.Objects.Delete(id, true);
            throw;
        }

        if (deleteInputs)
            foreach (var id in deleteIds) doc.Objects.Delete(id, true);
        return added;
    }

    public static Brep CutterToBrep(GeometryBase cutter, Brep target, double tol)
    {
        switch (cutter)
        {
            case Brep cb:
                return cb;
            case Extrusion ex:
                return ex.ToBrep() ?? throw new InvalidOperationException("Failed to convert extrusion cutter.");
            case Surface s:
                return Brep.CreateFromSurface(s) ?? throw new InvalidOperationException("Failed to convert surface cutter.");
            case Curve crv:
            {
                SurfaceOps.RequirePlanarCutter(crv.TryGetPlane(out var plane, tol));
                var bbox = target.GetBoundingBox(true);
                var len = Math.Max(bbox.Diagonal.Length, tol * 100);
                var dir = plane.Normal;
                if (!dir.Unitize())
                    throw new InvalidOperationException("Planar curve cutter has a degenerate normal.");
                var extr = Surface.CreateExtrusion(crv, dir * len)
                    ?? throw new InvalidOperationException("Failed to build extruded curve cutter.");
                extr.Translate(-dir * (len / 2));
                return Brep.CreateFromSurface(extr)
                    ?? throw new InvalidOperationException("Failed to convert extruded cutter to brep.");
            }
            default:
                throw new ArgumentException("cutter_id must be a curve, surface, extrusion, or brep.");
        }
    }

    public static Brep[] SplitWithCutter(Brep brep, GeometryBase cutter, double tol)
    {
        var cutterBrep = CutterToBrep(cutter, brep, tol);
        var parts = brep.Split(cutterBrep, tol);
        if (parts is null || parts.Length == 0)
            throw new InvalidOperationException("Split produced no pieces — cutter may miss the surface.");
        return parts;
    }
}
