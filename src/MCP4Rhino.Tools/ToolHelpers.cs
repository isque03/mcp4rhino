using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Logic;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace MCP4Rhino.Tools;

/// <summary>
/// Shared helpers for tool facades. Parse/schema/geometry delegate to <see cref="MCP4Rhino.Logic"/>.
/// </summary>
[ExcludeFromCodeCoverage]
internal static class ToolHelpers
{
    // ---- Document / attributes ------------------------------------------------

    public static RhinoDoc RequireDoc() =>
        RhinoDoc.ActiveDoc ?? throw new InvalidOperationException("No active Rhino document.");

    public static ObjectAttributes BuildAttributes(RhinoDoc doc, JsonObject args)
    {
        var attrs = new ObjectAttributes();
        var name = args["name"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(name)) attrs.Name = name;

        var layer = args["layer"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(layer))
            attrs.LayerIndex = EnsureLayer(doc, layer);

        foreach (var (k, v) in ParseTags(args))
            attrs.SetUserString(k, v);

        return attrs;
    }

    public static void ApplyGroup(RhinoDoc doc, Guid id, JsonObject args)
    {
        var gname = args["group"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(gname) || id == Guid.Empty) return;
        var idx = doc.Groups.Find(gname);
        if (idx < 0) idx = doc.Groups.Add(gname);
        doc.Groups.AddToGroup(idx, id);
    }

