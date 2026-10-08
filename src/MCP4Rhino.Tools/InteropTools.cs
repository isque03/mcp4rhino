using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using MCP4Rhino.Host;
using MCP4Rhino.Logic;
using MCP4Rhino.Logic.Models;
using Rhino;
using Rhino.FileIO;
using Rhino.Geometry;

namespace MCP4Rhino.Tools;

[ExcludeFromCodeCoverage]
internal static class InteropTools
{
    public static object[] ListTools() =>
    [
        T("check_rhino_license",
            "Report whether Rhino allows saving (valid license or active evaluation). " +
            "Call before export_3dm; export_3dm fails when can_save is false.",
            new { type = "object", properties = new { } }),
        T("export_ifc", "Export mcp4-tagged building model to a minimal IFC2x3 file.", new
        {
            type = "object",
            properties = new { path = new { type = "string" } },
        }),
        T("import_ifc", "Limited IFC import: records path and note (full parse not in v1).", new
        {
            type = "object",
            properties = new { path = new { type = "string" } },
            required = new[] { "path" },
        }),
        T("export_obj", "Export model (or ids) to OBJ.", new
        {
            type = "object",
            properties = new { path = new { type = "string" }, ids = new { type = "string" } },
            required = new[] { "path" },
        }),
        T("export_3dm",
            "Save the document to 3DM. Requires a valid Rhino license or non-expired evaluation " +
            "(RhinoApp.CanSave). Call check_rhino_license first; fails with a clear error when saving is not allowed.",
            new
            {
                type = "object",
                properties = new { path = new { type = "string" } },
                required = new[] { "path" },
            }),
        T("link_external_model", "Import a file onto layer LINK::<name>.", new
        {
            type = "object",
            properties = new { path = new { type = "string" }, name = new { type = "string" } },
            required = new[] { "path" },
        }),
        T("list_links", "List LINK::* layers.", new { type = "object", properties = new { } }),
        T("clash_detect", "Axis-aligned bbox clash between two object sets.", new
        {
            type = "object",
            properties = new
            {
                ids_a = new { type = "string" }, ids_b = new { type = "string" },
                clearance = new { type = "number", description = "Soft clash expansion" },
            },
            required = new[] { "ids_a", "ids_b" },
        }),
        T("quantity_takeoff", "Counts/areas/lengths by mcp4:kind and level.", new
        {
            type = "object",
            properties = new { level = new { type = "string" }, kind = new { type = "string" } },
        }),
    ];

    private static object T(string n, string d, object s) => new { name = n, description = d, inputSchema = s };

    public static string? Dispatch(string name, JsonObject args) => name switch
    {
        "check_rhino_license" => CheckRhinoLicense(),
        "export_ifc" => ExportIfc(args),
        "import_ifc" => ImportIfc(args),
        "export_obj" => ExportObj(args),
        "export_3dm" => Export3dm(args),
        "link_external_model" => LinkExternal(args),
        "list_links" => ListLinks(),
        "clash_detect" => ClashDetect(args),
        "quantity_takeoff" => QuantityTakeoffTool(args),
        _ => null,
    };

    private static string CheckRhinoLicense() =>
        ToolHelpers.Json(RhinoLicenseStatus.Query().ToJsonObject());

