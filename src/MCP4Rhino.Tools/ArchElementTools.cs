using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Host;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using static MCP4Rhino.Tools.ToolHelpers;

namespace MCP4Rhino.Tools;

/// <summary>
/// P1 architectural semantics. Per-object BIM data lives in Attribute User Text keys prefixed <c>mcp4:</c>
/// (mcp4:kind, mcp4:level, mcp4:type_id, mcp4:ifc, ...). Project / levels / types / code live in RhinoDoc.Strings as JSON
/// (MCP4RHINO_PROJECT_JSON, MCP4RHINO_LEVELS_JSON, MCP4RHINO_TYPES_JSON, MCP4RHINO_CODE_JSON).
/// </summary>
internal static class ArchElementTools
{
    private const string ProjectKey = "MCP4RHINO_PROJECT_JSON";
    private const string LevelsKey = "MCP4RHINO_LEVELS_JSON";
    private const string TypesKey = "MCP4RHINO_TYPES_JSON";
    private const string CodeKey = "MCP4RHINO_CODE_JSON";
    private const string ActiveLevelKey = "MCP4RHINO_ACTIVE_LEVEL";
    private const string Kind = "mcp4:kind";

    private static readonly Dictionary<string, (string Layer, string Ifc)> KindDefaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["wall"] = ("A-WALL", "IfcWall"),
        ["slab"] = ("A-FLOR", "IfcSlab"),
        ["roof"] = ("A-ROOF", "IfcRoof"),
        ["door"] = ("A-DOOR", "IfcDoor"),
        ["window"] = ("A-GLAZ", "IfcWindow"),
        ["stair"] = ("A-STAIR", "IfcStair"),
        ["ramp"] = ("A-RAMP", "IfcRamp"),
        ["railing"] = ("A-RAIL", "IfcRailing"),
        ["column"] = ("S-COLS", "IfcColumn"),
        ["beam"] = ("S-BEAM", "IfcBeam"),
        ["space"] = ("A-AREA", "IfcSpace"),
        ["level"] = ("A-LEVEL", "IfcBuildingStorey"),
        ["site"] = ("A-SITE", "IfcSite"),
        ["building"] = ("A-BLDG", "IfcBuilding"),
        ["grid"] = ("A-GRID", "IfcGrid"),
    };

    private const string LevelDesc = "Level name (default: active level, else elevation 0)";
    private const string ZOffDesc = "Added to the level elevation (default 0)";
    private const string TypeDesc = "Element type id from upsert_element_type (supplies default dimensions / fire_rating / ifc)";

    public static object[] ListTools() => Tools;

    public static string? Dispatch(string name, JsonObject args) => TryCall(name, args);

    public static readonly object[] Tools =
    [
        Tool("set_project_info", "Merge fields into the project info JSON (null value removes a key). Optional `code` merges into the code-settings JSON.",
            ("info", "object", "Fields to merge (or pass fields at top level, e.g. name, address, client)"),
            ("code", "object", "Code settings to merge, e.g. {stair_max_riser_m:0.19, ramp_max_slope:0.0833, door_min_clear_width_m:0.81}")),
        Tool("get_project_info", "Get project info, code settings, levels and active level."),
        Tool("create_level", "Create or update a level (name + elevation). Stored in document strings.",
            ("name*", "string", "Level name"),
            ("elevation*", "number", "Elevation in model units"),
            ("height", "number", "Floor-to-floor height (default wall/column height for this level)"),
            ("set_active", "boolean", "Make this the active level")),
        Tool("list_levels", "List levels sorted by elevation and the active level."),
        Tool("set_active_level", "Set the active level used as default by create_* tools.",
            ("name*", "string", "Level name")),
        ToolC("create_grid", "Create structural grid lines on layer A-GRID (numbers along X, letters along Y).",
            ("origin", "any", "[x,y,z] grid origin (default origin; z defaults to level elevation)"),
            ("x_count*", "integer", "Number of grid lines along X (labelled 1,2,..)"),
            ("y_count*", "integer", "Number of grid lines along Y (labelled A,B,..)"),
            ("x_spacing*", "number", "Spacing between X lines"),
            ("y_spacing*", "number", "Spacing between Y lines"),
            ("extension", "number", "Line extension past outer grid (default 1 m)"),
            ("level", "string", LevelDesc)),
        ToolC("create_site", "Create a site boundary curve (closed polyline) tagged kind=site.",
            ("boundary*", "array", "[[x,y,z],...] closed polygon")),
        ToolC("create_building", "Create a building marker: footprint outline curve, or a point at origin if no footprint.",
            ("footprint", "array", "Optional [[x,y,z],...] outline"),
            ("origin", "any", "Marker location when no footprint (default origin)"),
            ("level", "string", LevelDesc)),
        ToolC("create_wall", "Create a wall solid along an XY path with thickness and height.",
            ("path*", "array", "[[x,y],...] centerline (z ignored; level elevation used)"),
            ("closed", "boolean", "Close the path into a ring (also inferred if first == last)"),
            ("thickness", "number", "Wall thickness (default 0.2 m)"),
            ("height", "number", "Wall height (default level height or 3 m)"),
            ("level", "string", LevelDesc),
            ("z_offset", "number", ZOffDesc),
            ("wall_type_id", "string", TypeDesc),
            ("fire_rating", "string", "e.g. 1hr, 2hr"),
            ("type_id", "string", TypeDesc)),
        ToolC("create_slab", "Create a floor slab: boundary extruded down from the level elevation by thickness.",
            ("boundary*", "array", "[[x,y],...] polygon (z forced to level elevation)"),
            ("thickness", "number", "Slab thickness (default 0.25 m)"),
            ("level", "string", LevelDesc),
            ("z_offset", "number", ZOffDesc),
            ("type_id", "string", TypeDesc),
            ("fire_rating", "string", "Fire rating")),
        ToolC("create_roof", "Create a roof plate from a boundary, optionally sloped.",
            ("boundary*", "array", "[[x,y],...] polygon"),
            ("thickness", "number", "Vertical thickness (default 0.2 m)"),
            ("level", "string", LevelDesc),
            ("z_offset", "number", ZOffDesc),
            ("slope_deg", "number", "Roof pitch in degrees (default 0 = flat)"),
            ("slope_direction_deg", "number", "Direction of rise from +X (default 90 = rises toward +Y)"),
            ("type_id", "string", TypeDesc)),
        ToolC("create_column", "Create a column (round if radius, else rectangular width x depth).",
            ("base*", "any", "[x,y] or [x,y,z] base point"),
            ("height", "number", "Column height (default level height or 3 m)"),
            ("radius", "number", "Round column radius"),
            ("width", "number", "Rect width (default 0.4 m)"),
            ("depth", "number", "Rect depth (default = width)"),
            ("level", "string", LevelDesc),
            ("z_offset", "number", ZOffDesc),
            ("type_id", "string", TypeDesc)),
        ToolC("create_beam", "Create a rectangular beam between two points.",
            ("start*", "any", "[x,y,z] start (z defaults to level elevation)"),
            ("end*", "any", "[x,y,z] end"),
            ("width", "number", "Section width (default 0.3 m)"),
            ("height", "number", "Section height (default 0.5 m)"),
            ("level", "string", LevelDesc),
            ("type_id", "string", TypeDesc)),
        ToolC("create_door", "Create a door in a wall: cuts the opening and adds a door leaf.",
            ("host_wall_id*", "string", "Wall GUID created by create_wall"),
            ("t_param", "number", "0-1 position along wall path (default 0.5)"),
            ("position", "any", "[x,y] position projected onto wall path (overrides t_param)"),
            ("width", "number", "Opening width (default 0.9 m)"),
            ("height", "number", "Opening height (default 2.1 m)"),
            ("sill", "number", "Bottom offset above wall base (default 0)"),
            ("type_id", "string", TypeDesc)),
        ToolC("create_window", "Create a window in a wall: cuts the opening and adds a glass pane.",
            ("host_wall_id*", "string", "Wall GUID created by create_wall"),
            ("t_param", "number", "0-1 position along wall path (default 0.5)"),
            ("position", "any", "[x,y] position projected onto wall path (overrides t_param)"),
            ("width", "number", "Opening width (default 1.2 m)"),
            ("height", "number", "Opening height (default 1.2 m)"),
            ("sill", "number", "Sill height above wall base (default 0.9 m)"),
            ("type_id", "string", TypeDesc)),
        ToolC("create_stair", "Create a straight stair solid (sawtooth profile) between start and end.",
            ("start*", "any", "[x,y,z] bottom of run (z defaults to level elevation)"),
            ("end*", "any", "[x,y,z] top of run (horizontal run measured in XY)"),
            ("total_rise", "number", "Total rise (default end.z - start.z)"),
            ("tread_count", "integer", "Number of steps"),
            ("riser_height", "number", "Target riser height (used if tread_count omitted)"),
            ("width", "number", "Stair width (default 1 m)"),
            ("level", "string", LevelDesc)),
        ToolC("create_ramp", "Create a ramp slab between start and end and check slope against code.",
            ("start*", "any", "[x,y,z] bottom"),
            ("end*", "any", "[x,y,z] top"),
            ("width", "number", "Ramp width (default 1.2 m)"),
            ("thickness", "number", "Ramp thickness (default 0.15 m)"),
            ("level", "string", LevelDesc)),
        ToolC("create_railing", "Create a railing (top rail + posts) along a path.",
            ("path*", "array", "[[x,y,z],...]"),
            ("height", "number", "Railing height (default 1 m)"),
            ("post_spacing", "number", "Post spacing (default 1.2 m)"),
            ("level", "string", LevelDesc)),
        ToolC("create_curtain_wall", "Create a simplified curtain wall: mullions/transoms (frame) plus glass panels.",
            ("path*", "array", "[[x,y],...]"),
            ("height*", "number", "Wall height"),
            ("mullion_spacing", "number", "Target bay width (default 1.5 m)"),
            ("level", "string", LevelDesc),
            ("z_offset", "number", ZOffDesc)),
        ToolC("create_space", "Create a room/space from a closed boundary (flat surface on layer A-AREA).",
            ("boundary*", "array", "[[x,y],...] closed polygon"),
            ("height", "number", "Space height (default level height or 3 m)"),
            ("occupancy_factor", "number", "Net m² per occupant (occupant_load = ceil(area_m2 / factor))"),
            ("use", "string", "Programmatic use, e.g. office, corridor"),
            ("level", "string", LevelDesc),
            ("z_offset", "number", ZOffDesc)),
        Tool("update_space", "Update space tags and optionally its boundary.",
            ("id*", "string", "Space GUID"),
            ("name", "string", "New name"),
            ("height", "number", "Space height"),
            ("occupancy_factor", "number", "Net m² per occupant"),
            ("use", "string", "Use"),
            ("level", "string", "Level name (tag only)"),
            ("boundary", "array", "[[x,y],...] replace boundary")),
        Tool("list_spaces", "List spaces with area, occupancy and level.",
            ("level", "string", "Optional level filter")),
        Tool("apply_opening", "Cut an opening in a host: with cutter_id, or a box (width/height/depth) at position.",
            ("host_id*", "string", "Host object GUID (Brep/solid)"),
            ("cutter_id", "string", "Solid GUID to subtract"),
            ("keep_cutter", "boolean", "Keep cutter solid (default false)"),
            ("width", "number", "Box width along rotation_deg_z direction"),
            ("height", "number", "Box height along Z"),
            ("depth", "number", "Box depth perpendicular to width"),
            ("position", "any", "[x,y,z] bottom-center of box"),
            ("rotation_deg_z", "number", "Rotation about Z (default 0 = width along X)")),
        Tool("list_element_types", "List element types (optionally by kind).",
            ("kind", "string", "Optional kind filter")),
        Tool("upsert_element_type", "Create or update an element type used as defaults by create_* tools.",
            ("type_id*", "string", "Type id"),
            ("kind", "string", "wall|slab|roof|door|window|column|beam|..."),
            ("name", "string", "Display name"),
            ("thickness", "number", "Default thickness"),
            ("height", "number", "Default height"),
            ("width", "number", "Default width"),
            ("depth", "number", "Default depth"),
            ("fire_rating", "string", "Fire rating"),
            ("ifc", "string", "IFC class, e.g. IfcWall"),
            ("props", "object", "Extra fields to merge")),
        Tool("get_building_model", "Aggregate project, levels, spaces and tagged elements into one model summary.",
            ("include_elements", "boolean", "Include per-element list (default true)"),
            ("limit", "integer", "Max elements listed (default 500)")),
        Tool("query_elements", "Filter mcp4-tagged elements by kind/level/type/tag/name/layer.",
            ("kind", "string", "Kind or comma list: wall,slab,door,..."),
            ("level", "string", "Level name"),
            ("type_id", "string", "Element type id"),
            ("tag_key", "string", "Require this user-text key (any tag)"),
            ("tag_value", "string", "Substring match for tag_key"),
            ("name", "string", "Name substring"),
            ("layer", "string", "Layer substring"),
            ("limit", "integer", "Max results (default 1000)")),
        Tool("measure_distance", "Distance between two points or two objects (centers + bounding-box gap).",
            ("a", "any", "[x,y,z] point"),
            ("b", "any", "[x,y,z] point"),
            ("ids", "any", "Alternative: two object GUIDs")),
        Tool("measure_area", "Area of objects (surface area / closed curve area) or a polygon of points.",
            ("ids", "any", "GUIDs"),
            ("points", "array", "[[x,y,z],...] polygon")),
        Tool("measure_clear_width", "Clear width of a door/window (id), between two walls (a,b ids), or between two points (from,to).",
            ("id", "string", "Door/window GUID"),
            ("a", "string", "First wall/object GUID"),
            ("b", "string", "Second wall/object GUID"),
            ("from", "any", "[x,y,z]"),
            ("to", "any", "[x,y,z]")),
    ];

    public static string? TryCall(string name, JsonObject args) => name switch
    {
        "set_project_info" => SetProjectInfo(args),
        "get_project_info" => GetProjectInfo(),
        "create_level" => CreateLevel(args),
        "list_levels" => ListLevels(),
        "set_active_level" => SetActiveLevel(args),
        "create_grid" => CreateGrid(args),
        "create_site" => CreateSite(args),
        "create_building" => CreateBuilding(args),
        "create_wall" => CreateWall(args),
        "create_slab" => CreateSlab(args),
        "create_roof" => CreateRoof(args),
        "create_column" => CreateColumn(args),
        "create_beam" => CreateBeam(args),
        "create_door" => CreateOpeningElement(args, "door"),
        "create_window" => CreateOpeningElement(args, "window"),
        "create_stair" => CreateStair(args),
        "create_ramp" => CreateRamp(args),
        "create_railing" => CreateRailing(args),
        "create_curtain_wall" => CreateCurtainWall(args),
        "create_space" => CreateSpace(args),
        "update_space" => UpdateSpace(args),
        "list_spaces" => ListSpaces(args),
        "apply_opening" => ApplyOpening(args),
        "list_element_types" => ListElementTypes(args),
        "upsert_element_type" => UpsertElementType(args),
        "get_building_model" => GetBuildingModel(args),
        "query_elements" => QueryElements(args),
        "measure_distance" => MeasureDistance(args),
        "measure_area" => MeasureArea(args),
        "measure_clear_width" => MeasureClearWidth(args),
        _ => null,
    };

    // ---- Doc-string JSON store ---------------------------------------------------

    private static JsonNode? ReadJson(RhinoDoc doc, string key)
    {
        var s = doc.Strings.GetValue(key);
        return string.IsNullOrWhiteSpace(s) ? null : JsonNode.Parse(s);
    }

    private static JsonObject ReadObj(RhinoDoc doc, string key) => ReadJson(doc, key) as JsonObject ?? new JsonObject();

    private static JsonArray ReadArr(RhinoDoc doc, string key) => ReadJson(doc, key) as JsonArray ?? new JsonArray();

    private static void WriteJson(RhinoDoc doc, string key, JsonNode node) => doc.Strings.SetString(key, node.ToJsonString());

    private static JsonNode? Clone(JsonNode? n) => n is null ? null : JsonNode.Parse(n.ToJsonString());

    private static void Merge(JsonObject target, JsonObject source)
    {
        foreach (var kv in source)
        {
            if (kv.Value is null) target.Remove(kv.Key);
            else target[kv.Key] = Clone(kv.Value);
        }
    }

    private static string SetProjectInfo(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var project = ReadObj(doc, ProjectKey);
        var info = args["info"] as JsonObject
            ?? new JsonObject(args.Where(kv => kv.Key != "code").Select(kv => KeyValuePair.Create(kv.Key, Clone(kv.Value))));
        Merge(project, info);
        WriteJson(doc, ProjectKey, project);

        var code = ReadObj(doc, CodeKey);
        if (args["code"] is JsonObject codeArgs)
        {
            Merge(code, codeArgs);
            WriteJson(doc, CodeKey, code);
        }
        return JsonSerializer.Serialize(new { project, code });
    });

    private static string GetProjectInfo() => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        return JsonSerializer.Serialize(new
        {
            project = ReadObj(doc, ProjectKey),
            code = ReadObj(doc, CodeKey),
            levels = SortedLevels(doc),
            active_level = NullIfEmpty(doc.Strings.GetValue(ActiveLevelKey)),
            units = doc.ModelUnitSystem.ToString(),
        });
    });

    private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

    // ---- Levels --------------------------------------------------------------------

    private readonly record struct LevelRef(string? Name, double Elevation, double? Height);

    private static List<JsonObject> SortedLevels(RhinoDoc doc) =>
        ReadArr(doc, LevelsKey).OfType<JsonObject>().OrderBy(l => ToDouble(l["elevation"])).Select(l => (JsonObject)Clone(l)!).ToList();

    private static LevelRef ResolveLevel(RhinoDoc doc, JsonObject args)
    {
        var offset = Num(args, "z_offset");
        var name = NullIfEmpty(Str(args, "level")) ?? NullIfEmpty(doc.Strings.GetValue(ActiveLevelKey));
        if (name is null) return new LevelRef(null, offset, null);

        var lvl = ReadArr(doc, LevelsKey).OfType<JsonObject>()
            .FirstOrDefault(l => string.Equals(Str(l, "name"), name, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Level not found: {name}. Create it with create_level first.");
        return new LevelRef(Str(lvl, "name"), ToDouble(lvl["elevation"]) + offset,
            lvl["height"] is null ? null : ToDouble(lvl["height"]));
    }

    private static string CreateLevel(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var name = Str(args, "name") ?? throw new ArgumentException("name required");
        if (args["elevation"] is null) throw new ArgumentException("elevation required");
        var levels = ReadArr(doc, LevelsKey);
        var entry = levels.OfType<JsonObject>().FirstOrDefault(l => string.Equals(Str(l, "name"), name, StringComparison.OrdinalIgnoreCase));
        var created = entry is null;
        if (entry is null) { entry = new JsonObject { ["name"] = name }; levels.Add(entry); }
        entry["elevation"] = Num(args, "elevation");
        if (args["height"] is not null) entry["height"] = Num(args, "height");
        WriteJson(doc, LevelsKey, levels);
        if (Bool(args, "set_active")) doc.Strings.SetString(ActiveLevelKey, name);
        return JsonSerializer.Serialize(new { created, level = entry, levels = SortedLevels(doc), active_level = NullIfEmpty(doc.Strings.GetValue(ActiveLevelKey)) });
    });

    private static string ListLevels() => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var levels = SortedLevels(doc);
        return JsonSerializer.Serialize(new { count = levels.Count, active_level = NullIfEmpty(doc.Strings.GetValue(ActiveLevelKey)), levels });
    });

    private static string SetActiveLevel(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var lvl = ResolveLevel(doc, new JsonObject { ["level"] = Str(args, "name") ?? throw new ArgumentException("name required") });
        doc.Strings.SetString(ActiveLevelKey, lvl.Name!);
        return JsonSerializer.Serialize(new { active_level = lvl.Name, elevation = lvl.Elevation });
    });

    // ---- Element helpers -------------------------------------------------------------

    private sealed class Pr
    {
        public required JsonObject Args { get; init; }
        public JsonObject? Type { get; init; }
        public string? TypeId { get; init; }

        public double Num(string key, double fallback) =>
            Args[key] is not null ? ToolHelpers.Num(Args, key)
            : Type?[key] is { } t ? ToDouble(t)
            : fallback;

        public string? Str(string key) => ToolHelpers.Str(Args, key) ?? (Type is null ? null : ToolHelpers.Str(Type, key));

        public static Pr Resolve(RhinoDoc doc, JsonObject args, string kind)
        {
            var id = NullIfEmpty(ToolHelpers.Str(args, "type_id")) ?? NullIfEmpty(ToolHelpers.Str(args, kind + "_type_id"));
            if (id is null) return new Pr { Args = args };
            var type = ReadArr(doc, TypesKey).OfType<JsonObject>()
                .FirstOrDefault(t => string.Equals(ToolHelpers.Str(t, "type_id"), id, StringComparison.OrdinalIgnoreCase))
                ?? throw new ArgumentException($"Element type not found: {id}. Create it with upsert_element_type.");
            return new Pr { Args = args, Type = type, TypeId = ToolHelpers.Str(type, "type_id") };
        }
    }

    private static string Fmt(object v) => v switch
    {
        double d => Inv(d),
        float f => Inv(f),
        bool b => b ? "true" : "false",
        string s => s,
        _ => JsonSerializer.Serialize(v),
    };

    private static ObjectAttributes Attrs(RhinoDoc doc, JsonObject args, string kind, string? level, Pr? pr,
        params (string Key, object? Value)[] extra)
    {
        var a = BuildAttributes(doc, args);
        var d = KindDefaults[kind];
        if (string.IsNullOrWhiteSpace(Str(args, "layer")))
            a.LayerIndex = EnsureLayer(doc, d.Layer);
        a.SetUserString(Kind, kind);
        a.SetUserString("mcp4:ifc", pr?.Str("ifc") ?? d.Ifc);
        if (level is not null) a.SetUserString("mcp4:level", level);
        if (pr?.TypeId is not null) a.SetUserString("mcp4:type_id", pr.TypeId);
        var fire = pr?.Str("fire_rating");
        if (!string.IsNullOrWhiteSpace(fire)) a.SetUserString("mcp4:fire_rating", fire);
        foreach (var (k, v) in extra)
            if (v is not null) a.SetUserString("mcp4:" + k, Fmt(v));
        return a;
    }

    private static string PathJson(IEnumerable<Point3d> pts) =>
        JsonSerializer.Serialize(pts.Select(p => new[] { Math.Round(p.X, 6), Math.Round(p.Y, 6) }));

    private static List<Point3d> ReadPath(Dictionary<string, string> tags, string label)
    {
        if (!tags.TryGetValue("mcp4:path", out var json))
            throw new ArgumentException($"{label} has no mcp4:path (was it created by create_wall?).");
        var z = tags.TryGetValue("mcp4:base_z", out var zs) && double.TryParse(zs, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var zv) ? zv : 0;
        return (JsonNode.Parse(json) as JsonArray ?? throw new ArgumentException("Bad mcp4:path"))
            .Select(n => new Point3d(ToDouble(n![0]), ToDouble(n[1]), z)).ToList();
    }

    private static double TagNum(Dictionary<string, string> tags, string key, double fallback = 0) =>
        tags.TryGetValue(key, out var s) && double.TryParse(s, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : fallback;

    private static double UnitsPerMetre(RhinoDoc doc) => RhinoMath.UnitScale(UnitSystem.Meters, doc.ModelUnitSystem);

    private static double CodeNum(RhinoDoc doc, string key, double fallback) =>
        ReadObj(doc, CodeKey)[key] is { } n ? ToDouble(n) : fallback;

    private static Guid AddBrep(RhinoDoc doc, Brep brep, ObjectAttributes attrs, JsonObject args)
    {
        var id = doc.Objects.AddBrep(brep, attrs);
        if (id == Guid.Empty) throw new InvalidOperationException("Failed to add geometry to document.");
        ApplyGroup(doc, id, args);
        return id;
    }

    /// <summary>Merge disjoint breps into one object (falls back to separate objects).</summary>
    private static List<Guid> AddMerged(RhinoDoc doc, IList<Brep> breps, ObjectAttributes attrs, JsonObject args, double tol)
    {
        var merged = breps.Count == 1 ? breps[0] : Brep.MergeBreps(breps, tol);
        if (merged is not null) return [AddBrep(doc, merged, attrs, args)];
        return breps.Select(b => AddBrep(doc, b, attrs.Duplicate(), args)).ToList();
    }

    private static double LoopArea(IList<Point3d> pts)
    {
        double a = 0;
        for (var i = 0; i < pts.Count; i++)
        {
            var p = pts[i]; var q = pts[(i + 1) % pts.Count];
            a += p.X * q.Y - q.X * p.Y;
        }
        return a / 2;
    }

    private static List<Point3d> Flatten(IEnumerable<Point3d> pts, double z, double tol)
    {
        var list = pts.Select(p => new Point3d(p.X, p.Y, z)).ToList();
        for (var i = list.Count - 1; i > 0; i--)
            if (list[i].DistanceTo(list[i - 1]) < tol) list.RemoveAt(i);
        return list;
    }

    private static double ClosedArea(Curve crv) => AreaMassProperties.Compute(crv)?.Area ?? 0;

    private static object Result(Guid id, string kind, string? level, params (string, object?)[] extra)
    {
        var d = new Dictionary<string, object?> { ["id"] = id.ToString(), ["kind"] = kind, ["level"] = level };
        foreach (var (k, v) in extra) d[k] = v;
        return d;
    }

    // ---- Grid / site / building -------------------------------------------------------

    private static string Letters(int i)
    {
        var s = "";
        for (var n = i; n >= 0; n = n / 26 - 1) s = (char)('A' + n % 26) + s;
        return s;
    }

    private static string CreateGrid(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var lvl = ResolveLevel(doc, args);
        var gname = Str(args, "name") ?? "Grid";
        var origin = ParsePointOr(args, "origin", Point3d.Origin, lvl.Elevation);
        var xc = (int)Num(args, "x_count"); var yc = (int)Num(args, "y_count");
        var xs = Num(args, "x_spacing"); var ys = Num(args, "y_spacing");
        if (xc < 1 || yc < 1 || xs <= 0 || ys <= 0)
            throw new ArgumentException("x_count/y_count must be >= 1 and spacings positive.");
        var ext = Num(args, "extension", Metres(doc, 1));

        var ids = new List<string>();
        void Line(Point3d a, Point3d b, string axis, string label, double pos)
        {
            var attrs = Attrs(doc, args, "grid", lvl.Name, null, ("grid", gname), ("axis", axis), ("label", label), ("position", pos));
            attrs.Name = $"{gname} {label}";
            ids.Add(doc.Objects.AddLine(a, b, attrs).ToString());
        }
        for (var i = 0; i < xc; i++)
        {
            var x = origin.X + i * xs;
            Line(new Point3d(x, origin.Y - ext, origin.Z), new Point3d(x, origin.Y + (yc - 1) * ys + ext, origin.Z), "x", (i + 1).ToString(), x);
        }
        for (var j = 0; j < yc; j++)
        {
            var y = origin.Y + j * ys;
            Line(new Point3d(origin.X - ext, y, origin.Z), new Point3d(origin.X + (xc - 1) * xs + ext, y, origin.Z), "y", Letters(j), y);
        }
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { name = gname, count = ids.Count, ids, layer = KindDefaults["grid"].Layer });
    });

    private static string CreateSite(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var tol = doc.ModelAbsoluteTolerance;
        var crv = ClosedPolyline(ParsePoints(args["boundary"], "boundary"), tol);
        var area = ClosedArea(crv);
        var id = doc.Objects.AddCurve(crv, Attrs(doc, args, "site", null, null, ("area", area), ("boundary", PathJson(crv.ToPolyline()))));
        ApplyGroup(doc, id, args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(Result(id, "site", null, ("area", area), ("area_m2", area / Math.Pow(UnitsPerMetre(doc), 2))));
    });

    private static string CreateBuilding(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var tol = doc.ModelAbsoluteTolerance;
        var lvl = ResolveLevel(doc, args);
        Guid id;
        double? area = null;
        if (args["footprint"] is not null)
        {
            var crv = ClosedPolyline(ParsePoints(args["footprint"], "footprint", lvl.Elevation), tol);
            area = ClosedArea(crv);
            id = doc.Objects.AddCurve(crv, Attrs(doc, args, "building", lvl.Name, null, ("area", area), ("footprint", PathJson(crv.ToPolyline()))));
        }
        else
        {
            var p = ParsePointOr(args, "origin", Point3d.Origin, lvl.Elevation);
            id = doc.Objects.AddPoint(p, Attrs(doc, args, "building", lvl.Name, null));
        }
        ApplyGroup(doc, id, args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(Result(id, "building", lvl.Name, ("footprint_area", area)));
    });

    // ---- Walls -------------------------------------------------------------------------

    private static (List<Point3d> left, List<Point3d> right) OffsetBoth(IList<Point3d> p, bool closed, double half)
    {
        var n = p.Count;
        var left = new List<Point3d>(n); var right = new List<Point3d>(n);
        Vector3d Dir(Point3d a, Point3d b) { var v = b - a; v.Z = 0; v.Unitize(); return v; }
        Vector3d Nrm(Vector3d d) => new(-d.Y, d.X, 0);
        for (var i = 0; i < n; i++)
        {
            var hasPrev = closed || i > 0;
            var hasNext = closed || i < n - 1;
            Vector3d off;
            if (hasPrev && hasNext)
            {
                var n0 = Nrm(Dir(p[(i - 1 + n) % n], p[i]));
                var n1 = Nrm(Dir(p[i], p[(i + 1) % n]));
                var denom = 1 + n0 * n1;
                off = denom < 0.2 ? n0 * half : (n0 + n1) * (half / denom);
            }
            else
            {
                off = Nrm(hasNext ? Dir(p[i], p[i + 1]) : Dir(p[i - 1], p[i])) * half;
            }
            left.Add(p[i] + off);
            right.Add(p[i] - off);
        }
        return (left, right);
    }

    private static Brep BuildWallSolid(IList<Point3d> pts, bool closed, double thickness, double height, double tol)
    {
        var (left, right) = OffsetBoth(pts, closed, thickness / 2);
        var up = new Vector3d(0, 0, height);
        if (!closed)
        {
            right.Reverse();
            var outline = left.Concat(right).ToList();
            return ExtrudeProfile(ClosedPolyline(outline, tol), up, tol) ?? throw new InvalidOperationException("Wall extrusion failed.");
        }
        var (outer, inner) = Math.Abs(LoopArea(left)) >= Math.Abs(LoopArea(right)) ? (left, right) : (right, left);
        var ob = ExtrudeProfile(ClosedPolyline(outer, tol), up, tol) ?? throw new InvalidOperationException("Wall extrusion failed.");
        var ib = ExtrudeProfile(ClosedPolyline(inner, tol), up, tol) ?? throw new InvalidOperationException("Wall extrusion failed.");
        var diff = Brep.CreateBooleanDifference(ob, ib, tol);
        return diff is { Length: > 0 } ? diff[0] : throw new InvalidOperationException("Closed wall boolean failed (thickness may exceed ring size).");
    }

    private static string CreateWall(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var tol = doc.ModelAbsoluteTolerance;
        var lvl = ResolveLevel(doc, args);
        var pr = Pr.Resolve(doc, args, "wall");
        var raw = ParsePoints(args["path"], "path");
        var closed = Bool(args, "closed") || (raw.Count > 2 && raw[0].DistanceTo(raw[^1]) < tol);
        var pts = Flatten(raw, lvl.Elevation, tol);
        if (closed && pts.Count > 1 && pts[0].DistanceTo(pts[^1]) < tol) pts.RemoveAt(pts.Count - 1);
        if (pts.Count < (closed ? 3 : 2)) throw new ArgumentException("Wall path needs at least 2 distinct points (3 if closed).");

        var thickness = pr.Num("thickness", Metres(doc, 0.2));
        var height = pr.Num("height", lvl.Height ?? Metres(doc, 3.0));
        if (thickness <= 0 || height <= 0) throw new ArgumentException("thickness and height must be positive.");

        var brep = BuildWallSolid(pts, closed, thickness, height, tol);
        var length = pts.Zip(pts.Skip(1).Concat(closed ? [pts[0]] : []), (a, b) => a.DistanceTo(b)).Sum();
        var attrs = Attrs(doc, args, "wall", lvl.Name, pr, ("thickness", thickness), ("height", height),
            ("path", PathJson(pts)), ("base_z", lvl.Elevation), ("closed", closed), ("length", length));
        var id = AddBrep(doc, brep, attrs, args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(Result(id, "wall", lvl.Name, ("thickness", thickness), ("height", height), ("length", length), ("is_solid", brep.IsSolid)));
    });

    // ---- Slab / roof -------------------------------------------------------------------

    private static string CreateSlab(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var tol = doc.ModelAbsoluteTolerance;
        var lvl = ResolveLevel(doc, args);
        var pr = Pr.Resolve(doc, args, "slab");
        var thickness = pr.Num("thickness", Metres(doc, 0.25));
        if (thickness <= 0) throw new ArgumentException("thickness must be positive.");
        var crv = ClosedPolyline(Flatten(ParsePoints(args["boundary"], "boundary"), lvl.Elevation, tol), tol);
        var area = ClosedArea(crv);
        var brep = ExtrudeProfile(crv, new Vector3d(0, 0, -thickness), tol) ?? throw new InvalidOperationException("Slab extrusion failed.");
        var id = AddBrep(doc, brep, Attrs(doc, args, "slab", lvl.Name, pr, ("thickness", thickness), ("area", area), ("top_z", lvl.Elevation)), args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(Result(id, "slab", lvl.Name, ("thickness", thickness), ("area", area),
            ("area_m2", area / Math.Pow(UnitsPerMetre(doc), 2)), ("is_solid", brep.IsSolid)));
    });

    private static string CreateRoof(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var tol = doc.ModelAbsoluteTolerance;
        var lvl = ResolveLevel(doc, args);
        var pr = Pr.Resolve(doc, args, "roof");
        var thickness = pr.Num("thickness", Metres(doc, 0.2));
        if (thickness <= 0) throw new ArgumentException("thickness must be positive.");
        var slope = Num(args, "slope_deg", 0);
        if (slope < 0 || slope >= 89) throw new ArgumentException("slope_deg must be in [0, 89).");
        var dirRad = RhinoMath.ToRadians(Num(args, "slope_direction_deg", 90));
        var (cx, cy) = (Math.Cos(dirRad), Math.Sin(dirRad));

        var pts = Flatten(ParsePoints(args["boundary"], "boundary"), lvl.Elevation, tol);
        var minProj = pts.Min(p => p.X * cx + p.Y * cy);
        var tan = Math.Tan(RhinoMath.ToRadians(slope));
        var sloped = pts.Select(p => new Point3d(p.X, p.Y, lvl.Elevation + tan * (p.X * cx + p.Y * cy - minProj))).ToList();
        var crv = ClosedPolyline(sloped, tol);
        var plan = ClosedPolyline(pts, tol);
        var area = ClosedArea(plan);
        var brep = ExtrudeProfile(crv, new Vector3d(0, 0, thickness), tol) ?? throw new InvalidOperationException("Roof extrusion failed.");
        var id = AddBrep(doc, brep, Attrs(doc, args, "roof", lvl.Name, pr, ("thickness", thickness), ("slope_deg", slope), ("plan_area", area)), args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(Result(id, "roof", lvl.Name, ("thickness", thickness), ("slope_deg", slope),
            ("plan_area", area), ("is_solid", brep.IsSolid)));
    });

    // ---- Column / beam -----------------------------------------------------------------

    private static string CreateColumn(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var tol = doc.ModelAbsoluteTolerance;
        var lvl = ResolveLevel(doc, args);
        var pr = Pr.Resolve(doc, args, "column");
        var b = ParsePoint(args["base"], "base", lvl.Elevation);
        var height = pr.Num("height", lvl.Height ?? Metres(doc, 3.0));
        if (height <= 0) throw new ArgumentException("height must be positive.");
        var radius = pr.Num("radius", 0);

        Brep brep;
        object extra;
        if (radius > 0)
        {
            brep = new Cylinder(new Circle(new Plane(b, Vector3d.ZAxis), radius), height).ToBrep(true, true)
                ?? throw new InvalidOperationException("Failed to create column cylinder.");
            extra = new { radius };
        }
        else
        {
            var width = pr.Num("width", Metres(doc, 0.4));
            var depth = pr.Num("depth", width);
            if (width <= 0 || depth <= 0) throw new ArgumentException("width/depth must be positive.");
            brep = OrientedBox(b, Vector3d.XAxis, width, depth, height);
            extra = new { width, depth };
        }
        var id = AddBrep(doc, brep, Attrs(doc, args, "column", lvl.Name, pr, ("height", height), ("base", Pt(b))), args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(Result(id, "column", lvl.Name, ("height", height), ("section", extra), ("base", Pt(b))));
    });

    private static string CreateBeam(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var tol = doc.ModelAbsoluteTolerance;
        var lvl = ResolveLevel(doc, args);
        var pr = Pr.Resolve(doc, args, "beam");
        var s = ParsePoint(args["start"], "start", lvl.Elevation);
        var e = ParsePoint(args["end"], "end", lvl.Elevation);
        var axis = e - s;
        var length = axis.Length;
        if (length < tol || !axis.Unitize()) throw new ArgumentException("Beam start and end must differ.");
        var width = pr.Num("width", Metres(doc, 0.3));
        var height = pr.Num("height", Metres(doc, 0.5));
        if (width <= 0 || height <= 0) throw new ArgumentException("width/height must be positive.");

        var side = Vector3d.CrossProduct(Vector3d.ZAxis, axis);
        if (side.Length < 1e-6) side = Vector3d.XAxis;
        side.Unitize();
        var up = Vector3d.CrossProduct(axis, side);
        var plane = new Plane(s, side, up);
        var box = new Box(plane, new Interval(-width / 2, width / 2), new Interval(-height / 2, height / 2), new Interval(0, length));
        var brep = box.ToBrep() ?? throw new InvalidOperationException("Failed to create beam.");
        var id = AddBrep(doc, brep, Attrs(doc, args, "beam", lvl.Name, pr, ("width", width), ("height", height), ("length", length)), args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(Result(id, "beam", lvl.Name, ("width", width), ("height", height), ("length", length)));
    });

    // ---- Openings: door / window / apply_opening --------------------------------------

    private static (Point3d p, Vector3d d, double t) LocateOnPath(IList<Point3d> pts, bool closed, double? t, Point3d? near)
    {
        var segs = new List<(Point3d a, Point3d b, double len)>();
        for (var i = 0; i + 1 < pts.Count; i++) segs.Add((pts[i], pts[i + 1], pts[i].DistanceTo(pts[i + 1])));
        if (closed) segs.Add((pts[^1], pts[0], pts[^1].DistanceTo(pts[0])));
        var total = segs.Sum(s => s.len);
        if (total <= 0) throw new ArgumentException("Wall path has zero length.");

        double target;
        if (near is { } q)
        {
            var best = double.MaxValue; target = 0; double cum = 0;
            foreach (var (a, b, len) in segs)
            {
                var ab = b - a;
                var s = len < 1e-12 ? 0 : Math.Clamp(((q - a) * ab) / (len * len), 0, 1);
                var d = (a + ab * s).DistanceTo(q);
                if (d < best) { best = d; target = cum + s * len; }
                cum += len;
            }
        }
        else
        {
            target = Math.Clamp(t ?? 0.5, 0, 1) * total;
        }

        double acc = 0;
        foreach (var (a, b, len) in segs)
        {
            if (target <= acc + len + 1e-9 || ReferenceEquals(segs[^1].a, a))
            {
                var f = len < 1e-12 ? 0 : Math.Clamp((target - acc) / len, 0, 1);
                var dir = b - a; dir.Z = 0; dir.Unitize();
                return (a + (b - a) * f, dir, target / total);
            }
            acc += len;
        }
        throw new InvalidOperationException("Failed to locate point on wall path.");
    }

    /// <summary>Subtract <paramref name="cutter"/> from the host object, replacing its geometry in place.</summary>
    private static void CutHost(RhinoDoc doc, RhinoObject host, Brep cutter)
    {
        var hb = ToBrep(host.Geometry) ?? throw new ArgumentException("Host is not a Brep/solid.");
        if (!BoundingBox.Intersection(hb.GetBoundingBox(true), cutter.GetBoundingBox(true)).IsValid)
            throw new ArgumentException("Opening does not intersect the host.");
        var res = Brep.CreateBooleanDifference(hb, cutter, doc.ModelAbsoluteTolerance);
        if (res is null || res.Length == 0)
            throw new InvalidOperationException("Opening cut failed (host must be a closed solid and the cutter must overlap it).");
        var result = res.Length == 1 ? res[0] : Brep.MergeBreps(res, doc.ModelAbsoluteTolerance) ?? res[0];
        if (!doc.Objects.Replace(host.Id, result))
            throw new InvalidOperationException("Failed to replace host geometry.");
    }

    private static string CreateOpeningElement(JsonObject args, string kind) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var tol = doc.ModelAbsoluteTolerance;
        var hostId = IdArg(args, "host_wall_id");
        var host = FindObject(doc, hostId);
        var htags = ReadTags(host);
        if (!htags.TryGetValue(Kind, out var hk) || !hk.Equals("wall", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("host_wall_id must reference a wall created by create_wall.");
        var closed = htags.TryGetValue("mcp4:closed", out var cs) && cs == "true";
        var path = ReadPath(htags, "Host wall");
        var thickness = TagNum(htags, "mcp4:thickness", Metres(doc, 0.2));
        var wallHeight = TagNum(htags, "mcp4:height", Metres(doc, 3.0));
        var baseZ = TagNum(htags, "mcp4:base_z");

        var pr = Pr.Resolve(doc, args, kind);
        var isDoor = kind == "door";
        var width = pr.Num("width", Metres(doc, isDoor ? 0.9 : 1.2));
        var height = pr.Num("height", Metres(doc, isDoor ? 2.1 : 1.2));
        var sill = pr.Num("sill", isDoor ? 0 : Metres(doc, 0.9));
        if (width <= 0 || height <= 0 || sill < 0) throw new ArgumentException("width/height must be positive and sill >= 0.");
        if (sill + height > wallHeight + tol) throw new ArgumentException("Opening is taller than the host wall.");

        var (p, d, t) = args["position"] is not null
            ? LocateOnPath(path, closed, null, ParsePoint(args["position"], "position", baseZ))
            : LocateOnPath(path, closed, args["t_param"] is null ? 0.5 : Num(args, "t_param"), null);

        var bottom = new Point3d(p.X, p.Y, baseZ + sill);
        using var cutter = OrientedBox(bottom, d, width, thickness * 1.5, height);
        CutHost(doc, host, cutter);

        // Fill: door leaf / glass pane centred in the wall thickness.
        var fillT = Metres(doc, isDoor ? 0.04 : 0.02);
        var fill = OrientedBox(bottom, d, width, Math.Min(fillT, thickness), height);
        var attrs = Attrs(doc, args, kind, htags.GetValueOrDefault("mcp4:level"), pr,
            ("host", hostId), ("t", t), ("width", width), ("height", height), ("sill", sill), ("position", Pt(bottom)));
        var id = AddBrep(doc, fill, attrs, args);

        // Record opening on the host.
        var refreshed = FindObject(doc, hostId);
        var openings = refreshed.Attributes.GetUserString("mcp4:openings") is { Length: > 0 } ex
            ? JsonNode.Parse(ex) as JsonArray ?? new JsonArray() : new JsonArray();
        openings.Add(new JsonObject { ["id"] = id.ToString(), ["kind"] = kind, ["t"] = Math.Round(t, 6), ["width"] = width, ["height"] = height, ["sill"] = sill });
        refreshed.Attributes.SetUserString("mcp4:openings", openings.ToJsonString());
        refreshed.CommitChanges();

        doc.Views.Redraw();
        return JsonSerializer.Serialize(Result(id, kind, htags.GetValueOrDefault("mcp4:level"),
            ("host_wall_id", hostId.ToString()), ("t_param", t), ("width", width), ("height", height), ("sill", sill), ("position", Pt(bottom))));
    });

    private static string ApplyOpening(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var hostId = IdArg(args, "host_id");
        var host = FindObject(doc, hostId);

        Brep cutter;
        Guid? cutterId = null;
        if (args["cutter_id"] is not null)
        {
            cutterId = IdArg(args, "cutter_id");
            cutter = ToBrep(FindObject(doc, cutterId.Value).Geometry) ?? throw new ArgumentException("cutter_id must be a Brep/solid.");
        }
        else
        {
            var w = Num(args, "width"); var h = Num(args, "height");
            var htags = ReadTags(host);
            var depth = args["depth"] is null ? TagNum(htags, "mcp4:thickness", Metres(doc, 0.2)) * 1.5 : Num(args, "depth");
            if (w <= 0 || h <= 0 || depth <= 0) throw new ArgumentException("Provide cutter_id, or positive width, height (and position).");
            var pos = ParsePoint(args["position"], "position");
            var rot = RhinoMath.ToRadians(Num(args, "rotation_deg_z"));
            cutter = OrientedBox(pos, new Vector3d(Math.Cos(rot), Math.Sin(rot), 0), w, depth, h);
        }
        CutHost(doc, host, cutter);
        if (cutterId is { } cid && !Bool(args, "keep_cutter")) doc.Objects.Delete(cid, true);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { host_id = hostId.ToString(), cut = true });
    });

    // ---- Stair / ramp / railing / curtain wall -------------------------------------------

    private static string CreateStair(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var tol = doc.ModelAbsoluteTolerance;
        var lvl = ResolveLevel(doc, args);
        var s = ParsePoint(args["start"], "start", lvl.Elevation);
        var e = ParsePoint(args["end"], "end", lvl.Elevation);
        var run = e - s; run.Z = 0;
        var runLen = run.Length;
        if (runLen < tol) throw new ArgumentException("start and end must differ horizontally.");
        run.Unitize();

        var rise = args["total_rise"] is not null ? Num(args, "total_rise") : e.Z - s.Z;
        if (rise <= tol) throw new ArgumentException("total_rise must be positive (or give end z above start z).");
        int n;
        if (args["tread_count"] is not null) n = (int)Num(args, "tread_count");
        else if (args["riser_height"] is not null) n = (int)Math.Ceiling(rise / Num(args, "riser_height") - 1e-9);
        else throw new ArgumentException("Provide tread_count or riser_height.");
        if (n < 1) throw new ArgumentException("Stair needs at least 1 step.");
        var width = Num(args, "width", Metres(doc, 1.0));
        if (width <= 0) throw new ArgumentException("width must be positive.");

        var riser = rise / n; var tread = runLen / n;
        var profile = new List<(double u, double v)> { (0, 0) };
        for (var i = 0; i < n; i++)
        {
            profile.Add((i * tread, (i + 1) * riser));
            profile.Add(((i + 1) * tread, (i + 1) * riser));
        }
        profile.Add((n * tread, 0));

        var upm = UnitsPerMetre(doc);
        var warnings = new List<string>();
        var maxRiser = CodeNum(doc, "stair_max_riser_m", 0.19); var minTread = CodeNum(doc, "stair_min_tread_m", 0.25);
        if (riser / upm > maxRiser + 1e-9) warnings.Add($"riser {riser / upm:0.###} m exceeds max {maxRiser} m");
        if (tread / upm < minTread - 1e-9) warnings.Add($"tread {tread / upm:0.###} m below min {minTread} m");

        var brep = SideProfileSolid(new Point3d(s.X, s.Y, s.Z), run, width, profile, tol);
        var id = AddBrep(doc, brep, Attrs(doc, args, "stair", lvl.Name, null, ("steps", n), ("riser_height", riser), ("tread_depth", tread),
            ("total_rise", rise), ("width", width)), args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(Result(id, "stair", lvl.Name, ("steps", n), ("riser_height", riser), ("tread_depth", tread),
            ("total_rise", rise), ("run", runLen), ("width", width), ("warnings", warnings), ("is_solid", brep.IsSolid)));
    });

    private static string CreateRamp(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var tol = doc.ModelAbsoluteTolerance;
        var lvl = ResolveLevel(doc, args);
        var s = ParsePoint(args["start"], "start", lvl.Elevation);
        var e = ParsePoint(args["end"], "end", lvl.Elevation);
        var run = e - s; run.Z = 0;
        var runLen = run.Length;
        if (runLen < tol) throw new ArgumentException("start and end must differ horizontally.");
        run.Unitize();
        var width = Num(args, "width", Metres(doc, 1.2));
        var thickness = Num(args, "thickness", Metres(doc, 0.15));
        if (width <= 0 || thickness <= 0) throw new ArgumentException("width/thickness must be positive.");

        var rise = e.Z - s.Z;
        var slope = Math.Abs(rise) / runLen;
        var maxSlope = CodeNum(doc, "ramp_max_slope", 1.0 / 12);
        var compliant = slope <= maxSlope + 1e-9;
        var warnings = compliant ? new List<string>() : [$"slope {slope:0.####} ({slope * 100:0.#}%) exceeds max {maxSlope:0.####}"];

        var profile = new List<(double u, double v)> { (0, 0), (runLen, rise), (runLen, rise - thickness), (0, -thickness) };
        var brep = SideProfileSolid(s, run, width, profile, tol);
        var id = AddBrep(doc, brep, Attrs(doc, args, "ramp", lvl.Name, null, ("width", width), ("slope", slope), ("rise", rise), ("run", runLen)), args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(Result(id, "ramp", lvl.Name, ("slope", slope), ("slope_percent", slope * 100),
            ("max_slope", maxSlope), ("compliant", compliant), ("warnings", warnings), ("rise", rise), ("run", runLen)));
    });

    private static string CreateRailing(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var tol = doc.ModelAbsoluteTolerance;
        var lvl = ResolveLevel(doc, args);
        var pts = ParsePoints(args["path"], "path", lvl.Elevation);
        if (pts.Count < 2) throw new ArgumentException("Railing path needs at least 2 points.");
        var height = Num(args, "height", Metres(doc, 1.0));
        var spacing = Num(args, "post_spacing", Metres(doc, 1.2));
        var post = Metres(doc, 0.05); var rail = Metres(doc, 0.05);
        if (height <= rail || spacing <= 0) throw new ArgumentException("height must exceed rail size and post_spacing must be positive.");

        var parts = new List<Brep>();
        for (var i = 0; i + 1 < pts.Count; i++)
        {
            var a = pts[i]; var b = pts[i + 1];
            var v = b - a; var len = v.Length;
            if (len < tol) continue;
            var dir = v / len;
            var mid = (a + b) / 2;
            parts.Add(OrientedBox(new Point3d(mid.X, mid.Y, Math.Min(a.Z, b.Z) + height - rail), dir, len + post, rail, rail));
            var count = Math.Max(1, (int)Math.Ceiling(len / spacing));
            for (var k = (i == 0 ? 0 : 1); k <= count; k++)
            {
                var p = a + v * (k / (double)count);
                parts.Add(OrientedBox(new Point3d(p.X, p.Y, p.Z), dir, post, post, height - rail));
            }
        }
        if (parts.Count == 0) throw new ArgumentException("Railing path has no non-zero segments.");
        var ids = AddMerged(doc, parts, Attrs(doc, args, "railing", lvl.Name, null, ("height", height)), args, tol);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { ids = ids.Select(i => i.ToString()), id = ids[0].ToString(), kind = "railing", level = lvl.Name, height });
    });

    private static string CreateCurtainWall(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var tol = doc.ModelAbsoluteTolerance;
        var lvl = ResolveLevel(doc, args);
        var raw = ParsePoints(args["path"], "path");
        var pts = Flatten(raw, lvl.Elevation, tol);
        if (pts.Count < 2) throw new ArgumentException("Curtain wall path needs at least 2 distinct points.");
        var height = Num(args, "height");
        var spacing = Num(args, "mullion_spacing", Metres(doc, 1.5));
        var mull = Metres(doc, 0.06); var frameDepth = Metres(doc, 0.15); var glassT = Metres(doc, 0.03);
        if (height <= 2 * mull || spacing <= 2 * mull) throw new ArgumentException("height/mullion_spacing too small.");

        var z = lvl.Elevation;
        var frame = new List<Brep>(); var glass = new List<Brep>();
        var bays = 0;
        for (var i = 0; i + 1 < pts.Count; i++)
        {
            var a = pts[i]; var b = pts[i + 1];
            var v = b - a; var len = v.Length; var dir = v / len;
            var nb = Math.Max(1, (int)Math.Round(len / spacing));
            var bay = len / nb;
            for (var k = (i == 0 ? 0 : 1); k <= nb; k++)
            {
                var p = a + v * (k / (double)nb);
                frame.Add(OrientedBox(new Point3d(p.X, p.Y, z), dir, mull, frameDepth, height));
            }
            var mid = (a + b) / 2;
            frame.Add(OrientedBox(new Point3d(mid.X, mid.Y, z), dir, len, frameDepth, mull));
            frame.Add(OrientedBox(new Point3d(mid.X, mid.Y, z + height - mull), dir, len, frameDepth, mull));
            for (var k = 0; k < nb; k++)
            {
                var c = a + v * ((k + 0.5) / nb);
                glass.Add(OrientedBox(new Point3d(c.X, c.Y, z + mull), dir, bay - mull, glassT, height - 2 * mull));
                bays++;
            }
        }

        string[] common = ["curtain_wall"];
        var frameIds = AddMerged(doc, frame, Attrs(doc, args, "wall", lvl.Name, null, ("subtype", common[0]), ("part", "frame"), ("height", height), ("path", PathJson(pts)), ("base_z", z)), args, tol);
        var glassAttrs = Attrs(doc, args, "wall", lvl.Name, null, ("subtype", common[0]), ("part", "glass"), ("height", height), ("path", PathJson(pts)), ("base_z", z));
        glassAttrs.LayerIndex = EnsureLayer(doc, KindDefaults["window"].Layer);
        var glassIds = AddMerged(doc, glass, glassAttrs, args, tol);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new
        {
            kind = "wall", subtype = "curtain_wall", level = lvl.Name, bays, height,
            frame_ids = frameIds.Select(i => i.ToString()), glass_ids = glassIds.Select(i => i.ToString()),
        });
    });

    // ---- Spaces ------------------------------------------------------------------------

    private static Brep SpaceSurface(List<Point3d> pts, double tol)
    {
        var crv = ClosedPolyline(pts, tol);
        var breps = Brep.CreatePlanarBreps(crv, tol);
        return breps is { Length: > 0 } ? breps[0] : throw new InvalidOperationException("Could not create a planar surface from the boundary (self-intersecting or non-planar?).");
    }

    private static void SetSpaceTags(RhinoDoc doc, ObjectAttributes a, double area, double? factor)
    {
        var m2 = area / Math.Pow(UnitsPerMetre(doc), 2);
        a.SetUserString("mcp4:area", Inv(area));
        a.SetUserString("mcp4:area_m2", Inv(m2));
        if (factor is > 0)
        {
            a.SetUserString("mcp4:occupancy_factor", Inv(factor.Value));
            a.SetUserString("mcp4:occupant_load", ((int)Math.Ceiling(m2 / factor.Value - 1e-9)).ToString());
        }
    }

    private static string CreateSpace(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var tol = doc.ModelAbsoluteTolerance;
        var lvl = ResolveLevel(doc, args);
        var pts = Flatten(ParsePoints(args["boundary"], "boundary"), lvl.Elevation, tol);
        var brep = SpaceSurface(pts, tol);
        var area = AreaMassProperties.Compute(brep)?.Area ?? 0;
        var height = Num(args, "height", lvl.Height ?? Metres(doc, 3.0));
        double? factor = args["occupancy_factor"] is null ? null : Num(args, "occupancy_factor");

        var attrs = Attrs(doc, args, "space", lvl.Name, null, ("height", height), ("use", Str(args, "use")), ("boundary", PathJson(pts)), ("base_z", lvl.Elevation));
        SetSpaceTags(doc, attrs, area, factor);
        var id = AddBrep(doc, brep, attrs, args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(SpaceBrief(doc, FindObject(doc, id)));
    });

    private static object SpaceBrief(RhinoDoc doc, RhinoObject o)
    {
        var t = ReadTags(o);
        return new
        {
            id = o.Id.ToString(),
            name = o.Name ?? "",
            level = t.GetValueOrDefault("mcp4:level"),
            use = t.GetValueOrDefault("mcp4:use"),
            height = TagNum(t, "mcp4:height"),
            area = TagNum(t, "mcp4:area"),
            area_m2 = TagNum(t, "mcp4:area_m2"),
            occupancy_factor = t.ContainsKey("mcp4:occupancy_factor") ? TagNum(t, "mcp4:occupancy_factor") : (double?)null,
            occupant_load = t.ContainsKey("mcp4:occupant_load") ? (int)TagNum(t, "mcp4:occupant_load") : (int?)null,
        };
    }

    private static string UpdateSpace(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var tol = doc.ModelAbsoluteTolerance;
        var id = IdArg(args, "id");
        var obj = FindObject(doc, id);
        var tags = ReadTags(obj);
        if (!tags.TryGetValue(Kind, out var k) || k != "space") throw new ArgumentException("Object is not a space.");

        var attrs = obj.Attributes;
        if (args["name"] is not null) attrs.Name = Str(args, "name");
        if (args["height"] is not null) attrs.SetUserString("mcp4:height", Inv(Num(args, "height")));
        if (args["use"] is not null) attrs.SetUserString("mcp4:use", Str(args, "use") ?? "");
        if (args["level"] is not null) attrs.SetUserString("mcp4:level", Str(args, "level") ?? "");

        var area = TagNum(tags, "mcp4:area");
        if (args["boundary"] is not null)
        {
            var z = TagNum(tags, "mcp4:base_z");
            var pts = Flatten(ParsePoints(args["boundary"], "boundary"), z, tol);
            var brep = SpaceSurface(pts, tol);
            area = AreaMassProperties.Compute(brep)?.Area ?? 0;
            if (!doc.Objects.Replace(id, brep)) throw new InvalidOperationException("Failed to replace space geometry.");
            obj = FindObject(doc, id);
            attrs = obj.Attributes;
            attrs.SetUserString("mcp4:boundary", PathJson(pts));
        }
        double? factor = args["occupancy_factor"] is not null ? Num(args, "occupancy_factor")
            : tags.ContainsKey("mcp4:occupancy_factor") ? TagNum(tags, "mcp4:occupancy_factor") : null;
        SetSpaceTags(doc, attrs, area, factor);
        obj.CommitChanges();
        doc.Views.Redraw();
        return JsonSerializer.Serialize(SpaceBrief(doc, FindObject(doc, id)));
    });

    private static string ListSpaces(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var level = Str(args, "level");
        var spaces = Elements(doc)
            .Where(e => e.tags[Kind] == "space")
            .Where(e => string.IsNullOrWhiteSpace(level) || string.Equals(e.tags.GetValueOrDefault("mcp4:level"), level, StringComparison.OrdinalIgnoreCase))
            .Select(e => SpaceBrief(doc, e.obj)).ToList();
        return JsonSerializer.Serialize(new { count = spaces.Count, spaces });
    });

    // ---- Element types -----------------------------------------------------------------

    private static string ListElementTypes(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var kind = Str(args, "kind");
        var types = ReadArr(doc, TypesKey).OfType<JsonObject>()
            .Where(t => string.IsNullOrWhiteSpace(kind) || string.Equals(Str(t, "kind"), kind, StringComparison.OrdinalIgnoreCase)).ToList();
        return JsonSerializer.Serialize(new { count = types.Count, types });
    });

    private static string UpsertElementType(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var id = Str(args, "type_id") ?? throw new ArgumentException("type_id required");
        var types = ReadArr(doc, TypesKey);
        var entry = types.OfType<JsonObject>().FirstOrDefault(t => string.Equals(Str(t, "type_id"), id, StringComparison.OrdinalIgnoreCase));
        var created = entry is null;
        if (entry is null) { entry = new JsonObject { ["type_id"] = id }; types.Add(entry); }
        foreach (var key in new[] { "kind", "name", "thickness", "height", "width", "depth", "fire_rating", "ifc", "description" })
            if (args[key] is not null) entry[key] = Clone(args[key]);
        if (args["props"] is JsonObject props) Merge(entry, props);
        WriteJson(doc, TypesKey, types);
        return JsonSerializer.Serialize(new { created, type = entry });
    });

    // ---- Queries -----------------------------------------------------------------------

    private static IEnumerable<(RhinoObject obj, Dictionary<string, string> tags)> Elements(RhinoDoc doc)
    {
        foreach (var obj in doc.Objects)
        {
            if (obj is null || obj.IsDeleted) continue;
            var tags = ReadTags(obj);
            if (tags.ContainsKey(Kind)) yield return (obj, tags);
        }
    }

    private static object ElementBrief(RhinoDoc doc, RhinoObject o, Dictionary<string, string> t) => new
    {
        id = o.Id.ToString(),
        kind = t[Kind],
        name = o.Name ?? "",
        level = t.GetValueOrDefault("mcp4:level"),
        type_id = t.GetValueOrDefault("mcp4:type_id"),
        ifc = t.GetValueOrDefault("mcp4:ifc"),
        layer = doc.Layers[o.Attributes.LayerIndex]?.FullPath ?? "",
        tags = t.Where(kv => kv.Key.StartsWith("mcp4:", StringComparison.OrdinalIgnoreCase) && kv.Key != "mcp4:path" && kv.Key != "mcp4:boundary")
            .ToDictionary(kv => kv.Key, kv => kv.Value),
        bbox = BBoxObj(o.Geometry.GetBoundingBox(true)),
    };

    private static string QueryElements(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var kinds = (Str(args, "kind") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var level = Str(args, "level"); var typeId = Str(args, "type_id");
        var tagKey = Str(args, "tag_key"); var tagValue = Str(args, "tag_value");
        var name = Str(args, "name"); var layerF = Str(args, "layer");
        var limit = Math.Max(1, (int)Num(args, "limit", 1000));

        var items = new List<object>(); var total = 0;
        foreach (var (obj, t) in Elements(doc))
        {
            if (kinds.Length > 0 && !kinds.Contains(t[Kind], StringComparer.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrWhiteSpace(level) && !string.Equals(t.GetValueOrDefault("mcp4:level"), level, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrWhiteSpace(typeId) && !string.Equals(t.GetValueOrDefault("mcp4:type_id"), typeId, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrWhiteSpace(tagKey))
            {
                if (!t.TryGetValue(tagKey, out var tv)) continue;
                if (!string.IsNullOrWhiteSpace(tagValue) && tv.IndexOf(tagValue, StringComparison.OrdinalIgnoreCase) < 0) continue;
            }
            if (!string.IsNullOrWhiteSpace(name) && (obj.Name ?? "").IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0) continue;
            var ln = doc.Layers[obj.Attributes.LayerIndex]?.FullPath ?? "";
            if (!string.IsNullOrWhiteSpace(layerF) && ln.IndexOf(layerF, StringComparison.OrdinalIgnoreCase) < 0) continue;
            total++;
            if (items.Count < limit) items.Add(ElementBrief(doc, obj, t));
        }
        return JsonSerializer.Serialize(new { count = total, returned = items.Count, elements = items });
    });

    private static string GetBuildingModel(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var includeElements = Bool(args, "include_elements", true);
        var limit = Math.Max(1, (int)Num(args, "limit", 500));
        var all = Elements(doc).ToList();

        var levels = SortedLevels(doc).Select(l =>
        {
            var lname = Str(l, "name");
            var onLevel = all.Where(e => string.Equals(e.tags.GetValueOrDefault("mcp4:level"), lname, StringComparison.OrdinalIgnoreCase)).ToList();
            return new
            {
                name = lname,
                elevation = ToDouble(l["elevation"]),
                height = l["height"] is null ? (double?)null : ToDouble(l["height"]),
                element_counts = onLevel.GroupBy(e => e.tags[Kind]).ToDictionary(g => g.Key, g => g.Count()),
                spaces = onLevel.Where(e => e.tags[Kind] == "space").Select(e => SpaceBrief(doc, e.obj)).ToList(),
                space_area_m2 = onLevel.Where(e => e.tags[Kind] == "space").Sum(e => TagNum(e.tags, "mcp4:area_m2")),
                occupant_load = onLevel.Where(e => e.tags[Kind] == "space").Sum(e => (int)TagNum(e.tags, "mcp4:occupant_load")),
            };
        }).ToList();

        var levelNames = new HashSet<string>(levels.Select(l => l.name ?? ""), StringComparer.OrdinalIgnoreCase);
        var unassigned = all.Where(e => !levelNames.Contains(e.tags.GetValueOrDefault("mcp4:level") ?? "")).ToList();
        return JsonSerializer.Serialize(new
        {
            project = ReadObj(doc, ProjectKey),
            code = ReadObj(doc, CodeKey),
            units = doc.ModelUnitSystem.ToString(),
            active_level = NullIfEmpty(doc.Strings.GetValue(ActiveLevelKey)),
            levels,
            element_counts = all.GroupBy(e => e.tags[Kind]).ToDictionary(g => g.Key, g => g.Count()),
            total_elements = all.Count,
            unassigned_count = unassigned.Count,
            element_types = ReadArr(doc, TypesKey),
            elements = includeElements ? all.Take(limit).Select(e => ElementBrief(doc, e.obj, e.tags)).ToList() : null,
        });
    });

    // ---- Measure -------------------------------------------------------------------------

    private static string MeasureDistance(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var upm = UnitsPerMetre(doc);
        if (args["a"] is not null || args["b"] is not null)
        {
            var a = ParsePoint(args["a"], "a"); var b = ParsePoint(args["b"], "b");
            var d = a.DistanceTo(b);
            return JsonSerializer.Serialize(new { distance = d, distance_m = d / upm, dx = b.X - a.X, dy = b.Y - a.Y, dz = b.Z - a.Z });
        }
        var ids = IdsArg(args);
        if (ids.Count != 2) throw new ArgumentException("Provide two ids, or points a and b.");
        var ba = FindObject(doc, ids[0]).Geometry.GetBoundingBox(true);
        var bb = FindObject(doc, ids[1]).Geometry.GetBoundingBox(true);
        var center = ba.Center.DistanceTo(bb.Center);
        double Gap(double a0, double a1, double b0, double b1) => Math.Max(0, Math.Max(b0 - a1, a0 - b1));
        var gap = new Vector3d(Gap(ba.Min.X, ba.Max.X, bb.Min.X, bb.Max.X), Gap(ba.Min.Y, ba.Max.Y, bb.Min.Y, bb.Max.Y), Gap(ba.Min.Z, ba.Max.Z, bb.Min.Z, bb.Max.Z)).Length;
        return JsonSerializer.Serialize(new { center_distance = center, center_distance_m = center / upm, bbox_gap = gap, bbox_gap_m = gap / upm });
    });

    private static string MeasureArea(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var tol = doc.ModelAbsoluteTolerance;
        var u2 = Math.Pow(UnitsPerMetre(doc), 2);
        var items = new List<object>(); double total = 0;
        if (args["points"] is not null)
        {
            var crv = ClosedPolyline(ParsePoints(args["points"]), tol);
            var a = ClosedArea(crv);
            total += a; items.Add(new { source = "points", area = a, area_m2 = a / u2 });
        }
        if (args["ids"] is not null || args["id"] is not null)
        {
            foreach (var id in IdsArg(args))
            {
                var g = FindObject(doc, id).Geometry;
                double? a = g switch
                {
                    Curve c when c.IsClosed => AreaMassProperties.Compute(c)?.Area,
                    Mesh m => AreaMassProperties.Compute(m)?.Area,
                    _ => ToBrep(g) is { } b ? AreaMassProperties.Compute(b)?.Area : null,
                };
                items.Add(new { id = id.ToString(), area = a, area_m2 = a / u2 });
                total += a ?? 0;
            }
        }
        if (items.Count == 0) throw new ArgumentException("Provide ids or points.");
        return JsonSerializer.Serialize(new { total_area = total, total_area_m2 = total / u2, items });
    });

    private static string MeasureClearWidth(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var upm = UnitsPerMetre(doc);
        var minM = CodeNum(doc, "door_min_clear_width_m", 0.81);

        if (args["id"] is not null)
        {
            var id = IdArg(args, "id");
            var t = ReadTags(FindObject(doc, id));
            if (!t.TryGetValue(Kind, out var k) || (k != "door" && k != "window"))
                throw new ArgumentException("id must be a door or window created by create_door/create_window.");
            var w = TagNum(t, "mcp4:width");
            return JsonSerializer.Serialize(new { kind = k, clear_width = w, clear_width_m = w / upm, min_required_m = k == "door" ? minM : (double?)null, compliant = k == "door" ? w / upm >= minM - 1e-9 : (bool?)null });
        }
        if (args["a"] is not null && args["b"] is not null)
        {
            var oa = FindObject(doc, IdArg(args, "a")); var ob = FindObject(doc, IdArg(args, "b"));
            var ta = ReadTags(oa); var tb = ReadTags(ob);
            if (ta.ContainsKey("mcp4:path") && tb.ContainsKey("mcp4:path"))
            {
                using var ca = new PolylineCurve(ReadPath(ta, "a"));
                using var cb = new PolylineCurve(ReadPath(tb, "b"));
                if (!ca.ClosestPoints(cb, out var pa, out var pb)) throw new InvalidOperationException("Could not compute distance between wall centerlines.");
                var w = Math.Max(0, pa.DistanceTo(pb) - TagNum(ta, "mcp4:thickness") / 2 - TagNum(tb, "mcp4:thickness") / 2);
                return JsonSerializer.Serialize(new { method = "wall_centerline_minus_half_thickness", clear_width = w, clear_width_m = w / upm, from = Pt(pa), to = Pt(pb) });
            }
            var ba = oa.Geometry.GetBoundingBox(true); var bb = ob.Geometry.GetBoundingBox(true);
            double Gap(double a0, double a1, double b0, double b1) => Math.Max(0, Math.Max(b0 - a1, a0 - b1));
            var gap = new Vector3d(Gap(ba.Min.X, ba.Max.X, bb.Min.X, bb.Max.X), Gap(ba.Min.Y, ba.Max.Y, bb.Min.Y, bb.Max.Y), 0).Length;
            return JsonSerializer.Serialize(new { method = "bbox_gap_xy", clear_width = gap, clear_width_m = gap / upm });
        }
        if (args["from"] is not null && args["to"] is not null)
        {
            var d = ParsePoint(args["from"], "from").DistanceTo(ParsePoint(args["to"], "to"));
            return JsonSerializer.Serialize(new { method = "points", clear_width = d, clear_width_m = d / upm });
        }
        throw new ArgumentException("Provide id (door/window), a+b (objects), or from+to (points).");
    });
}
