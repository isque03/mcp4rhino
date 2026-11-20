using System.Text.Json.Nodes;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using MCP4Rhino.Logic.Grasshopper;
using Rhino;
using Rhino.DocObjects;

namespace MCP4Rhino.Grasshopper;

internal static class ResultReader
{
    public static object Inspect(DefinitionSession session, JsonObject args)
    {
        session.RequireContext();
        var options = GhInspectOptions.Parse(args);
        var objects = session.Document.Objects.ToArray();
        var (groups, groupsOf) = Groups(objects);
        var infos = objects.Select(o => Info(o, groupsOf, options.Mode == GhInspectMode.Overview)).ToArray();
        var summary = GhInspect.ToNode(session.Summary());
        if (options.Mode == GhInspectMode.Overview && !options.Filtered)
            return GhInspect.Overview(summary, infos, groups);
        var matches = GhInspect.Filter(infos, groups, options);
        if (options.Mode == GhInspectMode.Overview)
            return GhInspect.Overview(summary, matches, groups);
        var byId = objects.ToDictionary(o => o.InstanceGuid.ToString(), StringComparer.OrdinalIgnoreCase);
        return GhInspect.Page(summary, matches, infos.Length, options, info => GhInspect.ToNode(DescribeNode(byId[info.Id])));
    }

    private static (GhGroupInfo[] Groups, Dictionary<Guid, List<string>> Of) Groups(IGH_DocumentObject[] objects)
    {
        var of = new Dictionary<Guid, List<string>>();
        var groups = objects.OfType<GH_Group>().Select(g =>
        {
            var name = string.IsNullOrWhiteSpace(g.NickName) ? "Group" : g.NickName;
            foreach (var id in g.ObjectIDs)
                (of.TryGetValue(id, out var list) ? list : of[id] = new List<string>()).Add(name);
            return new GhGroupInfo(g.InstanceGuid.ToString(), name, g.ObjectIDs.Select(i => i.ToString()).ToArray());
        }).ToArray();
        return (groups, of);
    }

    private static GhNodeInfo Info(IGH_DocumentObject node, Dictionary<Guid, List<string>> groupsOf, bool control)
    {
        var active = node as IGH_ActiveObject;
        return new GhNodeInfo(node.InstanceGuid.ToString(), node.Name, node.NickName, node.GetType().Name,
            node.Attributes.Pivot.X, node.Attributes.Pivot.Y,
            groupsOf.TryGetValue(node.InstanceGuid, out var g) ? g.ToArray() : [],
            active?.RuntimeMessages(GH_RuntimeMessageLevel.Error).ToArray() ?? [],
            active?.RuntimeMessages(GH_RuntimeMessageLevel.Warning).ToArray() ?? [],
            ComponentCatalog.Parameters(node, true).Count(), ComponentCatalog.Parameters(node, false).Count(),
            control ? Control(node) : null);
    }

    /// <summary>Compact description of a likely user input; null for ordinary components.</summary>
    private static JsonObject? Control(IGH_DocumentObject node)
    {
        switch (node)
        {
            case GH_NumberSlider slider:
                return new JsonObject { ["kind"] = "slider", ["value"] = slider.CurrentValue,
                    ["minimum"] = (double)slider.Slider.Minimum, ["maximum"] = (double)slider.Slider.Maximum };
            case GH_Panel panel:
                return new JsonObject { ["kind"] = "panel", ["text"] = GhInspect.Cut(panel.UserText ?? "", 100) };
            case GH_BooleanToggle toggle:
                return new JsonObject { ["kind"] = "toggle", ["value"] = toggle.Value };
            case GH_ValueList list:
                return new JsonObject { ["kind"] = "value_list", ["selected"] = GhInspect.Cut(list.FirstSelectedItem?.Name ?? "", 100),
                    ["items"] = list.ListItems.Count };
            case IGH_Param param when param.Sources.Count == 0 && node is not IGH_Component:
                var data = param.GetType().GetProperty("PersistentData")?.GetValue(param) as IGH_Structure;
                if (data is null || data.DataCount == 0) return null;
                return new JsonObject { ["kind"] = "param", ["type"] = param.TypeName, ["items"] = data.DataCount,
                    ["branches"] = data.PathCount, ["sample"] = GhInspect.ToNode(ValueCodec.Rows(data, measurements: false).Take(3).ToArray()) };
            default: return null;
        }
    }

