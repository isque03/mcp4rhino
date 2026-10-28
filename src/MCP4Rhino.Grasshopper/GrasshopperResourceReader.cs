using System.Text.Json;
using System.Text.Json.Nodes;
using Grasshopper.Kernel;
using MCP4Rhino.Contracts;
using MCP4Rhino.Logic.Grasshopper;
using MCP4Rhino.Logic.Protocol;
using MCP4Rhino.Logic.Resources;
using Rhino;
using GH = Grasshopper.Instances;

namespace MCP4Rhino.Grasshopper;

internal static class GrasshopperResourceReader
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public static string Read(JsonObject request, IEnumerable<DefinitionSession> sessions)
    {
        try { return JsonSerializer.Serialize(Select(request, sessions.ToArray()), Options); }
        catch (McpRpcException ex) { return JsonSerializer.Serialize(new { error = new { code = ex.Code, message = ex.Message } }); }
        catch (GhException ex) { return JsonSerializer.Serialize(new { error = new { code = -32002, message = ex.Code } }); }
    }

    private static object Select(JsonObject args, DefinitionSession[] sessions)
    {
        var kind = args["kind"]!.GetValue<string>();
        if (kind == "capabilities") return new { status = "initialized", initialized = true,
            installed_component_count = GH.ComponentServer.ObjectProxies.Count(),
            supported_installed_component_count = GH.ComponentServer.ObjectProxies.Count(ComponentCatalog.Supported),
            component_support_levels = new[] { "verified", "available", "restricted" },
            component_authorization = "Available assemblies require local MCP4RHINO_GH_TRUSTED_ASSEMBLIES configuration; restricted types cannot be authorized." };
        var document = RhinoDoc.ActiveDoc;
        if (kind == "component") return new { status = "available", data = ComponentCatalog.ReadDescriptor(Guid.Parse(args["component_id"]!.GetValue<string>())) };
        if (document is null) return new { status = "no_active_document" };
        if (kind == "sessions") return Sessions(document, sessions, args);
        var session = sessions.FirstOrDefault(s => s.Id == args["session_id"]?.GetValue<string>() && s.RhinoSerial == document.RuntimeSerialNumber && !s.IsClosed) ??
            throw new McpRpcException(-32002, "Session is closed, unknown or belongs to an inactive Rhino document");
        session.RequireContext(); session.Identity.Require(args);
        if (kind == "graph" && args["offset"]?.GetValue<int>() > 0 && !args.ContainsKey("expected_revision"))
            throw new McpRpcException(-32602, "Graph continuation requires the original expected_revision");
        if (kind is "result" or "output") session.RequireResult(args["result_id"]!.GetValue<string>());
        var data = kind switch
        {
            "graph" => Graph(session, args),
            "node" => DescribeNode(Node(session, args)),
            "result" => Result(session, args),
            "output" => Output(session, args),
            _ => throw new McpRpcException(-32002, "Unknown Grasshopper resource"),
        };
        return new { status = "available", document_id = DocumentResourceIds.For(document.RuntimeSerialNumber),
            session = session.Summary(), revision = session.Revision, identity = session.Identity,
            units = document.ModelUnitSystem.ToString(), data };
    }

    private static object Sessions(RhinoDoc rhino, DefinitionSession[] sessions, JsonObject args)
    {
        var accessible = sessions.Where(s => s.RhinoSerial == rhino.RuntimeSerialNumber && !s.IsClosed).ToArray();
        var visible = GH.ActiveCanvas?.Document;
        var unattached = visible is not null && !sessions.Any(s => ReferenceEquals(s.Document, visible))
            ? new[] { new { document_id = visible.RuntimeID, name = visible.DisplayName, attached = false,
                revision = (long?)null, modified = visible.IsModified } } : Array.Empty<object>();
        return new { status = "available", document_id = DocumentResourceIds.For(rhino.RuntimeSerialNumber),
            total_sessions = accessible.Length, sessions = ResourcePaging.Page(accessible.Select(s => s.Summary()), args),
            unattached_documents = unattached, unattached_scope = "Only the current visible definition; no inactive document enumeration." };
    }

    private static IGH_DocumentObject Node(DefinitionSession session, JsonObject args) =>
        GraphEditor.Node(session.Document, args["node_id"]!.GetValue<string>());

    private static JsonObject DescribeNode(IGH_DocumentObject node)
    {
        var description = JsonSerializer.SerializeToNode(ResultReader.DescribeNode(node, true), Options)!.AsObject();
        description["input_port_count"] = ComponentCatalog.Parameters(node, true).Count();
        description["output_port_count"] = ComponentCatalog.Parameters(node, false).Count();
        description["port_limit"] = 200; description["source_limit"] = 200;
        var truncated = new JsonArray();
        foreach (var side in new[] { true, false })
            foreach (var port in ComponentCatalog.Parameters(node, side).Take(200).Select((p, index) => (p, index)).Where(pair => pair.p.SourceCount > 200))
                truncated.Add(new JsonObject { ["port"] = (side ? "in:" : "out:") + port.index, ["source_count"] = port.p.SourceCount });
        description["truncated_sources"] = truncated;
        return description;
    }

    private static object Graph(DefinitionSession session, JsonObject args) => new
    {
        total_nodes = session.Document.Objects.Count,
        nodes = ResourcePaging.Page(session.Document.Objects.Select(node => (object)DescribeNode(node)), args),
    };

    private static object Result(DefinitionSession session, JsonObject args) => new
    {
        state = "current", milliseconds = session.Document.SolutionSpan.TotalMilliseconds,
        nodes = ResourcePaging.Page(session.Document.Objects.Select(node => (object)new
        {
            node_id = node.InstanceGuid, diagnostics = ResultReader.Diagnostics(node),
            output_port_count = ComponentCatalog.Parameters(node, false).Count(), output_port_limit = 200,
            outputs = ComponentCatalog.Parameters(node, false).Take(200).Select((port, index) => new
            {
                output_index = index, port = "out:" + index, type = port.TypeName,
                branch_count = port.VolatileData.PathCount, item_count = port.VolatileDataCount,
                uri = $"mcp4rhino://grasshopper/sessions/{session.Id}/results/{session.ResultId}/nodes/{node.InstanceGuid}/outputs/{index}",
            }).ToArray(),
        }), args),
    };

    private static object Output(DefinitionSession session, JsonObject args)
    {
        var node = Node(session, args);
        var port = ComponentCatalog.Port(node, "out:" + args["output_index"]!.GetValue<string>(), false);
        return new { type = port.TypeName, branch_count = port.VolatileData.PathCount, item_count = port.VolatileDataCount,
            rows = ResourcePaging.Page(ValueCodec.Rows(port.VolatileData, measurements: false,
                offset: args["offset"]?.GetValue<int>() ?? 0), args, alreadySkipped: true) };
    }
}
