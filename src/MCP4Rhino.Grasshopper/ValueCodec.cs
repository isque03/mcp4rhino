using System.Text.Json.Nodes;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Types;
using MCP4Rhino.Logic.Grasshopper;
using Rhino;
using Rhino.Geometry;

namespace MCP4Rhino.Grasshopper;

internal static class ValueCodec
{
    public static void Assign(IGH_Param parameter, JsonNode? tree, RhinoDoc rhino)
    {
        if (parameter.SourceCount > 0)
            throw new GhException("connected_input", "Disconnect sources before assigning persistent values.", "data-trees");
        var branches = DataTree.Parse(tree);
        switch (parameter)
        {
            case Param_Number p: Set(p, branches, v => new GH_Number(GhJson.Number(v))); break;
            case Param_Integer p: Set(p, branches, v => new GH_Integer(v!.GetValue<int>())); break;
            case Param_Boolean p: Set(p, branches, v => new GH_Boolean(v!.GetValue<bool>())); break;
            case Param_String p: Set(p, branches, v => new GH_String(v!.GetValue<string>())); break;
            case Param_Point p: Set(p, branches, v => new GH_Point(Point(v))); break;
            case Param_Vector p: Set(p, branches, v => new GH_Vector(Vector(v))); break;
            case Param_Plane p: Set(p, branches, v => new GH_Plane(Plane(v))); break;
            case Param_Interval p: Set(p, branches, v => new GH_Interval(Interval(v))); break;
            case Param_Curve p: Set(p, branches, v => Reference<GH_Curve>(v, rhino)); break;
            case Param_Brep p: Set(p, branches, v => Reference<GH_Brep>(v, rhino)); break;
            case Param_Surface p: Set(p, branches, v => Reference<GH_Surface>(v, rhino)); break;
            case Param_Mesh p: Set(p, branches, v => Reference<GH_Mesh>(v, rhino)); break;
            case Param_Geometry p: Set(p, branches, v => GeometryReference(v, rhino)); break;
            default: throw new GhException("unsupported_value", $"Persistent values for {parameter.TypeName} are unsupported. " +
                "Add a native Number, Integer, Boolean or Text parameter, set_value on its in:0 port, then connect its out:0 to this input. " +
                "Use gh_describe_components to discover compatible types and ports.", "data-trees");
        }
    }

    private static void Set<T>(GH_PersistentParam<T> parameter, IReadOnlyList<DataBranch> branches,
        Func<JsonNode?, T> convert) where T : class, IGH_Goo
    {
        var structure = new GH_Structure<T>();
        foreach (var branch in branches)
        {
            var path = new GH_Path(branch.Path);
            structure.EnsurePath(path);
            foreach (var value in branch.Items) structure.Append(value is null ? null! : convert(value), path);
        }
        // SetPersistentData records its own undo event. GraphEditor owns a single batch undo record.
        parameter.PersistentData.Clear();
        parameter.PersistentData.MergeStructure(structure);
        parameter.OnObjectChanged(GH_ObjectEventType.PersistentData);
        parameter.ExpireSolution(false);
    }

    private static double[] Coordinates(JsonNode? value, string kind, int count)
    {
        var item = GhJson.Object(value);
        if (GhJson.Text(item, "type") != kind) throw new GhException("invalid_value", $"Expected {kind}.", "design");
        var coordinates = GhJson.Array(item["value"]).Select(GhJson.Number).ToArray();
        if (coordinates.Length != count) throw new GhException("invalid_value", $"{kind} requires {count} coordinates.", "design");
        return coordinates;
    }

    private static Point3d Point(JsonNode? value)
    {
        var c = Coordinates(value, "point", 3); return new Point3d(c[0], c[1], c[2]);
    }

    private static Vector3d Vector(JsonNode? value)
    {
        var c = Coordinates(value, "vector", 3); return new Vector3d(c[0], c[1], c[2]);
    }

    private static Interval Interval(JsonNode? value)
    {
        var c = Coordinates(value, "interval", 2); return new Interval(c[0], c[1]);
    }

    private static Plane Plane(JsonNode? value)
    {
        var p = GhJson.Object(value);
        if (GhJson.Text(p, "type") != "plane") throw new GhException("invalid_value", "Expected plane.", "design");
        var plane = new Plane(Point(p["origin"]), Vector(p["x_axis"]), Vector(p["y_axis"]));
        if (!plane.IsValid) throw new GhException("invalid_value", "Plane axes must define a valid plane.", "design");
        return plane;
    }

