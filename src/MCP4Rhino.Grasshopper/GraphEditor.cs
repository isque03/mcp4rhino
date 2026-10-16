using System.Drawing;
using System.Text.Json.Nodes;
using Grasshopper.GUI.Base;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using MCP4Rhino.Logic.Grasshopper;
using Rhino;

namespace MCP4Rhino.Grasshopper;

internal static class GraphEditor
{
    public static object Patch(DefinitionSession session, JsonObject args)
    {
        var rhino = session.RequireContext();
        GhJson.ExpectRevision(session.Revision, args);
        ComponentCatalog.RequireSupported(session.Document);
        var operations = GhJson.Array(args["operations"]);
        if (operations.Count is < 1 or > 100) throw new GhException("limit_exceeded", "Use 1–100 operations per patch.");
        var before = DefinitionArchive.Capture(session.Document);
        using (var staging = DefinitionArchive.Clone(before)) Apply(staging, operations, rhino);
        return Commit(session, operations, rhino, before);
    }

    private static object Commit(DefinitionSession session, JsonArray operations, RhinoDoc rhino, byte[] before)
    {
        var document = session.Document;
        var enabled = document.Enabled;
        session.Muting = true;
        try
        {
            document.Enabled = false;
            var aliases = Apply(document, operations, rhino);
            // Low-level edits do not record undo; this snapshot owns the entire patch.
            var after = DefinitionArchive.Capture(document);
            document.UndoServer.PushUndoRecord("MCP Grasshopper patch", new DefinitionUndo(session, before, after));
            document.Modified();
            return new { aliases };
        }
        catch
        {
            try { DefinitionArchive.Restore(document, before); }
            catch { session.Faulted = true; }
            throw;
        }
        finally
        {
            document.Enabled = enabled; session.Rebind(); session.Muting = false; session.Changed();
            global::Grasshopper.Instances.InvalidateCanvas();
        }
    }

    private static Dictionary<string, string> Apply(GH_Document doc, JsonArray operations, RhinoDoc rhino)
    {
        var aliases = new Dictionary<string, string>();
        for (var index = 0; index < operations.Count; index++)
        {
            try { ApplyOne(doc, GhJson.Object(operations[index]), aliases, rhino); }
            catch (Exception ex)
            {
                throw new GhException((ex as GhException)?.Code ?? "invalid_operation",
                    $"Operation {index}: {ex.Message}", (ex as GhException)?.HelpTopic ?? "workflow");
            }
        }
        GraphRules.RequireAcyclic(doc.Objects.Select(n => n.InstanceGuid.ToString()), Edges(doc));
        ArrangeAddedNodes(doc, operations, aliases);
        return aliases;
    }

    private static void ArrangeAddedNodes(GH_Document doc, JsonArray operations, Dictionary<string, string> aliases)
    {
        var layout = GraphRules.Layout(doc.Objects.Select(n => n.InstanceGuid.ToString()), Edges(doc));
        var existing = doc.Objects.Where(n => !aliases.Values.Contains(n.InstanceGuid.ToString())).ToArray();
        var offset = existing.Length == 0 ? 0 : existing.Max(n => n.Attributes.Bounds.Right) + 120;
        foreach (var op in operations.OfType<JsonObject>().Where(o => o["op"]!.GetValue<string>() == "add" && o["position"] is null))
        {
            var node = Node(doc, GhJson.Text(op, "alias"), aliases);
            var position = layout[node.InstanceGuid.ToString()];
            node.Attributes.Pivot = new PointF(position.X + offset, position.Y);
        }
    }

    private static void ApplyOne(GH_Document doc, JsonObject op, Dictionary<string, string> aliases, RhinoDoc rhino)
    {
        var kind = GhJson.Text(op, "op");
        if (kind == "add") { Add(doc, op, aliases); return; }
        if (kind is "connect" or "disconnect") { Connect(doc, op, aliases, kind == "connect"); return; }
        var node = Node(doc, GhJson.Text(op, "node"), aliases);
        switch (kind)
        {
            case "remove": doc.RemoveObject(node, false); break;
            case "set_value": ValueCodec.Assign(ComponentCatalog.Port(node, GhJson.Text(op, "port"), true), op["tree"], rhino); break;
            case "set_slider": Slider(node, op); break;
            case "set_properties": Properties(node, op); break;
            case "set_mapping": Mapping(node, op); break;
            default: throw new GhException("invalid_operation", $"Unknown operation {kind}.");
        }
        if (node is IGH_ActiveObject active) active.ExpireSolution(false);
    }

    public static IGH_DocumentObject Node(GH_Document doc, string id, IReadOnlyDictionary<string, string>? aliases = null)
    {
        if (aliases?.TryGetValue(id, out var resolved) == true) id = resolved;
        return Guid.TryParse(id, out var guid) ? doc.FindObject(guid, false) ?? throw new GhException("node_not_found", $"No node {id}.") :
            throw new GhException("node_not_found", $"Unknown node or alias {id}.");
    }