    private static string ExportIfc(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var path = ToolHelpers.Str(args, "path")
                   ?? Path.Combine(ToolHelpers.LogsDir(), $"model-{DateTime.Now:yyyyMMdd-HHmmss}.ifc");
        var levels = ToolHelpers.GetDocJson<List<LevelInfo>>(doc, ToolHelpers.LevelsKey) ?? [];
        var elements = ElementMapper.TaggedInDoc(doc);
        var useMetres = doc.ModelUnitSystem.ToString().Contains("Meter", StringComparison.OrdinalIgnoreCase);
        var text = MinimalIfcExporter.Export(levels, elements, Path.GetFileName(path), useMetres);
        File.WriteAllText(path, text);
        return ToolHelpers.Json(new { path, note = "Minimal IFC2X3 with bounding-box geometry for tagged elements. For full breps use a dedicated IFC exporter later." });
    });

    private static string ImportIfc(JsonObject args) => UiThread.Invoke(() =>
    {
        var path = ToolHelpers.Str(args, "path")!;
        if (!File.Exists(path)) throw new FileNotFoundException(path);
        var text = File.ReadAllText(path);
        var storeys = IfcImportMetadata.FindStoreyGuids(text);
        return ToolHelpers.Json(new
        {
            path,
            storey_guids_found = storeys.Length,
            note = "v1 import is metadata-only. Use export_ifc for MCP4Rhino round-trip of tagged models; full IFC geometry import is not implemented.",
        });
    });

    private static string ExportObj(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var path = ToolHelpers.Str(args, "path")!;
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        if (ToolHelpers.Str(args, "ids") is { } ids)
        {
            doc.Objects.UnselectAll();
            foreach (var id in ToolHelpers.ParseIds(ids))
                doc.Objects.FindId(id)?.Select(true);
            var okSel = RhinoApp.RunScript($"_-Export \"{path}\" _Enter", false);
            return ToolHelpers.Json(new { ok = okSel, path });
        }
        var ok = doc.Export(path);
        return ToolHelpers.Json(new { ok, path });
    });

    private static string Export3dm(JsonObject args) => UiThread.Invoke(() =>
    {
        var license = RhinoLicenseStatus.Query();
        if (!license.CanSave)
            throw new InvalidOperationException(license.Message);

        var doc = ToolHelpers.RequireDoc();
        var path = ToolHelpers.Str(args, "path")!;
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        var ok = doc.WriteFile(path, new FileWriteOptions { SuppressDialogBoxes = true, WriteSelectedObjectsOnly = false });
        return ToolHelpers.Json(new { ok, path });
    });

    private static string LinkExternal(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var path = ToolHelpers.Str(args, "path")!;
        var name = ToolHelpers.Str(args, "name") ?? Path.GetFileNameWithoutExtension(path);
        if (!File.Exists(path)) throw new FileNotFoundException(path);
        var layer = $"LINK::{name}";
        var idx = ToolHelpers.EnsureLayer(doc, layer);
        var ok = RhinoApp.RunScript($"_-Import \"{path}\" _Enter", false);
        doc.Layers[idx].IsLocked = true;
        doc.Layers[idx].CommitChanges();
        return ToolHelpers.Json(new { ok, layer, path, note = "Imported via -_Import; layer LINK::* created and locked. Move objects to the link layer if needed." });
    });

    private static string ListLinks() => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var links = doc.Layers
            .Where(l => l.FullPath.StartsWith("LINK::", StringComparison.OrdinalIgnoreCase))
            .Select(l => new { path = l.FullPath, locked = l.IsLocked, visible = l.IsVisible })
            .ToArray();
        return ToolHelpers.Json(new { count = links.Length, links });
    });

    private static string ClashDetect(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var a = ToolHelpers.IdList(args["ids_a"], "ids_a");
        var b = ToolHelpers.IdList(args["ids_b"], "ids_b");
        var clearance = ToolHelpers.Num(args, "clearance");

        static (string Id, BBox Box)? BoxOf(Rhino.RhinoDoc doc, Guid id)
        {
            var o = doc.Objects.FindId(id);
            if (o?.Geometry is null) return null;
            var bb = o.Geometry.GetBoundingBox(true);
            if (!bb.IsValid) return null;
            return (id.ToString(), new BBox(bb.Min.X, bb.Min.Y, bb.Min.Z, bb.Max.X, bb.Max.Y, bb.Max.Z));
        }

        var setA = a.Select(id => BoxOf(doc, id)).Where(x => x is not null).Select(x => x!.Value).ToList();
        var setB = b.Select(id => BoxOf(doc, id)).Where(x => x is not null).Select(x => x!.Value).ToList();
        var clashes = BboxClash.Detect(setA, setB, clearance);
        return ToolHelpers.Json(new
        {
            count = clashes.Count,
            clashes = clashes.Select(c => new { a = c.A, b = c.B, type = c.Type }).ToArray(),
        });
    });

    private static string QuantityTakeoffTool(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = ToolHelpers.RequireDoc();
        var rows = QuantityTakeoff.Compute(
            ElementMapper.TaggedInDoc(doc),
            ToolHelpers.Str(args, "level"),
            ToolHelpers.Str(args, "kind"));
        return ToolHelpers.Json(new
        {
            rows = rows.Select(r => new { level = r.Level, kind = r.Kind, count = r.Count, area = r.Area, length = r.Length }).ToArray(),
        });
    });
}
