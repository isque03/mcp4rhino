using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Drawing.Imaging;
using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Host;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace MCP4Rhino.Tools;

/// <summary>
/// MCP tool definitions + dispatch (no external MCP SDK / Roslyn).
/// </summary>
[ExcludeFromCodeCoverage]
public static class RhinoToolCatalog
{
    public static object[] ListTools() =>
        CurveFoundationTools.ListTools()
            .Concat(GeometryFoundationTools.ListTools())
            .Concat(ArchElementTools.ListTools())
            .Concat(DocSheetTools.ListTools())
            .Concat(InteropTools.ListTools())
            .Concat(CodeCheckTools.ListTools())
            .Concat(LegacyTools())
            .ToArray();

    private static object[] LegacyTools() =>
    [
        Tool("get_document_info", "Get units, layers, groups, object count, and selected object IDs.", new
        {
            type = "object",
            properties = new { },
        }),
        Tool("get_objects", "List/filter objects by layer, name, type, group, or user-text tags.", new
        {
            type = "object",
            properties = new
            {
                layer = new { type = "string", description = "Layer full-path substring filter" },
                name = new { type = "string", description = "Object name substring filter" },
                type = new { type = "string", description = "brep|mesh|curve|point|surface|extrude" },
                group = new { type = "string", description = "Group name (exact, case-insensitive)" },
                tag_key = new { type = "string", description = "Require this Attribute User Text key" },
                tag_value = new { type = "string", description = "Require tag_key equals this value (substring OK)" },
                include_tags = new { type = "boolean", description = "Include user-text tags in results (default true)" },
            },
        }),
        Tool("set_user_text", "Set Attribute User Text tags (key/value) on objects.", new
        {
            type = "object",
            properties = new
            {
                ids = new { type = "string", description = "Comma-separated or JSON array of GUIDs" },
                key = new { type = "string", description = "Tag key (if setting one key)" },
                value = new { type = "string", description = "Tag value (if setting one key)" },
                tags = new { type = "object", description = "Map of key→value tags to set" },
            },
            required = new[] { "ids" },
        }),
        Tool("get_user_text", "Get Attribute User Text tags for objects.", new
        {
            type = "object",
            properties = new
            {
                ids = new { type = "string", description = "Comma-separated or JSON array of GUIDs" },
                key = new { type = "string", description = "Optional single key to fetch" },
            },
            required = new[] { "ids" },
        }),
        Tool("set_object_name", "Set the Rhino object Name attribute.", new
        {
            type = "object",
            properties = new
            {
                ids = new { type = "string" },
                name = new { type = "string" },
            },
            required = new[] { "ids", "name" },
        }),
        Tool("set_object_layer", "Move objects to a layer (created if missing).", new
        {
            type = "object",
            properties = new
            {
                ids = new { type = "string" },
                layer = new { type = "string", description = "Layer full path, e.g. Parthenon::Columns" },
            },
            required = new[] { "ids", "layer" },
        }),
        Tool("list_groups", "List document groups and member counts.", new
        {
            type = "object",
            properties = new { },
        }),
        Tool("create_group", "Create a named group containing the given objects (replaces membership if group exists).", new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string" },
                ids = new { type = "string", description = "GUIDs to put in the group" },
            },
            required = new[] { "name", "ids" },
        }),
        Tool("add_to_group", "Add objects to an existing group (creates group if missing).", new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string" },
                ids = new { type = "string" },
            },
            required = new[] { "name", "ids" },
        }),
        Tool("create_box", "Create an axis-aligned box. Optional name/layer/tags/group.", CreateGeomSchema()),
        Tool("create_sphere", "Create a sphere. Optional name/layer/tags/group.", new
        {
            type = "object",
            properties = new
            {
                centerX = new { type = "number" }, centerY = new { type = "number" }, centerZ = new { type = "number" },
                radius = new { type = "number" },
                name = new { type = "string" }, layer = new { type = "string" },
                group = new { type = "string" },
                tags = new { type = "object", description = "Attribute User Text key/values" },
            },
        }),
        Tool("create_cylinder", "Create a cylinder along +Z. Optional name/layer/tags/group.", new
        {
            type = "object",
            properties = new
            {
                baseX = new { type = "number" }, baseY = new { type = "number" }, baseZ = new { type = "number" },
                radius = new { type = "number" }, height = new { type = "number" },
                name = new { type = "string" }, layer = new { type = "string" },
                group = new { type = "string" },
                tags = new { type = "object", description = "Attribute User Text key/values" },
            },
        }),
        Tool("delete_objects", "Delete objects by GUID list (comma-separated or JSON array).", new
        {
            type = "object",
            properties = new { ids = new { type = "string", description = "GUIDs" } },
            required = new[] { "ids" },
        }),
        Tool("list_views", "List Rhino viewports with camera summaries.", new
        {
            type = "object",
            properties = new { },
        }),
        Tool("get_view", "Get camera state for a view (default: active).", new
        {
            type = "object",
            properties = new
            {
                view = new { type = "string", description = "Viewport name, e.g. Perspective (default: active)" },
            },
        }),
        Tool("set_active_view", "Activate a viewport by name (Perspective, Top, Front, Right, …).", new
        {
            type = "object",
            properties = new
            {
                view = new { type = "string" },
            },
            required = new[] { "view" },
        }),
        Tool("set_view", "Set absolute camera location/target (optional up, perspective flag).", new
        {
            type = "object",
            properties = new
            {
                view = new { type = "string" },
                camera = new { description = "[x,y,z] or {x,y,z}" },
                target = new { description = "[x,y,z] or {x,y,z}" },
                up = new { description = "Optional up vector [x,y,z]" },
                perspective = new { type = "boolean", description = "true=perspective, false=parallel" },
            },
            required = new[] { "camera", "target" },
        }),
        Tool("orbit_view", "Orbit camera around target. yaw/pitch in degrees (yaw about world Z, pitch about camera right).", new
        {
            type = "object",
            properties = new
            {
                view = new { type = "string", description = "Default active; pass Perspective to force it" },
                yaw = new { type = "number", description = "Degrees, positive = CCW about world Z" },
                pitch = new { type = "number", description = "Degrees, positive = look up; clamped near ±89" },
            },
        }),
        Tool("pan_view", "Pan camera+target in view plane (model units along camera right/up).", new
        {
            type = "object",
            properties = new
            {
                view = new { type = "string" },
                right = new { type = "number", description = "Distance along camera right" },
                up = new { type = "number", description = "Distance along camera up" },
            },
        }),
        Tool("zoom_view", "Dolly zoom. factor>1 zooms in toward target; factor<1 zooms out.", new
        {
            type = "object",
            properties = new
            {
                view = new { type = "string" },
                factor = new { type = "number", description = "Zoom factor (e.g. 1.2)" },
            },
            required = new[] { "factor" },
        }),
        Tool("zoom_extents", "Zoom view to fit all objects, or filter by ids/layer.", new
        {
            type = "object",
            properties = new
            {
                view = new { type = "string" },
                ids = new { type = "string", description = "Optional GUID list to fit" },
                layer = new { type = "string", description = "Optional layer substring filter" },
            },
        }),
        Tool("capture_viewport", "Capture a viewport to a PNG file and return its path.", new
        {
            type = "object",
            properties = new
            {
                view = new { type = "string", description = "Viewport name (default: active)" },
                width = new { type = "integer", description = "Image width (default 1024)" },
                height = new { type = "integer", description = "Image height (default 768)" },
                path = new { type = "string", description = "Optional output PNG path" },
            },
        }),
    ];

    private static object CreateGeomSchema() => new
    {
        type = "object",
        properties = new
        {
            minX = new { type = "number" }, minY = new { type = "number" }, minZ = new { type = "number" },
            maxX = new { type = "number" }, maxY = new { type = "number" }, maxZ = new { type = "number" },
            name = new { type = "string" }, layer = new { type = "string" },
            group = new { type = "string" },
            tags = new { type = "object", description = "Attribute User Text key/values" },
        },
    };

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
            "set_user_text" => SetUserText(args),
            "get_user_text" => GetUserText(args),
            "set_object_name" => SetObjectName(args),
            "set_object_layer" => SetObjectLayer(args),
            "list_groups" => ListGroups(),
            "create_group" => CreateGroup(args),
            "add_to_group" => AddToGroup(args),
            "create_box" => CreateBox(args),
            "create_sphere" => CreateSphere(args),
            "create_cylinder" => CreateCylinder(args),
            "delete_objects" => DeleteObjects(args),
            "list_views" => ViewTools.ListViews(),
            "get_view" => ViewTools.GetView(args),
            "set_active_view" => ViewTools.SetActiveView(args),
            "set_view" => ViewTools.SetView(args),
            "orbit_view" => ViewTools.OrbitView(args),
            "pan_view" => ViewTools.PanView(args),
            "zoom_view" => ViewTools.ZoomView(args),
            "zoom_extents" => ViewTools.ZoomExtents(args),
            "capture_viewport" => CaptureViewport(args),
            _ => CurveFoundationTools.TryCall(name, args)
                ?? GeometryFoundationTools.TryCall(name, args)
                ?? ArchElementTools.Dispatch(name, args)
                ?? DocSheetTools.Dispatch(name, args)
                ?? InteropTools.Dispatch(name, args)
                ?? CodeCheckTools.Dispatch(name, args)
                ?? throw new InvalidOperationException($"Unknown tool: {name}"),
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
        var doc = ToolHelpers.RequireDoc();
        var layers = doc.Layers.Select(l => l.FullPath).ToArray();
        var selected = doc.Objects.GetSelectedObjects(false, false).Select(o => o.Id.ToString()).ToArray();
        var groups = Enumerable.Range(0, doc.Groups.Count)
            .Select(i => doc.Groups.GroupName(i))
            .Where(n => !string.IsNullOrEmpty(n))
            .ToArray();
        return JsonSerializer.Serialize(new
        {
            name = doc.Name,
            path = doc.Path,
            units = doc.ModelUnitSystem.ToString(),
            object_count = doc.Objects.Count,
            layer_count = doc.Layers.Count,
            layers,
            groups,
            selected_ids = selected,
        });
    });

    private static string GetObjects(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var layer = args["layer"]?.GetValue<string>();
        var name = args["name"]?.GetValue<string>();
        var type = args["type"]?.GetValue<string>();
        var group = args["group"]?.GetValue<string>();
        var tagKey = args["tag_key"]?.GetValue<string>();
        var tagValue = args["tag_value"]?.GetValue<string>();
        var includeTags = args["include_tags"]?.GetValue<bool>() ?? true;

        var items = new List<object>();
        foreach (var obj in doc.Objects)
        {
            if (obj is null || obj.IsDeleted) continue;
            var geomType = ToolHelpers.Classify(obj.Geometry);
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

            var groupNames = ToolHelpers.GetGroupNames(doc, obj);
            if (!string.IsNullOrWhiteSpace(group) &&
                !groupNames.Any(g => string.Equals(g, group, StringComparison.OrdinalIgnoreCase)))
                continue;

            var tags = ToolHelpers.ReadTags(obj);
            if (!string.IsNullOrWhiteSpace(tagKey))
            {
                if (!tags.TryGetValue(tagKey, out var tv))
                    continue;
                if (!string.IsNullOrWhiteSpace(tagValue) &&
                    tv.IndexOf(tagValue, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
            }

            var bbox = obj.Geometry?.GetBoundingBox(true) ?? BoundingBox.Empty;
            items.Add(new
            {
                id = obj.Id.ToString(),
                name = objName,
                type = geomType,
                layer = layerName,
                groups = groupNames,
                tags = includeTags ? tags : null,
                bbox = bbox.IsValid
                    ? new { min = new[] { bbox.Min.X, bbox.Min.Y, bbox.Min.Z }, max = new[] { bbox.Max.X, bbox.Max.Y, bbox.Max.Z } }
                    : null,
            });
        }

        return JsonSerializer.Serialize(new { count = items.Count, objects = items });
    });

    private static string SetUserText(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var ids = ToolHelpers.ParseIds(args["ids"]?.GetValue<string>() ?? throw new ArgumentException("ids required"));
        var tags = ToolHelpers.ParseTags(args);
        if (tags.Count == 0)
            throw new ArgumentException("Provide key+value and/or tags object.");

        var updated = new List<string>();
        var missing = new List<string>();
        foreach (var id in ids)
        {
            var obj = doc.Objects.FindId(id);
            if (obj is null) { missing.Add(id.ToString()); continue; }
            foreach (var (k, v) in tags)
                obj.Attributes.SetUserString(k, v);
            obj.CommitChanges();
            updated.Add(id.ToString());
        }
        return JsonSerializer.Serialize(new { updated, missing, tags });
    });

    private static string GetUserText(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var ids = ToolHelpers.ParseIds(args["ids"]?.GetValue<string>() ?? throw new ArgumentException("ids required"));
        var key = args["key"]?.GetValue<string>();
        var items = new List<object>();
        var missing = new List<string>();
        foreach (var id in ids)
        {
            var obj = doc.Objects.FindId(id);
            if (obj is null) { missing.Add(id.ToString()); continue; }
            var tags = ToolHelpers.ReadTags(obj);
            items.Add(new
            {
                id = id.ToString(),
                tags = string.IsNullOrWhiteSpace(key)
                    ? tags
                    : tags.TryGetValue(key, out var v)
                        ? new Dictionary<string, string> { [key] = v }
                        : new Dictionary<string, string>(),
            });
        }
        return JsonSerializer.Serialize(new { objects = items, missing });
    });

    private static string SetObjectName(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var ids = ToolHelpers.ParseIds(args["ids"]?.GetValue<string>() ?? throw new ArgumentException("ids required"));
        var name = args["name"]?.GetValue<string>() ?? throw new ArgumentException("name required");
        var updated = new List<string>();
        var missing = new List<string>();
        foreach (var id in ids)
        {
            var obj = doc.Objects.FindId(id);
            if (obj is null) { missing.Add(id.ToString()); continue; }
            obj.Attributes.Name = name;
            obj.CommitChanges();
            updated.Add(id.ToString());
        }
        return JsonSerializer.Serialize(new { updated, missing, name });
    });

    private static string SetObjectLayer(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var ids = ToolHelpers.ParseIds(args["ids"]?.GetValue<string>() ?? throw new ArgumentException("ids required"));
        var layer = args["layer"]?.GetValue<string>() ?? throw new ArgumentException("layer required");
        var layerIndex = ToolHelpers.EnsureLayer(doc, layer);
        var updated = new List<string>();
        var missing = new List<string>();
        foreach (var id in ids)
        {
            var obj = doc.Objects.FindId(id);
            if (obj is null) { missing.Add(id.ToString()); continue; }
            obj.Attributes.LayerIndex = layerIndex;
            obj.CommitChanges();
            updated.Add(id.ToString());
        }
        return JsonSerializer.Serialize(new { updated, missing, layer });
    });

    private static string ListGroups() => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var groups = new List<object>();
        for (var i = 0; i < doc.Groups.Count; i++)
        {
            var gname = doc.Groups.GroupName(i);
            if (string.IsNullOrEmpty(gname)) continue;
            var members = doc.Groups.GroupMembers(i) ?? [];
            groups.Add(new { name = gname, index = i, count = members.Length });
        }
        return JsonSerializer.Serialize(new { count = groups.Count, groups });
    });

    private static string CreateGroup(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var gname = args["name"]?.GetValue<string>() ?? throw new ArgumentException("name required");
        var ids = ToolHelpers.ParseIds(args["ids"]?.GetValue<string>() ?? throw new ArgumentException("ids required"));

        // Remove existing group with same name so create is idempotent.
        var existing = doc.Groups.Find(gname);
        if (existing >= 0)
            doc.Groups.Delete(existing);

        var idx = doc.Groups.Add(gname, ids);
        return JsonSerializer.Serialize(new
        {
            name = gname,
            index = idx,
            member_count = ids.Count,
            ids = ids.Select(i => i.ToString()).ToArray(),
        });
    });

    private static string AddToGroup(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var gname = args["name"]?.GetValue<string>() ?? throw new ArgumentException("name required");
        var ids = ToolHelpers.ParseIds(args["ids"]?.GetValue<string>() ?? throw new ArgumentException("ids required"));
        var idx = doc.Groups.Find(gname);
        if (idx < 0)
            idx = doc.Groups.Add(gname);

        var added = new List<string>();
        foreach (var id in ids)
        {
            if (doc.Groups.AddToGroup(idx, id))
                added.Add(id.ToString());
        }
        return JsonSerializer.Serialize(new { name = gname, index = idx, added });
    });

    private static string CreateBox(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var minX = ToolHelpers.Num(args, "minX"); var minY = ToolHelpers.Num(args, "minY"); var minZ = ToolHelpers.Num(args, "minZ");
        var maxX = ToolHelpers.Num(args, "maxX", 1); var maxY = ToolHelpers.Num(args, "maxY", 1); var maxZ = ToolHelpers.Num(args, "maxZ", 1);
        var box = new Box(new BoundingBox(new Point3d(minX, minY, minZ), new Point3d(maxX, maxY, maxZ)));
        if (!box.IsValid) throw new ArgumentException("Invalid box dimensions.");
        var id = doc.Objects.AddBox(box, ToolHelpers.BuildAttributes(doc, args));
        ToolHelpers.ApplyGroup(doc, id, args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "brep" });
    });

    private static string CreateSphere(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var radius = ToolHelpers.Num(args, "radius", 1);
        if (radius <= 0) throw new ArgumentException("Radius must be positive.");
        var sphere = new Sphere(new Point3d(ToolHelpers.Num(args, "centerX"), ToolHelpers.Num(args, "centerY"), ToolHelpers.Num(args, "centerZ")), radius);
        var id = doc.Objects.AddSphere(sphere, ToolHelpers.BuildAttributes(doc, args));
        ToolHelpers.ApplyGroup(doc, id, args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "brep" });
    });

    private static string CreateCylinder(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var radius = ToolHelpers.Num(args, "radius", 1);
        var height = ToolHelpers.Num(args, "height", 1);
        if (radius <= 0 || Math.Abs(height) < 1e-12)
            throw new ArgumentException("Radius must be positive and height non-zero.");
        var plane = new Plane(new Point3d(ToolHelpers.Num(args, "baseX"), ToolHelpers.Num(args, "baseY"), ToolHelpers.Num(args, "baseZ")), Vector3d.ZAxis);
        var cylinder = new Cylinder(new Circle(plane, radius), height);
        var brep = cylinder.ToBrep(true, true) ?? throw new InvalidOperationException("Failed to create cylinder.");
        var id = doc.Objects.AddBrep(brep, ToolHelpers.BuildAttributes(doc, args));
        ToolHelpers.ApplyGroup(doc, id, args);
        doc.Views.Redraw();
        return JsonSerializer.Serialize(new { id = id.ToString(), type = "brep" });
    });

    private static string DeleteObjects(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var ids = args["ids"]?.GetValue<string>() ?? throw new ArgumentException("ids required");
        var parsed = ToolHelpers.ParseIds(ids);
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
        var doc = ToolHelpers.RequireDoc();
        var viewName = args["view"]?.GetValue<string>();
        RhinoView? view = null;
        if (string.IsNullOrWhiteSpace(viewName))
        {
            view = doc.Views.ActiveView;
        }
        else
        {
            foreach (var v in doc.Views)
            {
                if (v?.ActiveViewport is not null &&
                    string.Equals(v.ActiveViewport.Name, viewName, StringComparison.OrdinalIgnoreCase))
                {
                    view = v;
                    break;
                }
            }
        }
        if (view is null)
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(viewName)
                ? "No active view."
                : $"View not found: {viewName}");

        var width = (int)ToolHelpers.Num(args, "width", 1024);
        var height = (int)ToolHelpers.Num(args, "height", 768);
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

        // ViewCapture.CaptureToBitmap(RhinoView) is missing/broken on many Mac Rhino builds.
        // Prefer RhinoView.CaptureToBitmap*; fall back to -_ViewCaptureToFile.
        var (actualWidth, actualHeight, method) = CaptureViewportToFile(view, path, width, height);
        PluginLog.Info($"capture_viewport wrote {path} ({actualWidth}x{actualHeight}) via {method}");
        return JsonSerializer.Serialize(new
        {
            path,
            width = actualWidth,
            height = actualHeight,
            requested_width = width,
            requested_height = height,
            method,
            view = view.ActiveViewport?.Name,
        });
    });

    private static (int width, int height, string method) CaptureViewportToFile(
        RhinoView view, string path, int width, int height)
    {
        view.Redraw();

        // macOS: System.Drawing/GDI+ CaptureToBitmap usually fails (Gdip type initializer).
        // Prefer the scripted command which writes a real PNG without GDI+.
        if (TryViewCaptureToFile(view, path))
        {
            var (aw, ah) = TryReadPngSize(path) ?? (width, height);
            return (aw, ah, "ViewCaptureToFile");
        }

        if (!OperatingSystem.IsMacOS())
        {
            foreach (var attempt in new Func<System.Drawing.Bitmap?>[]
            {
                () => view.CaptureToBitmap(new System.Drawing.Size(width, height), false, false, false),
                () => view.CaptureToBitmap(new System.Drawing.Size(width, height)),
                () => view.CaptureToBitmap(false, false, false),
                () => view.CaptureToBitmap(),
            })
            {
                try
                {
                    using var bitmap = attempt();
                    if (bitmap is null) continue;
                    bitmap.Save(path, ImageFormat.Png);
                    return (bitmap.Width, bitmap.Height, "CaptureToBitmap");
                }
                catch (MissingMethodException) { /* try next overload */ }
                catch (TypeLoadException) { /* System.Drawing mismatch */ }
                catch (TypeInitializationException) { /* Gdip init */ }
            }
        }

        throw new InvalidOperationException(
            "Viewport capture failed: ViewCaptureToFile wrote no file"
            + (OperatingSystem.IsMacOS() ? " (CaptureToBitmap unavailable on macOS)." : "."));
    }

    private static bool TryViewCaptureToFile(RhinoView view, string path)
    {
        if (File.Exists(path))
            File.Delete(path);

        var prev = view.Document?.Views.ActiveView;
        try
        {
            if (view.Document is not null)
                view.Document.Views.ActiveView = view;
            view.Redraw();

            // Keep the script minimal — Mac is picky about ViewCaptureToFile option tokens.
            var ok = RhinoApp.RunScript($"_-ViewCaptureToFile \"{path}\" _Enter", false);
            if (File.Exists(path) && new FileInfo(path).Length > 32)
                return true;

            // Some builds append .png when the path has no extension handling quirks.
            var alt = path.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? path : path + ".png";
            if (File.Exists(alt) && new FileInfo(alt).Length > 32)
            {
                if (!string.Equals(alt, path, StringComparison.Ordinal))
                    File.Move(alt, path, overwrite: true);
                return true;
            }

            PluginLog.Info($"ViewCaptureToFile ok={ok} path={path} exists={File.Exists(path)}");
            return false;
        }
        finally
        {
            if (prev is not null && view.Document is not null)
                view.Document.Views.ActiveView = prev;
        }
    }

    private static (int width, int height)? TryReadPngSize(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            Span<byte> hdr = stackalloc byte[24];
            if (fs.Read(hdr) < 24) return null;
            // PNG signature + IHDR length/type + width/height (big-endian)
            if (hdr[0] != 0x89 || hdr[1] != 0x50 || hdr[2] != 0x4E || hdr[3] != 0x47) return null;
            int w = (hdr[16] << 24) | (hdr[17] << 16) | (hdr[18] << 8) | hdr[19];
            int h = (hdr[20] << 24) | (hdr[21] << 16) | (hdr[22] << 8) | hdr[23];
            if (w <= 0 || h <= 0) return null;
            return (w, h);
        }
        catch
        {
            return null;
        }
    }
}