    private static void Add(GH_Document doc, JsonObject op, Dictionary<string, string> aliases)
    {
        if (doc.Objects.Count >= 2000) throw new GhException("limit_exceeded", "Definitions are limited to 2000 objects in v1.");
        var alias = GhJson.Text(op, "alias");
        if (Guid.TryParse(alias, out _) || aliases.ContainsKey(alias)) throw new GhException("invalid_alias", "Use unique non-GUID aliases.");
        var node = ComponentCatalog.Create(Guid.Parse(GhJson.Text(op, "component_id")));
        node.Attributes.Pivot = Position(op) ?? new PointF(80 + doc.Objects.Count * 180, 100);
        if (op["name"] is not null) node.NickName = op["name"]!.GetValue<string>();
        if (!doc.AddObject(node, false)) throw new GhException("add_failed", "Component could not be added.");
        aliases.Add(alias, node.InstanceGuid.ToString());
    }

    private static void Connect(GH_Document doc, JsonObject op, Dictionary<string, string> aliases, bool connect)
    {
        var source = Endpoint(doc, GhJson.Object(op["source"]), aliases, false);
        var target = Endpoint(doc, GhJson.Object(op["target"]), aliases, true);
        if (connect)
            GraphRules.RequireAcyclic(doc.Objects.Select(n => n.InstanceGuid.ToString()), Edges(doc).Append(
                new GraphEdge(source.Attributes.GetTopLevel.DocObject.InstanceGuid.ToString(), target.Attributes.GetTopLevel.DocObject.InstanceGuid.ToString())));
        if (connect) { if (!target.Sources.Contains(source)) target.AddSource(source); }
        else target.RemoveSource(source);
        target.ExpireSolution(false);
    }

    public static IGH_Param Endpoint(GH_Document doc, JsonObject endpoint, IReadOnlyDictionary<string, string>? aliases, bool input) =>
        ComponentCatalog.Port(Node(doc, GhJson.Text(endpoint, "node"), aliases), GhJson.Text(endpoint, "port"), input);

    private static void Slider(IGH_DocumentObject node, JsonObject op)
    {
        if (node is not GH_NumberSlider slider) throw new GhException("invalid_value", "set_slider requires a Number Slider.");
        var value = (decimal)GhJson.Number(op["value"]);
        var min = op["minimum"] is null ? slider.Slider.Minimum : (decimal)GhJson.Number(op["minimum"]);
        var max = op["maximum"] is null ? slider.Slider.Maximum : (decimal)GhJson.Number(op["maximum"]);
        var integer = GhJson.Flag(op, "integer", slider.Slider.Type == GH_SliderAccuracy.Integer);
        if (min > max || value < min || value > max || integer && value != decimal.Truncate(value))
            throw new GhException("invalid_value", "Slider value must fit its bounds and accuracy.", "design");
        slider.Slider.Minimum = min; slider.Slider.Maximum = max;
        slider.Slider.Type = integer ? GH_SliderAccuracy.Integer : GH_SliderAccuracy.Float;
        slider.SetSliderValue(value);
    }

    private static void Properties(IGH_DocumentObject node, JsonObject op)
    {
        if (op["name"] is not null) node.NickName = op["name"]!.GetValue<string>();
        if (Position(op) is { } position) node.Attributes.Pivot = position;
        if (op["preview"] is not null && node is IGH_PreviewObject preview) preview.Hidden = !GhJson.Flag(op, "preview");
        if (op["enabled"] is not null && node is IGH_ActiveObject active) active.Locked = !GhJson.Flag(op, "enabled");
    }

    private static PointF? Position(JsonObject op)
    {
        if (op["position"] is null) return null;
        var p = GhJson.Array(op["position"]);
        if (p.Count != 2) throw new GhException("invalid_value", "Position requires x and y.");
        return new PointF((float)GhJson.Number(p[0]), (float)GhJson.Number(p[1]));
    }

    private static void Mapping(IGH_DocumentObject node, JsonObject op)
    {
        var port = ComponentCatalog.Port(node, GhJson.Text(op, "port"), true);
        if (op["mapping"] is not null) port.DataMapping = GhJson.Text(op, "mapping") switch
        {
            "none" => GH_DataMapping.None, "flatten" => GH_DataMapping.Flatten, "graft" => GH_DataMapping.Graft,
            _ => throw new GhException("invalid_value", "Mapping must be none, flatten or graft.", "data-trees"),
        };
        if (op["simplify"] is not null) port.Simplify = GhJson.Flag(op, "simplify");
        if (op["reverse"] is not null) port.Reverse = GhJson.Flag(op, "reverse");
        port.ExpireSolution(false);
    }

    public static IEnumerable<GraphEdge> Edges(GH_Document doc) => doc.Objects.SelectMany(node =>
        ComponentCatalog.Parameters(node, true).SelectMany(p => p.Sources.Select(s =>
            new GraphEdge(s.Attributes.GetTopLevel.DocObject.InstanceGuid.ToString(), node.InstanceGuid.ToString()))));
}