    internal static object DescribeNode(IGH_DocumentObject node, bool resource = false) => new
    {
        node = node.InstanceGuid, component_id = node.ComponentGuid, name = node.Name, nickname = node.NickName,
        supported = ComponentCatalog.Supported(node), position = new[] { node.Attributes.Pivot.X, node.Attributes.Pivot.Y },
        support_level = ComponentCatalog.Permission(node).SupportLevel, authorized = ComponentCatalog.Permission(node).Authorized,
        restriction_reason = ComponentCatalog.Permission(node).Reason, assembly_identity = node.GetType().Assembly.FullName,
        enabled = node is not IGH_ActiveObject active || !active.Locked,
        preview = node is IGH_PreviewObject preview && !preview.Hidden, settings = ComponentCatalog.Settings(node),
        inputs = ComponentCatalog.Ports(node, true, resource), outputs = ComponentCatalog.Ports(node, false, resource),
        diagnostics = Diagnostics(node),
    };

    public static object Diagnostics(IGH_DocumentObject node) => node is IGH_ActiveObject active ? new
    {
        node = node.InstanceGuid, errors = active.RuntimeMessages(GH_RuntimeMessageLevel.Error).ToArray(),
        warnings = active.RuntimeMessages(GH_RuntimeMessageLevel.Warning).ToArray(),
        milliseconds = active.ProcessorTime.TotalMilliseconds,
    } : (object)new { node = node.InstanceGuid };

    public static object SolveSummary(DefinitionSession session) => new
    {
        gh_session = session.Id, session_id = session.Id, revision = session.Revision, result_id = session.ResultId,
        milliseconds = session.Document.SolutionSpan.TotalMilliseconds,
        diagnostics = session.Document.Objects.Select(Diagnostics).ToArray(),
        outputs = session.Document.Objects.SelectMany(n => ComponentCatalog.Parameters(n, false)
            .Select((p, i) => new { node = n.InstanceGuid, port = "out:" + i, type = p.TypeName,
                branches = p.VolatileData.PathCount, items = p.VolatileDataCount })).ToArray(),
    };

    public static object Read(DefinitionSession session, JsonObject args)
    {
        var rhino = session.RequireContext(); session.Identity.Require(args, true);
        var port = GraphEditor.Endpoint(session.Document, args, null, false);
        var offset = GhJson.Offset(args);
        var limit = GhJson.PageSize(args);
        var page = ValueCodec.Rows(port.VolatileData, rhino.ModelAbsoluteTolerance, offset: offset).Take(limit + 1).ToArray();
        return new { gh_session = session.Id, session_id = session.Id, revision = session.Revision, result_id = session.ResultId,
            units = Units(rhino), branch_count = port.VolatileData.PathCount,
            item_count = port.VolatileDataCount, rows = page.Take(limit).ToArray(),
            next_offset = page.Length > limit ? offset + limit : (int?)null };
    }

    public static object Summary(DefinitionSession session, JsonObject args)
    {
        var rhino = session.RequireContext(); session.Identity.Require(args, true);
        var outputs = NamedOutput.Parse(args).Select(output =>
        {
            var node = GraphEditor.Node(session.Document, output.Node);
            var port = ComponentCatalog.Port(node, output.Port, false);
            return new { name = output.Name, node = output.Node, port = output.Port,
                branch_count = port.VolatileData.PathCount, item_count = port.VolatileDataCount,
                data = SolutionReport.Page(ValueCodec.Rows(port.VolatileData, rhino.ModelAbsoluteTolerance), args) };
        }).ToArray();
        var controls = session.Document.Objects.Select(node => (object)new
        {
            node = node.InstanceGuid, name = node.NickName, settings = ComponentCatalog.Settings(node),
            inputs = ComponentCatalog.Ports(node, true), diagnostics = Diagnostics(node),
        });
        return new { session = session.Summary(), identity = session.Identity, units = Units(rhino),
            graph = SolutionReport.Page(controls, args), outputs };
    }

