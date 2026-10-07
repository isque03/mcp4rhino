using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Host;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace MCP4Rhino.Tools;

internal static class DocSheetTools
{
    private const string SheetsKey = "MCP4RHINO_SHEETS_JSON";

    public static object[] ListTools() =>
    [
        T("create_section", "Create a section cutting plane + named view camera looking at cut.", new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string" },
                start = new { }, end = new { },
                depth = new { type = "number" },
            },
            required = new[] { "name", "start", "end" },
        }),
        T("create_elevation", "Create an elevation view direction from a baseline.", new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string" },
                start = new { }, end = new { },
                height = new { type = "number" },
            },
            required = new[] { "name", "start", "end" },
        }),
        T("create_detail_view", "Register a detail view (camera + target) by name.", new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string" },
                camera = new { }, target = new { },
            },
            required = new[] { "name", "camera", "target" },
        }),
        T("create_dimension", "Create a linear dimension between two points.", new
        {
            type = "object",
            properties = new
            {
                start = new { }, end = new { }, offset = new { type = "number" },
                plane_z = new { type = "number" }, layer = new { type = "string" },
            },
            required = new[] { "start", "end" },
        }),
        T("create_text", "Create model-space text.", new
        {
            type = "object",
            properties = new
            {
                text = new { type = "string" }, at = new { }, height = new { type = "number" },
                layer = new { type = "string" },
            },
            required = new[] { "text", "at" },
        }),
        T("create_leader", "Create a leader with text.", new
        {
            type = "object",
            properties = new
            {
                points = new { }, text = new { type = "string" }, height = new { type = "number" },
            },
            required = new[] { "points", "text" },
        }),
        T("create_hatch", "Hatch a closed planar curve.", new
        {
            type = "object",
            properties = new { id = new { type = "string" }, pattern = new { type = "string" }, scale = new { type = "number" } },
            required = new[] { "id" },
        }),
        T("create_room_tag", "Place a room tag at a space or point.", TagSchema()),
        T("create_door_tag", "Place a door tag near an object.", TagSchema()),
        T("create_window_tag", "Place a window tag near an object.", TagSchema()),
        T("create_schedule", "Build a schedule JSON from element tags (door|window|room|finish).", new
        {
            type = "object",
            properties = new { kind = new { type = "string" }, path = new { type = "string" } },
            required = new[] { "kind" },
        }),
        T("create_sheet", "Register a sheet (title, number, size) in document registry.", new
        {
            type = "object",
            properties = new
            {
                number = new { type = "string" }, title = new { type = "string" },
                size = new { type = "string", description = "e.g. 36x24" },
            },
            required = new[] { "number", "title" },
        }),
        T("list_sheets", "List registered sheets.", new { type = "object", properties = new { } }),
        T("place_view_on_sheet", "Attach a named view/capture to a sheet registry entry.", new
        {
            type = "object",
            properties = new
            {
                sheet_number = new { type = "string" }, view_name = new { type = "string" },
                scale = new { type = "string" },
            },
            required = new[] { "sheet_number", "view_name" },
        }),
        T("export_pdf", "Export active view or sheet views to PDF via print/script best-effort.", new
        {
            type = "object",
            properties = new { path = new { type = "string" }, view = new { type = "string" } },
        }),
        T("export_dwg", "Export selected or all model objects to DWG.", new
        {
            type = "object",
            properties = new { path = new { type = "string" }, ids = new { type = "string" } },
            required = new[] { "path" },
        }),
        T("export_images", "Capture listed views to PNGs.", new
        {
            type = "object",
            properties = new { views = new { type = "string", description = "comma-separated view names" }, dir = new { type = "string" } },
        }),
        T("apply_layer_standard", "Create NCS/AIA-like architectural layers.", new { type = "object", properties = new { } }),
    ];

    private static object TagSchema() => new
    {
        type = "object",
        properties = new
        {
            id = new { type = "string", description = "Source object id" },
            at = new { description = "[x,y,z] override" },
            text = new { type = "string" }, height = new { type = "number" },
        },
    };

    private static object T(string n, string d, object s) => new { name = n, description = d, inputSchema = s };

    public static string? Dispatch(string name, JsonObject args) => name switch
    {
        "create_section" => CreateSection(args),
        "create_elevation" => CreateElevation(args),
        "create_detail_view" => CreateDetailView(args),
        "create_dimension" => CreateDimension(args),
        "create_text" => CreateText(args),
        "create_leader" => CreateLeader(args),
        "create_hatch" => CreateHatch(args),
        "create_room_tag" => CreateTag(args, "room"),
        "create_door_tag" => CreateTag(args, "door"),
        "create_window_tag" => CreateTag(args, "window"),
        "create_schedule" => CreateSchedule(args),
        "create_sheet" => CreateSheet(args),
        "list_sheets" => ListSheets(),
        "place_view_on_sheet" => PlaceViewOnSheet(args),
        "export_pdf" => ExportPdf(args),
        "export_dwg" => ExportDwg(args),
        "export_images" => ExportImages(args),
        "apply_layer_standard" => ApplyLayerStandard(),
        _ => null,
    };

    private static string CreateSection(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var name = ToolHelpers.Str(args, "name")!;
        var a = ToolHelpers.ParsePoint(args["start"], "start");
        var b = ToolHelpers.ParsePoint(args["end"], "end");
        var depth = ToolHelpers.Num(args, "depth", 50);
        var dir = b - a; dir.Unitize();
        var look = Vector3d.CrossProduct(dir, Vector3d.ZAxis); look.Unitize();
        var mid = (a + b) * 0.5;
        var attrs = new ObjectAttributes { Name = name, LayerIndex = ToolHelpers.EnsureLayer(doc, "A-ANNO-SECT") };
        attrs.SetUserString(ToolHelpers.KindKey, "section");
        attrs.SetUserString("mcp4:depth", depth.ToString("G"));
        var id = doc.Objects.AddLine(a, b, attrs);
        // Named view
        var camera = mid - look * depth;
        var vp = doc.Views.ActiveView?.ActiveViewport;
        if (vp is not null)
        {
            vp.SetCameraLocations(mid, camera);
            doc.NamedViews.Add(name, vp.Id);
        }
        doc.Views.Redraw();
        return ToolHelpers.Json(new { id = id.ToString(), name, kind = "section" });
    });

    private static string CreateElevation(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var name = ToolHelpers.Str(args, "name")!;
        var a = ToolHelpers.ParsePoint(args["start"], "start");
        var b = ToolHelpers.ParsePoint(args["end"], "end");
        var height = ToolHelpers.Num(args, "height", 20);
        var dir = b - a; dir.Z = 0; dir.Unitize();
        var look = Vector3d.CrossProduct(Vector3d.ZAxis, dir); look.Unitize();
        var mid = (a + b) * 0.5 + new Vector3d(0, 0, height / 2);
        var attrs = new ObjectAttributes { Name = name, LayerIndex = ToolHelpers.EnsureLayer(doc, "A-ANNO-ELEV") };
        attrs.SetUserString(ToolHelpers.KindKey, "elevation");
        var id = doc.Objects.AddLine(a, b, attrs);
        var camera = mid - look * 40;
        var vp = doc.Views.ActiveView?.ActiveViewport;
        if (vp is not null)
        {
            vp.SetCameraLocations(mid, camera);
            doc.NamedViews.Add(name, vp.Id);
        }
        doc.Views.Redraw();
        return ToolHelpers.Json(new { id = id.ToString(), name, kind = "elevation" });
    });

    private static string CreateDetailView(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var name = ToolHelpers.Str(args, "name")!;
        var camera = ToolHelpers.ParsePoint(args["camera"], "camera");
        var target = ToolHelpers.ParsePoint(args["target"], "target");
        var vp = doc.Views.ActiveView?.ActiveViewport
            ?? throw new InvalidOperationException("No active view");
        vp.SetCameraLocations(target, camera);
        doc.NamedViews.Add(name, vp.Id);
        return ToolHelpers.Json(new { name, kind = "detail_view" });
    });

    private static string CreateDimension(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var a = ToolHelpers.ParsePoint(args["start"], "start");
        var b = ToolHelpers.ParsePoint(args["end"], "end");
        var offset = ToolHelpers.Num(args, "offset", 2);
        var z = ToolHelpers.Num(args, "plane_z", a.Z);
        a.Z = z; b.Z = z;
        var plane = Plane.WorldXY;
        plane.Origin = new Point3d(0, 0, z);
        var dir = b - a;
        var perp = Vector3d.CrossProduct(Vector3d.ZAxis, dir);
        if (!perp.Unitize()) perp = Vector3d.YAxis;
        var p = a + perp * offset;
        var dim = LinearDimension.FromPoints(a, b, p);
        if (dim is null) throw new InvalidOperationException("Dimension failed");
        var attrs = new ObjectAttributes { LayerIndex = ToolHelpers.EnsureLayer(doc, ToolHelpers.Str(args, "layer") ?? "A-ANNO-DIMS") };
        attrs.SetUserString(ToolHelpers.KindKey, "dimension");
        var id = doc.Objects.AddLinearDimension(dim, attrs);
        doc.Views.Redraw();
        return ToolHelpers.Json(new { id = id.ToString(), distance = a.DistanceTo(b) });
    });

    private static string CreateText(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var text = ToolHelpers.Str(args, "text")!;
        var at = ToolHelpers.ParsePoint(args["at"], "at");
        var height = ToolHelpers.Num(args, "height", 1);
        var entity = new TextEntity
        {
            Plane = new Plane(at, Vector3d.ZAxis),
            PlainText = text,
            TextHeight = height,
        };
        var attrs = new ObjectAttributes { LayerIndex = ToolHelpers.EnsureLayer(doc, ToolHelpers.Str(args, "layer") ?? "A-ANNO-TEXT") };
        attrs.SetUserString(ToolHelpers.KindKey, "text");
        var id = doc.Objects.AddText(entity, attrs);
        doc.Views.Redraw();
        return ToolHelpers.Json(new { id = id.ToString(), text });
    });

    private static string CreateLeader(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var pts = ToolHelpers.ParsePoints(args["points"]);
        var text = ToolHelpers.Str(args, "text")!;
        var height = ToolHelpers.Num(args, "height", 1);
        // Leader as polyline + text at end
        var attrs = new ObjectAttributes { LayerIndex = ToolHelpers.EnsureLayer(doc, "A-ANNO-TEXT") };
        attrs.SetUserString(ToolHelpers.KindKey, "leader");
        var lineId = doc.Objects.AddPolyline(new Polyline(pts), attrs);
        var te = new TextEntity
        {
            Plane = new Plane(pts[^1], Vector3d.ZAxis),
            PlainText = text,
            TextHeight = height,
        };
        var textId = doc.Objects.AddText(te, attrs);
        doc.Views.Redraw();
        return ToolHelpers.Json(new { line_id = lineId.ToString(), text_id = textId.ToString(), text });
    });

    private static string CreateHatch(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var id = Guid.Parse(ToolHelpers.Str(args, "id")!);
        var obj = doc.Objects.FindId(id) ?? throw new InvalidOperationException("Curve not found");
        if (obj.Geometry is not Curve c || !c.IsClosed) throw new ArgumentException("Need closed curve");
        var scale = ToolHelpers.Num(args, "scale", 1);
        var hatches = Hatch.Create(c, 0, 0, scale, doc.ModelAbsoluteTolerance);
        if (hatches is null || hatches.Length == 0) throw new InvalidOperationException("Hatch failed");
        var attrs = new ObjectAttributes { LayerIndex = ToolHelpers.EnsureLayer(doc, "A-ANNO-HATCH") };
        var ids = hatches.Select(h => doc.Objects.AddHatch(h, attrs).ToString()).ToArray();
        doc.Views.Redraw();
        return ToolHelpers.Json(new { ids });
    });

    private static string CreateTag(JsonObject args, string kind) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        Point3d at;
        string label;
        if (ToolHelpers.Str(args, "id") is { } sid)
        {
            var obj = doc.Objects.FindId(Guid.Parse(sid)) ?? throw new InvalidOperationException("Object not found");
            at = obj.Geometry!.GetBoundingBox(true).Center;
            label = ToolHelpers.Str(args, "text") ?? obj.Name ?? kind.ToUpperInvariant();
        }
        else
        {
            at = ToolHelpers.ParsePoint(args["at"], "at");
            label = ToolHelpers.Str(args, "text") ?? kind.ToUpperInvariant();
        }
        var height = ToolHelpers.Num(args, "height", 1);
        var te = new TextEntity { Plane = new Plane(at, Vector3d.ZAxis), PlainText = label, TextHeight = height };
        var attrs = new ObjectAttributes { LayerIndex = ToolHelpers.EnsureLayer(doc, "A-ANNO-TEXT") };
        attrs.SetUserString(ToolHelpers.KindKey, $"{kind}_tag");
        var id = doc.Objects.AddText(te, attrs);
        doc.Views.Redraw();
        return ToolHelpers.Json(new { id = id.ToString(), text = label, kind = $"{kind}_tag" });
    });

    private static string CreateSchedule(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var kind = (ToolHelpers.Str(args, "kind") ?? "door").ToLowerInvariant();
        var mapKind = kind switch
        {
            "door" => "door",
            "window" => "window",
            "room" or "space" => "space",
            "finish" => "space",
            _ => kind,
        };
        var rows = new List<Dictionary<string, string>>();
        foreach (var obj in doc.Objects)
        {
            if (obj is null || obj.IsDeleted) continue;
            var tags = ToolHelpers.ReadTags(obj);
            if (!tags.TryGetValue(ToolHelpers.KindKey, out var k) || !string.Equals(k, mapKind, StringComparison.OrdinalIgnoreCase))
                continue;
            var row = new Dictionary<string, string>(tags) { ["id"] = obj.Id.ToString(), ["name"] = obj.Name ?? "" };
            rows.Add(row);
        }
        var path = ToolHelpers.Str(args, "path")
                   ?? Path.Combine(ToolHelpers.LogsDir(), $"schedule-{kind}-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(new { kind, count = rows.Count, rows }, new JsonSerializerOptions { WriteIndented = true }));
        return ToolHelpers.Json(new { path, count = rows.Count, rows });
    });

    private record SheetRec(string number, string title, string size, List<Dictionary<string, string>> views);

    private static string CreateSheet(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var sheets = ToolHelpers.GetDocJson<List<SheetRec>>(doc, SheetsKey) ?? new();
        var number = ToolHelpers.Str(args, "number")!;
        sheets.RemoveAll(s => s.number == number);
        sheets.Add(new SheetRec(number, ToolHelpers.Str(args, "title")!, ToolHelpers.Str(args, "size") ?? "36x24", new()));
        ToolHelpers.SetDocJson(doc, SheetsKey, sheets);
        return ToolHelpers.Json(new { number, title = ToolHelpers.Str(args, "title"), sheets });
    });

    private static string ListSheets() => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var sheets = ToolHelpers.GetDocJson<List<SheetRec>>(doc, SheetsKey) ?? new();
        return ToolHelpers.Json(new { count = sheets.Count, sheet_list = sheets });
    });

    private static string PlaceViewOnSheet(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var sheets = ToolHelpers.GetDocJson<List<SheetRec>>(doc, SheetsKey) ?? new();
        var number = ToolHelpers.Str(args, "sheet_number")!;
        var idx = sheets.FindIndex(s => s.number == number);
        if (idx < 0) throw new InvalidOperationException($"Sheet not found: {number}");
        var s = sheets[idx];
        s.views.Add(new Dictionary<string, string>
        {
            ["view_name"] = ToolHelpers.Str(args, "view_name")!,
            ["scale"] = ToolHelpers.Str(args, "scale") ?? "1/4\"=1'-0\"",
        });
        sheets[idx] = s;
        ToolHelpers.SetDocJson(doc, SheetsKey, sheets);
        return ToolHelpers.Json(new { sheet = s });
    });

    private static string ExportPdf(JsonObject args) => UiThread.Invoke(() =>
    {
        var path = ToolHelpers.Str(args, "path")
                   ?? Path.Combine(ToolHelpers.LogsDir(), $"sheet-{DateTime.Now:yyyyMMdd-HHmmss}.pdf");
        // Best-effort: capture PNG and note PDF via print script
        var png = Path.ChangeExtension(path, ".png");
        var ok = RhinoApp.RunScript($"_-ViewCaptureToFile \"{png}\" _Enter", false);
        if (ok && File.Exists(png))
        {
            // Many Mac builds lack headless PDF print; return PNG companion + instruction
            return ToolHelpers.Json(new
            {
                ok = true,
                path_png = png,
                path_pdf = path,
                note = "PNG captured. For PDF use Rhino Print to PDF interactively, or install a PDF printer pipeline.",
            });
        }
        throw new InvalidOperationException("export_pdf capture failed");
    });

    private static string ExportDwg(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var path = ToolHelpers.Str(args, "path")!;
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        if (ToolHelpers.Str(args, "ids") is { } ids)
        {
            doc.Objects.UnselectAll();
            foreach (var id in ToolHelpers.ParseIds(ids))
                doc.Objects.FindId(id)?.Select(true);
        }
        var ok = RhinoApp.RunScript($"_-Export \"{path}\" _Enter", false);
        return ToolHelpers.Json(new { ok, path });
    });

    private static string ExportImages(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var dir = ToolHelpers.Str(args, "dir") ?? ToolHelpers.LogsDir();
        Directory.CreateDirectory(dir);
        var views = (ToolHelpers.Str(args, "views") ?? "Perspective")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var paths = new List<string>();
        foreach (var vname in views)
        {
            foreach (var v in doc.Views)
            {
                if (v?.ActiveViewport is null) continue;
                if (!string.Equals(v.ActiveViewport.Name, vname, StringComparison.OrdinalIgnoreCase)) continue;
                doc.Views.ActiveView = v;
                var path = Path.Combine(dir, $"view-{vname}-{DateTime.Now:HHmmss}.png");
                RhinoApp.RunScript($"_-ViewCaptureToFile \"{path}\" _Enter", false);
                if (File.Exists(path)) paths.Add(path);
            }
        }
        return ToolHelpers.Json(new { paths });
    });

    private static string ApplyLayerStandard() => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var layers = new (string path, string color)[]
        {
            ("A-WALL", "#C0C0C0"), ("A-DOOR", "#8B4513"), ("A-WINDOW", "#00BFFF"),
            ("A-FLOR", "#D2B48C"), ("A-ROOF", "#696969"), ("A-COLS", "#FF4500"),
            ("A-BEAM", "#FF6347"), ("A-GRID", "#808080"), ("A-AREA", "#90EE90"),
            ("A-SITE", "#228B22"), ("A-ANNO-DIMS", "#000000"), ("A-ANNO-TEXT", "#000000"),
            ("A-ANNO-SECT", "#FF00FF"), ("A-ANNO-ELEV", "#800080"), ("A-ANNO-HATCH", "#A9A9A9"),
        };
        var created = new List<string>();
        foreach (var (path, color) in layers)
        {
            var idx = ToolHelpers.EnsureLayer(doc, path);
            var c = ToolHelpers.TryParseColor(color);
            if (c is not null)
            {
                doc.Layers[idx].Color = c.Value;
                doc.Layers[idx].CommitChanges();
            }
            created.Add(path);
        }
        return ToolHelpers.Json(new { layers = created });
    });
}
