using System.Text.Json.Nodes;
using Grasshopper.Kernel;
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
        var nodes = session.Document.Objects;
        return new { session = session.Summary(), total = nodes.Count, offset = GhJson.Offset(args),
            nodes = nodes.Skip(GhJson.Offset(args)).Take(GhJson.PageSize(args)).Select(node => DescribeNode(node)).ToArray() };
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
        session_id = session.Id, revision = session.Revision, result_id = session.ResultId,
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
        return new { session_id = session.Id, revision = session.Revision, result_id = session.ResultId,
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
            return new { session_id = session.Id, result_id = session.ResultId, object_ids = ids };
        }
        catch { foreach (var id in ids) rhino.Objects.Delete(id, true); throw; }
        finally { rhino.EndUndoRecord(record); }
    }
}