    private static object Units(RhinoDoc rhino) => new
    {
        model = rhino.ModelUnitSystem.ToString(), absolute_tolerance = rhino.ModelAbsoluteTolerance,
        length = "model units", area = "model units squared", volume = "model units cubed",
        numeric_outputs = "Unitless unless defined by the graph; no dimensional meaning is inferred.",
    };

    public static object Bake(DefinitionSession session, JsonObject args)
    {
        var rhino = session.RequireContext();
        GhJson.ExpectRevision(session.Revision, args); session.RequireResult(GhJson.Text(args, "result_id"));
        ComponentCatalog.RequireAuthorized(session.Document);
        var outputs = GhJson.Array(args["outputs"]);
        if (outputs.Count is < 1 or > 100) throw new GhException("limit_exceeded", "Select 1–100 output ports.");
        var items = outputs.SelectMany(p => BakeItems(session.Document, GhJson.Object(p))).Take(10001).ToArray();
        if (items.Length is 0 or > 10000) throw new GhException("limit_exceeded", "Bake requires 1–10000 valid geometry items.");
        var layer = args["layer"]?.GetValue<string>();
        var index = layer is null ? rhino.Layers.CurrentLayerIndex : rhino.Layers.FindByFullPath(layer, -1);
        if (index < 0) throw new GhException("layer_not_found", "Create the target layer with create_layer before baking.");
        return BakeItems(session, rhino, items, args, index);
    }

    private sealed record BakeItem(IGH_BakeAwareData Data, string Node, string Port, string Path, int Index);

    private static IEnumerable<BakeItem> BakeItems(GH_Document document, JsonObject endpoint)
    {
        var parameter = GraphEditor.Endpoint(document, endpoint, null, false);
        foreach (var path in parameter.VolatileData.Paths)
        {
            var branch = parameter.VolatileData.get_Branch(path);
            for (var i = 0; i < branch.Count; i++)
            {
                if (branch[i] is null) continue;
                if (branch[i] is not IGH_BakeAwareData data || branch[i] is not IGH_Goo { IsValid: true })
                    throw new GhException("not_bakeable", $"Output {endpoint} contains invalid or non-bakeable data.", "debugging");
                yield return new BakeItem(data, GhJson.Text(endpoint, "node"), GhJson.Text(endpoint, "port"), path.ToString(), i);
            }
        }
    }

    private static object BakeItems(DefinitionSession session, RhinoDoc rhino, BakeItem[] items, JsonObject args, int layer)
    {
        var ids = new List<Guid>();
        var record = rhino.BeginUndoRecord("MCP Grasshopper bake snapshot");
        if (record == 0) throw new GhException("busy", "Cannot start a Rhino undo record right now.");
        try
        {
            foreach (var item in items)
            {
                var attributes = new ObjectAttributes { LayerIndex = layer, Name = args["name"]?.GetValue<string>() ?? "Grasshopper snapshot" };
                attributes.SetUserString("mcp4:gh_session", session.Id);
                attributes.SetUserString("mcp4:gh_result", session.ResultId);
                attributes.SetUserString("mcp4:gh_source", $"{item.Node}/{item.Port}/{item.Path}/{item.Index}");
                if (!item.Data.BakeGeometry(rhino, attributes, out var id) || id == Guid.Empty)
                    throw new GhException("bake_failed", "Geometry could not be baked.", "debugging");
                ids.Add(id);
            }
            rhino.Views.Redraw();
            return new { gh_session = session.Id, session_id = session.Id, result_id = session.ResultId, object_ids = ids };
        }
        catch { foreach (var id in ids) rhino.Objects.Delete(id, true); throw; }
        finally { rhino.EndUndoRecord(record); }
    }
}