    private static Guid ReferenceId(JsonNode? value, RhinoDoc rhino)
    {
        var item = GhJson.Object(value);
        if (GhJson.Text(item, "type") != "rhino_ref") throw new GhException("invalid_value", "Expected a rhino_ref with an object id.");
        var id = Guid.Parse(GhJson.Text(item, "id"));
        if (rhino.Objects.FindId(id) is null) throw new GhException("missing_reference", $"Rhino object {id} does not exist.", "debugging");
        return id;
    }

    private static T Reference<T>(JsonNode? value, RhinoDoc rhino) where T : class, IGH_GeometricGoo, new()
    {
        var goo = new T { ReferenceID = ReferenceId(value, rhino) };
        if (!goo.LoadGeometry(rhino)) throw new GhException("invalid_value", $"Rhino object cannot be used as {typeof(T).Name}.");
        return goo;
    }

    private static IGH_GeometricGoo GeometryReference(JsonNode? value, RhinoDoc rhino)
    {
        var id = ReferenceId(value, rhino);
        return rhino.Objects.FindId(id)!.Geometry switch
        {
            Curve => Reference<GH_Curve>(value, rhino), Brep => Reference<GH_Brep>(value, rhino),
            Mesh => Reference<GH_Mesh>(value, rhino), Surface => Reference<GH_Surface>(value, rhino),
            _ => throw new GhException("unsupported_value", "Reference a curve, surface, Brep or mesh."),
        };
    }

    public static object? PersistentSummary(IGH_Param parameter, bool measurements = true)
    {
        var data = parameter.GetType().GetProperty("PersistentData")?.GetValue(parameter) as IGH_Structure;
        return data is null ? null : new { branch_count = data.PathCount, item_count = data.DataCount,
            sample = Rows(data, measurements: measurements).Take(10).ToArray() };
    }

    public static IEnumerable<object> Rows(IGH_Structure tree, double tolerance = 0.001, bool measurements = true, int offset = 0)
    {
        foreach (var path in tree.Paths)
        {
            var branch = tree.get_Branch(path);
            if (branch.Count == 0)
            {
                if (offset > 0) { offset--; continue; }
                yield return new { path = path.Indices, item_index = (int?)null, empty = true };
            }
            if (offset >= branch.Count) { offset -= branch.Count; continue; }
            for (var i = offset; i < branch.Count; i++)
                yield return new { path = path.Indices, item_index = i, value = Summary(branch[i] as IGH_Goo, tolerance, measurements) };
            offset = 0;
        }
    }

    public static object? Summary(IGH_Goo? value, double tolerance = 0.001, bool measurements = true) => value switch
    {
        null => null,
        GH_Number n => new { type = "number", value = double.IsFinite(n.Value) ? (double?)n.Value : null, valid = double.IsFinite(n.Value) },
        GH_Integer n => new { type = "integer", value = n.Value },
        GH_Boolean b => new { type = "boolean", value = b.Value },
        GH_String s => new { type = "string", value = (s.Value ?? "")[..Math.Min(s.Value?.Length ?? 0, 2000)], truncated = (s.Value?.Length ?? 0) > 2000 },
        GH_Point p => new { type = "point", value = new[] { p.Value.X, p.Value.Y, p.Value.Z } },
        GH_Vector v => new { type = "vector", value = new[] { v.Value.X, v.Value.Y, v.Value.Z } },
        GH_Interval d => new { type = "interval", value = new[] { d.Value.T0, d.Value.T1 } },
        GH_Plane p => new { type = "plane", origin = new[] { p.Value.OriginX, p.Value.OriginY, p.Value.OriginZ },
            x_axis = new[] { p.Value.XAxis.X, p.Value.XAxis.Y, p.Value.XAxis.Z }, y_axis = new[] { p.Value.YAxis.X, p.Value.YAxis.Y, p.Value.YAxis.Z } },
        IGH_GeometricGoo g when measurements => GeometrySummary.Read(g, tolerance),
        IGH_GeometricGoo g => new { type = value.TypeName, valid = value.IsValid, bounds = Bounds(g.Boundingbox),
            reference_id = g.ReferenceID, measurements_status = "use_gh_read_output_for_computed_measurements" },
        _ => new { type = value.TypeName, valid = value.IsValid },
    };

    public static object? Bounds(BoundingBox bounds) => !bounds.IsValid ? null : new
    {
        min = new[] { bounds.Min.X, bounds.Min.Y, bounds.Min.Z },
        max = new[] { bounds.Max.X, bounds.Max.Y, bounds.Max.Z },
    };
}
