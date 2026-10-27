using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using MCP4Rhino.Logic.Resources;

namespace MCP4Rhino.Logic.Protocol;

public static class McpDispatcher
{
    public static JsonNode? Dispatch(JsonNode? value, McpServerServices services, McpRequestContext context)
    {
        var id = value is JsonObject obj ? obj["id"] : null;
        try
        {
            var request = Validate(value);
            var method = request["method"]!.GetValue<string>();
            if (!request.ContainsKey("id")) return Notification(method, context);
            var args = request["params"] as JsonObject ?? new JsonObject();
            var result = Execute(method, args, services, context);
            if (context.IsModern) Decorate(result, method);
            var response = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = id?.DeepClone(), ["result"] = result };
            if (method == "resources/read" && Encoding.UTF8.GetByteCount(response.ToJsonString()) > ResourcePaging.MaxBytes)
                throw new McpRpcException(-32000, "Resource response exceeds 256 KiB; request a smaller page");
            return response;
        }
        catch (McpRpcException ex) { return ex.Response(id); }
        catch (ArgumentException) { return new McpRpcException(-32602, "Invalid request arguments").Response(id); }
        catch (Exception) { return new McpRpcException(-32603, "Internal server error").Response(id); }
    }

    public static JsonObject Capabilities() => new()
        { ["tools"] = new JsonObject(), ["resources"] = new JsonObject(), ["prompts"] = new JsonObject() };

    private static JsonObject Validate(JsonNode? value)
    {
        if (value is not JsonObject request || McpRequestContext.Text(request["jsonrpc"]) != "2.0" ||
            string.IsNullOrWhiteSpace(McpRequestContext.Text(request["method"])) ||
            request["params"] is not null and not JsonObject)
            throw new McpRpcException(-32600, "Invalid JSON-RPC request");
        if (request.ContainsKey("id") && (request["id"] is null || request["id"]!.GetValueKind() is not (JsonValueKind.Number or JsonValueKind.String)))
            throw new McpRpcException(-32600, "Invalid request ID");
        return request;
    }

    private static JsonNode? Notification(string method, McpRequestContext context)
    {
        if (!context.IsModern && method.StartsWith("notifications/", StringComparison.Ordinal)) return null;
        throw new McpRpcException(-32601, "Unsupported notification");
    }

    private static JsonObject Execute(string method, JsonObject args, McpServerServices services, McpRequestContext context) => method switch
    {
        "server/discover" when context.IsModern => new JsonObject
        {
            ["supportedVersions"] = JsonSerializer.SerializeToNode(McpRequestContext.SupportedVersions),
            ["capabilities"] = Capabilities(), ["instructions"] = Instructions(services),
        },
        "initialize" when !context.IsModern => Initialize(args, services, context),
        "ping" => new JsonObject(),
        "resources/list" => ResourceCatalog.List(args),
        "resources/templates/list" => ResourceCatalog.Templates(args),
        "resources/read" => McpResources.Read(args, services.Resources, context),
        "prompts/list" => PromptCatalog.List(args),
        "prompts/get" => PromptCatalog.Get(args),
        "tools/list" or "tools/call" => Tools(method, args, services),
        _ => throw new McpRpcException(-32601, "Method not found"),
    };

    private static JsonObject Initialize(JsonObject args, McpServerServices services, McpRequestContext context)
    {
        var result = Tools("initialize", args, services);
        result["protocolVersion"] = context.Version;
        result["capabilities"] = Capabilities();
        return result;
    }

    private static JsonObject Tools(string method, JsonObject args, McpServerServices services)
    {
        var request = new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = method, ["params"] = args.DeepClone() };
        var response = McpJsonRpc.Dispatch(request, services.Tools, services.HotReload, services.Support,
            services.Telemetry, services.Instructions, services.OnInitialized)!;
        if (response["error"] is { } error)
            throw new McpRpcException(error["code"]!.GetValue<int>(), error["message"]!.GetValue<string>());
        return response["result"]!.DeepClone().AsObject();
    }

    private static string Instructions(McpServerServices services)
    {
        try { return services.Instructions?.Invoke() ?? SupportInfo.ServerInstructions; }
        catch { return SupportInfo.ServerInstructions; }
    }

    private static void Decorate(JsonObject result, string method)
    {
        result["resultType"] = "complete";
        var metadata = result["_meta"]?.DeepClone() as JsonObject ?? new JsonObject();
        metadata["io.modelcontextprotocol/serverInfo"] = new JsonObject
            { ["name"] = McpJsonRpc.ServerName, ["version"] = McpJsonRpc.ServerVersion };
        result["_meta"] = metadata;
        if (method is "server/discover" or "resources/list" or "resources/templates/list" or "resources/read" or "prompts/list" or "tools/list")
        { result["ttlMs"] = 0; result["cacheScope"] = "private"; }
    }
}
