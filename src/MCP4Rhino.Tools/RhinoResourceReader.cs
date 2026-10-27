using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Contracts;
using MCP4Rhino.Logic.Protocol;
using MCP4Rhino.Logic.Resources;
using Rhino;
using Rhino.DocObjects;

namespace MCP4Rhino.Tools;

internal static class RhinoResourceReader
{
    public static string Read(JsonObject request)
    {
        try { return JsonSerializer.Serialize(ReadActive(request)); }
        catch (McpRpcException ex) { return new JsonObject { ["error"] = new JsonObject { ["code"] = ex.Code, ["message"] = ex.Message } }.ToJsonString(); }
    }

    private static object ReadActive(JsonObject args)
    {
        var document = RhinoDoc.ActiveDoc;
        var requested = args["document_id"]?.GetValue<string>();
        if (document is null)
        {
            if (requested != "active") throw new McpRpcException(-32002, "Document is closed or inactive");
            return new { status = "no_active_document", instance_id = DocumentResourceIds.InstanceId };
        }
        var id = DocumentResourceIds.For(document.RuntimeSerialNumber);
        if (requested != "active" && !DocumentResourceIds.Matches(requested ?? "", document.RuntimeSerialNumber))
            throw new McpRpcException(-32002, "Document is closed or inactive");
        return new { status = "available", document_id = id, instance_id = DocumentResourceIds.InstanceId,
            units = RhinoReadModel.Units(document), data = Select(document, args) };
    }

    private static object Select(RhinoDoc document, JsonObject args) => args["kind"]!.GetValue<string>() switch
    {
        "document" => Overview(document, args),
        "object" => Object(FindObject(document, args)),
        "layer" => LayerDetails(document, document.Layers.FirstOrDefault(layer => !layer.IsDeleted && layer.Id.ToString() == args["layer_id"]!.GetValue<string>()) ??
            throw new McpRpcException(-32002, "Layer not found")),
        "view" => View(document.Views.Cast<Rhino.Display.RhinoView>().FirstOrDefault(view =>
            view.ActiveViewport.Id.ToString() == args["view_id"]!.GetValue<string>()) ?? throw new McpRpcException(-32002, "View not found")),
        _ => throw new McpRpcException(-32002, "Unknown Rhino resource"),
    };

    private static object Overview(RhinoDoc document, JsonObject args)
    {
        var sample = document.Objects.Where(obj => obj is not null && !obj.IsDeleted).Take(10001).ToArray();
        return new
        {
            name = document.Name, saved = !string.IsNullOrEmpty(document.Path), modified = document.Modified,
            object_count = document.Objects.Count, layer_count = document.Layers.Count,
            active_layer_id = document.Layers.CurrentLayer?.Id, active_view_id = document.Views.ActiveView?.ActiveViewport.Id,
            counts_by_type = sample.Take(10000).GroupBy(obj => ToolHelpers.Classify(obj.Geometry)).ToDictionary(g => g.Key, g => g.Count()),
            counts_complete = sample.Length <= 10000, counted_objects = Math.Min(sample.Length, 10000),
            objects = ResourcePaging.Page(document.Objects.Where(obj => obj is not null && !obj.IsDeleted).Select(obj =>
                (object)new { object_id = obj.Id, type = ToolHelpers.Classify(obj.Geometry), name = obj.Name }), args),
            layers = ResourcePaging.Page(document.Layers.Where(layer => !layer.IsDeleted).Select(layer => (object)Layer(layer)), args),
            views = ResourcePaging.Page(document.Views.Cast<Rhino.Display.RhinoView>().Select(view => (object)View(view)), args),
        };
    }

    private static RhinoObject FindObject(RhinoDoc document, JsonObject args) =>
        document.Objects.FindId(Guid.Parse(args["object_id"]!.GetValue<string>())) is { IsDeleted: false } obj ? obj :
            throw new McpRpcException(-32002, "Object not found");

    private static object Object(RhinoObject obj)
    {
        var bounds = obj.Geometry.GetBoundingBox(false);
        var layer = obj.Document.Layers[obj.Attributes.LayerIndex];
        return new { object_id = obj.Id, type = ToolHelpers.Classify(obj.Geometry), name = obj.Name,
            layer_id = layer.Id, visible = !obj.IsHidden && layer.IsVisible, locked = obj.IsLocked || layer.IsLocked,
            bounds = bounds.IsValid ? new { min = new[] { bounds.Min.X, bounds.Min.Y, bounds.Min.Z },
                max = new[] { bounds.Max.X, bounds.Max.Y, bounds.Max.Z } } : null };
    }

    private static object Layer(Layer layer) => new { layer_id = layer.Id, name = layer.FullPath, visible = layer.IsVisible, locked = layer.IsLocked };

    private static object LayerDetails(RhinoDoc document, Layer layer)
    {
        var sample = document.Objects.Where(obj => obj is not null && !obj.IsDeleted).Take(10001).ToArray();
        return new { metadata = Layer(layer), counted_objects = Math.Min(sample.Length, 10000),
            object_count = sample.Take(10000).Count(obj => obj.Attributes.LayerIndex == layer.Index), counts_complete = sample.Length <= 10000 };
    }

    private static object View(Rhino.Display.RhinoView view)
    {
        var viewport = view.ActiveViewport;
        return new { view_id = viewport.Id, name = viewport.Name, parallel = viewport.IsParallelProjection,
            camera = new[] { viewport.CameraLocation.X, viewport.CameraLocation.Y, viewport.CameraLocation.Z },
            target = new[] { viewport.CameraTarget.X, viewport.CameraTarget.Y, viewport.CameraTarget.Z } };
    }
}
