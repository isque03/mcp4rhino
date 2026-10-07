using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Host;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace MCP4Rhino.Tools;

/// <summary>
/// MCP tool definitions + dispatch (no external MCP SDK / Roslyn).
/// </summary>
public static class RhinoToolCatalog
{
    public static object[] ListTools() =>
    [
        Tool("get_document_info", "Get units, layers, object count, and selected object IDs.", new
        {
            type = "object",
            properties = new { },
        }),
        Tool("get_objects", "List objects. Optional filters: layer, name, type.", new
        {
            type = "object",
            properties = new
            {
                layer = new { type = "string", description = "Layer substring filter" },
                name = new { type = "string", description = "Name substring filter" },
                type = new { type = "string", description = "brep|mesh|curve|point|surface|extrude" },
            },
        }),
        Tool("create_box", "Create an axis-aligned box.", new
        {
            type = "object",
            properties = new
            {
                minX = new { type = "number" }, minY = new { type = "number" }, minZ = new { type = "number" },
                maxX = new { type = "number" }, maxY = new { type = "number" }, maxZ = new { type = "number" },
                name = new { type = "string" }, layer = new { type = "string" },
            },
        }),
        Tool("create_sphere", "Create a sphere.", new
        {
            type = "object",
            properties = new
            {
                centerX = new { type = "number" }, centerY = new { type = "number" }, centerZ = new { type = "number" },
                radius = new { type = "number" },
                name = new { type = "string" }, layer = new { type = "string" },
            },
        }),
        Tool("create_cylinder", "Create a cylinder along +Z.", new
        {
            type = "object",
            properties = new
            {
                baseX = new { type = "number" }, baseY = new { type = "number" }, baseZ = new { type = "number" },
                radius = new { type = "number" }, height = new { type = "number" },
                name = new { type = "string" }, layer = new { type = "string" },
            },
        }),
        Tool("delete_objects", "Delete objects by GUID list (comma-separated or JSON array).", new
        {
            type = "object",
            properties = new { ids = new { type = "string", description = "GUIDs" } },
            required = new[] { "ids" },
        }),
        Tool("capture_viewport", "Capture the active Rhino viewport to a PNG file and return its path.", new
        {
            type = "object",
            properties = new
            {
                width = new { type = "integer", description = "Image width (default 1024)" },
                height = new { type = "integer", description = "Image height (default 768)" },
                path = new { type = "string", description = "Optional output PNG path" },
            },
        }),
    ];

    public static object CallTool(JsonNode? @params)
    {
        var name = @params?["name"]?.GetValue<string>()
            ?? throw new ArgumentException("tools/call missing name");
        var args = @params?["arguments"] as JsonObject ?? new JsonObject();

        PluginLog.Info($"tools/call {name}");
        var text = name switch
        {
            "get_document_info" => GetDocumentInfo(),
            "get_objects" => GetObjects(args),
            "create_box" => CreateBox(args),
            "create_sphere" => CreateSphere(args),
            "create_cylinder" => CreateCylinder(args),
            "delete_objects" => DeleteObjects(args),
            "capture_viewport" => CaptureViewport(args),
            _ => throw new InvalidOperationException($"Unknown tool: {name}"),
        };

        return new
        {
            content = new[] { new { type = "text", text } },
            isError = false,
        };
    }

    private static object Tool(string name, string description, object inputSchema) => new
    {
        name,
        description,
        inputSchema,
    };