    public static int EnsureLayer(RhinoDoc doc, string layerPath)
    {
        var idx = doc.Layers.FindByFullPath(layerPath, -1);
        if (idx >= 0) return idx;

        var parts = layerPath.Split(["::"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var parentIndex = -1;
        var full = "";
        foreach (var part in parts)
        {
            full = string.IsNullOrEmpty(full) ? part : full + "::" + part;
            idx = doc.Layers.FindByFullPath(full, -1);
            if (idx >= 0)
            {
                parentIndex = idx;
                continue;
            }
            var layer = new Layer { Name = part };
            if (parentIndex >= 0)
                layer.ParentLayerId = doc.Layers[parentIndex].Id;
            idx = doc.Layers.Add(layer);
            parentIndex = idx;
        }
        return idx;
    }

    public static Dictionary<string, string> ReadTags(RhinoObject obj)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        NameValueCollection? nvc = obj.Attributes.GetUserStrings();
        if (nvc is null) return result;
        foreach (var key in nvc.AllKeys)
        {
            if (key is null) continue;
            result[key] = nvc[key] ?? "";
        }
        return result;
    }

    public static string[] GetGroupNames(RhinoDoc doc, RhinoObject obj)
    {
        var list = obj.GetGroupList();
        if (list is null || list.Length == 0) return [];
        return list
            .Select(i => doc.Groups.GroupName(i))
            .Where(n => !string.IsNullOrEmpty(n))
            .Select(n => n!)
            .ToArray();
    }

    public static Dictionary<string, string> ParseTags(JsonObject args) => JsonArgs.ParseTags(args);

    public static double Num(JsonObject args, string key, double fallback = 0) => JsonArgs.Num(args, key, fallback);

    public static List<Guid> ParseIds(string ids) => JsonArgs.ParseIds(ids);

    public static string Classify(GeometryBase? geometry) => GeometryBuilders.Classify(geometry);

    public static JsonNode? Unwrap(JsonNode? node) => JsonArgs.Unwrap(node);

    public static double ToDouble(JsonNode? node) => JsonArgs.ToDouble(node);

    public static string? Str(JsonObject args, string key) => JsonArgs.Str(args, key);

    public static bool Bool(JsonObject args, string key, bool fallback = false) => JsonArgs.Bool(args, key, fallback);

    public static bool TryPoint(JsonNode? node, out Point3d p, double defaultZ = 0)
    {
        if (!JsonArgs.TryPoint(node, out var v, defaultZ))
        {
            p = Point3d.Origin;
            return false;
        }
        p = new Point3d(v.X, v.Y, v.Z);
        return true;
    }

    public static Point3d ParsePoint(JsonNode? node, string label, double defaultZ = 0)
    {
        var v = JsonArgs.ParsePoint(node, label, defaultZ);
        return new Point3d(v.X, v.Y, v.Z);
    }

    public static Point3d ParsePointOr(JsonObject args, string key, Point3d fallback, double defaultZ = 0) =>
        args[key] is null ? fallback : ParsePoint(args[key], key, defaultZ);

    public static List<Point3d> ParsePoints(JsonNode? node, string label = "points", double defaultZ = 0) =>
        JsonArgs.ParsePoints(node, label, defaultZ).Select(v => new Point3d(v.X, v.Y, v.Z)).ToList();

    public static List<Guid> IdList(JsonNode? node, string label = "ids") => JsonArgs.IdList(node, label);

    public static List<Guid> IdsArg(JsonObject args) =>
        args["ids"] is not null ? IdList(args["ids"]) : IdList(args["id"], "ids");

    public static Guid IdArg(JsonObject args, string key)
    {
        var s = Str(args, key) ?? throw new ArgumentException($"{key} required");
        return Guid.TryParse(s.Trim(), out var g) ? g : throw new ArgumentException($"Invalid GUID: {s}");
    }

    public static RhinoObject FindObject(RhinoDoc doc, Guid id) =>
        doc.Objects.FindId(id) ?? throw new ArgumentException($"Object not found: {id}");

    public static double[] Pt(Point3d p) => [Math.Round(p.X, 6), Math.Round(p.Y, 6), Math.Round(p.Z, 6)];

    public static double[] Pt(Vector3d v) => [Math.Round(v.X, 6), Math.Round(v.Y, 6), Math.Round(v.Z, 6)];

    public static object? BBoxObj(BoundingBox b) =>
        b.IsValid ? new { min = Pt(b.Min), max = Pt(b.Max) } : null;

    public static string Inv(double d) => d.ToString("R", CultureInfo.InvariantCulture);

    public static string ColorHex(System.Drawing.Color c) => JsonArgs.ColorHex(c);

    public static System.Drawing.Color ParseColor(string hex) => JsonArgs.ParseColor(hex);

    public static readonly (string, string, string)[] CommonProps = JsonArgs.CommonProps;

    public static object Tool(string name, string description, params (string Name, string Type, string Desc)[] props) =>
        JsonArgs.Tool(name, description, props);

    public static object ToolC(string name, string description, params (string Name, string Type, string Desc)[] props) =>
        JsonArgs.ToolC(name, description, props);

    public static Brep? ToBrep(GeometryBase? g) => GeometryBuilders.ToBrep(g);

    public static Brep? ExtrudeProfile(Curve profile, Vector3d dir, double tol, bool cap = true) =>
        GeometryBuilders.ExtrudeProfile(profile, dir, tol, cap);

    public static PolylineCurve ClosedPolyline(IEnumerable<Point3d> pts, double tol) =>
        GeometryBuilders.ClosedPolyline(pts, tol);

    public static Brep OrientedBox(Point3d baseCenter, Vector3d dir, double width, double depth, double height) =>
        GeometryBuilders.OrientedBox(baseCenter, dir, width, depth, height);

    public static Brep SideProfileSolid(Point3d origin, Vector3d run, double width, IList<(double u, double v)> profile, double tol) =>
        GeometryBuilders.SideProfileSolid(origin, run, width, profile, tol);

    public static double Metres(RhinoDoc doc, double metres) =>
        metres * RhinoMath.UnitScale(UnitSystem.Meters, doc.ModelUnitSystem);

    public const string KindKey = "mcp4:kind";
    public const string LevelKey = "mcp4:level";
    public const string TypeKey = "mcp4:type_id";
    public const string IfcKey = "mcp4:ifc";
    public const string ProjectKey = "MCP4RHINO_PROJECT_JSON";
    public const string LevelsKey = "MCP4RHINO_LEVELS_JSON";
    public const string TypesKey = "MCP4RHINO_TYPES_JSON";
    public const string CodeKey = "MCP4RHINO_CODE_JSON";
    public const string ActiveLevelKey = "MCP4RHINO_ACTIVE_LEVEL";
    public const string LastCodeReportKey = "MCP4RHINO_LAST_CODE_REPORT";

    public static string Json(object o) => JsonSerializer.Serialize(o);

    public static string GetDocString(RhinoDoc doc, string key) => doc.Strings.GetValue(key) ?? "";

    public static void SetDocString(RhinoDoc doc, string key, string value) => doc.Strings.SetString(key, value);

    public static T? GetDocJson<T>(RhinoDoc doc, string key)
    {
        var s = GetDocString(doc, key);
        if (string.IsNullOrWhiteSpace(s)) return default;
        return JsonSerializer.Deserialize<T>(s);
    }

    public static void SetDocJson<T>(RhinoDoc doc, string key, T value) =>
        SetDocString(doc, key, JsonSerializer.Serialize(value));

    public static void SetKind(ObjectAttributes attrs, string kind, string? ifc = null, string? level = null, string? typeId = null)
    {
        attrs.SetUserString(KindKey, kind);
        if (!string.IsNullOrWhiteSpace(ifc)) attrs.SetUserString(IfcKey, ifc);
        if (!string.IsNullOrWhiteSpace(level)) attrs.SetUserString(LevelKey, level);
        if (!string.IsNullOrWhiteSpace(typeId)) attrs.SetUserString(TypeKey, typeId);
    }

    public static string LogsDir()
    {
        var dir = OperatingSystem.IsWindows()
            ? Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "MCP4Rhino")
            : Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Library", "Logs", "MCP4Rhino");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static System.Drawing.Color? TryParseColor(string? hex) => JsonArgs.TryParseColor(hex);
}