    private static string GetDocumentInfo() => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var layers = doc.Layers.Select(l => l.FullPath).ToArray();
        var selected = doc.Objects.GetSelectedObjects(false, false).Select(o => o.Id.ToString()).ToArray();
        return JsonSerializer.Serialize(new
        {
            name = doc.Name,
            path = doc.Path,
            units = doc.ModelUnitSystem.ToString(),
            object_count = doc.Objects.Count,
            layer_count = doc.Layers.Count,
            layers,
            selected_ids = selected,
        });
    });

    private static string GetObjects(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var layer = args["layer"]?.GetValue<string>();
        var name = args["name"]?.GetValue<string>();
        var type = args["type"]?.GetValue<string>();

        var items = new List<object>();
        foreach (var obj in doc.Objects)
        {
            if (obj is null || obj.IsDeleted) continue;
            var geomType = Classify(obj.Geometry);
            if (!string.IsNullOrWhiteSpace(type) &&
                !string.Equals(geomType, type, StringComparison.OrdinalIgnoreCase))
                continue;

            var layerName = doc.Layers[obj.Attributes.LayerIndex]?.FullPath ?? "";
            if (!string.IsNullOrWhiteSpace(layer) &&
                layerName.IndexOf(layer, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var objName = obj.Name ?? "";
            if (!string.IsNullOrWhiteSpace(name) &&
                objName.IndexOf(name, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            var bbox = obj.Geometry?.GetBoundingBox(true) ?? BoundingBox.Empty;
            items.Add(new
            {
                id = obj.Id.ToString(),
                name = objName,
                type = geomType,
                layer = layerName,
                bbox = bbox.IsValid
                    ? new { min = new[] { bbox.Min.X, bbox.Min.Y, bbox.Min.Z }, max = new[] { bbox.Max.X, bbox.Max.Y, bbox.Max.Z } }
                    : null,
            });
        }

        return JsonSerializer.Serialize(new { count = items.Count, objects = items });
    });

    private static string CreateBox(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var minX = Num(args, "minX"); var minY = Num(args, "minY"); var minZ = Num(args, "minZ");
        var maxX = Num(args, "maxX", 1); var maxY = Num(args, "maxY", 1); var maxZ = Num(args, "maxZ", 1);
        var box = new Box(new BoundingBox(new Point3d(minX, minY, minZ), new Point3d(maxX, maxY, maxZ)));
        if (!box.IsValid) throw new ArgumentException("Invalid box dimensions.");
        var id = doc.Objects.AddBox(box, BuildAttributes(doc, args));
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "brep" });
    });

    private static string CreateSphere(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var radius = Num(args, "radius", 1);
        if (radius <= 0) throw new ArgumentException("Radius must be positive.");
        var sphere = new Sphere(new Point3d(Num(args, "centerX"), Num(args, "centerY"), Num(args, "centerZ")), radius);
        var id = doc.Objects.AddSphere(sphere, BuildAttributes(doc, args));
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "brep" });
    });

    private static string CreateCylinder(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var radius = Num(args, "radius", 1);
        var height = Num(args, "height", 1);
        if (radius <= 0 || Math.Abs(height) < 1e-12)
            throw new ArgumentException("Radius must be positive and height non-zero.");
        var plane = new Plane(new Point3d(Num(args, "baseX"), Num(args, "baseY"), Num(args, "baseZ")), Vector3d.ZAxis);
        var cylinder = new Cylinder(new Circle(plane, radius), height);
        var brep = cylinder.ToBrep(true, true) ?? throw new InvalidOperationException("Failed to create cylinder.");
        var id = doc.Objects.AddBrep(brep, BuildAttributes(doc, args));
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "brep" });
    });

    private static string DeleteObjects(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var ids = args["ids"]?.GetValue<string>() ?? throw new ArgumentException("ids required");
        var parsed = ParseIds(ids);
        var deleted = new List<string>();
        var missing = new List<string>();
        foreach (var id in parsed)
        {
            if (doc.Objects.Delete(id, true)) deleted.Add(id.ToString());
            else missing.Add(id.ToString());
        }
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { deleted, missing });
    });

    private static string CaptureViewport(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var view = doc.Views.ActiveView
            ?? throw new InvalidOperationException("No active view.");

        var width = (int)Num(args, "width", 1024);
        var height = (int)Num(args, "height", 768);
        if (width < 64 || height < 64 || width > 4096 || height > 4096)
            throw new ArgumentException("width/height must be between 64 and 4096.");

        var path = args["path"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(path))
        {
            var dir = Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                "Library", "Logs", "MCP4Rhino");
            Directory.CreateDirectory(dir);
            path = Path.Combine(dir, $"viewport-{DateTime.Now:yyyyMMdd-HHmmss}.png");
        }
        else
        {
            var parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);
        }

        using var bitmap = view.CaptureToBitmap(new Size(width, height));
        if (bitmap is null)
            throw new InvalidOperationException("CaptureToBitmap returned null.");

        bitmap.Save(path, ImageFormat.Png);
        PluginLog.Info($"capture_viewport wrote {path} ({width}x{height})");
        return JsonSerializer.Serialize(new
        {
            path,
            width,
            height,
            view = view.ActiveViewport?.Name,
        });
    });

    private static RhinoDoc RequireDoc() =>
        RhinoDoc.ActiveDoc ?? throw new InvalidOperationException("No active Rhino document.");

    private static ObjectAttributes BuildAttributes(RhinoDoc doc, JsonObject args)
    {
        var attrs = new ObjectAttributes();
        var name = args["name"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(name)) attrs.Name = name;
        var layer = args["layer"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(layer))
        {
            var idx = doc.Layers.FindByFullPath(layer, -1);
            if (idx < 0) idx = doc.Layers.Add(new Layer { Name = layer });
            if (idx >= 0) attrs.LayerIndex = idx;
        }
        return attrs;
    }

    private static double Num(JsonObject args, string key, double fallback = 0)
    {
        var n = args[key];
        if (n is null) return fallback;
        return n.GetValue<double>();
    }

    private static List<Guid> ParseIds(string ids)
    {
        var trimmed = ids.Trim();
        IEnumerable<string> parts = trimmed.StartsWith('[')
            ? JsonSerializer.Deserialize<string[]>(trimmed) ?? []
            : trimmed.Split([',', ';', '\n', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var result = new List<Guid>();
        foreach (var p in parts)
        {
            if (!Guid.TryParse(p, out var g)) throw new ArgumentException($"Invalid GUID: {p}");
            result.Add(g);
        }
        return result;
    }

    private static string Classify(GeometryBase? geometry) => geometry switch
    {
        null => "unknown",
        Brep => "brep",
        Extrusion => "extrude",
        Mesh => "mesh",
        Curve => "curve",
        Rhino.Geometry.Point => "point",
        Surface => "surface",
        _ => geometry.GetType().Name.ToLowerInvariant(),
    };
}
