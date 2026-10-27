using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Logic.Telemetry;

namespace MCP4Rhino.Logic.Protocol;

public sealed record McpRequestContext(string Version, JsonObject ClientCapabilities)
{
    public const string Modern = "2026-07-28";
    public const string Streamable = "2025-11-25";
    public const string Legacy = McpJsonRpc.ProtocolVersion;
    public static readonly string[] SupportedVersions = [Modern, Streamable, Legacy];
    public bool IsModern => Version == Modern;

    public static McpRequestContext From(JsonObject request, string? header = null)
    {
        var args = request["params"] as JsonObject;
        var metadata = args?["_meta"] as JsonObject;
        var declared = Text(metadata?["io.modelcontextprotocol/protocolVersion"]);
        if (metadata?.Any(pair => pair.Key.StartsWith("io.modelcontextprotocol/", StringComparison.Ordinal)) == true && declared is null)
            throw new McpRpcException(-32602, "Modern metadata requires a string protocolVersion");
        var opening = Text(request["method"]) == "initialize";
        if (opening && declared is null && header is not null && Text(args?["protocolVersion"]) is { } requested && requested != header)
            throw new McpRpcException(-32020, "Protocol header does not match initialization version");
        var version = declared ?? header ?? (opening ? Text(args?["protocolVersion"]) : null) ?? Legacy;
        if (opening && header is null && declared is null && !SupportedVersions.Contains(version)) version = Streamable;
        if (!SupportedVersions.Contains(version))
            throw new McpRpcException(-32022, "Unsupported protocol version", new { supported = SupportedVersions, requested = version });
        if (declared is not null && header is not null && declared != header)
            throw new McpRpcException(-32020, "Protocol header does not match request metadata");
        if (version != Modern) return new(version, new JsonObject());
        if (declared != Modern || metadata?["io.modelcontextprotocol/clientCapabilities"] is not JsonObject capabilities)
            throw new McpRpcException(-32602, "Modern requests require protocolVersion and clientCapabilities metadata");
        if (metadata["io.modelcontextprotocol/clientInfo"] is { } info &&
            (info is not JsonObject || string.IsNullOrWhiteSpace(Text(info["name"])) || string.IsNullOrWhiteSpace(Text(info["version"]))))
            throw new McpRpcException(-32602, "clientInfo requires name and version");
        return new(version, (JsonObject)capabilities.DeepClone());
    }

    internal static string? Text(JsonNode? node) => node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}

public sealed class McpRpcException(int code, string message, object? details = null) : Exception(message)
{
    public int Code { get; } = code;
    public object? Details { get; } = details;
    public JsonObject Response(JsonNode? id) => new()
    {
        ["jsonrpc"] = "2.0", ["id"] = id?.GetValueKind() is JsonValueKind.String or JsonValueKind.Number ? id.DeepClone() : null,
        ["error"] = new JsonObject { ["code"] = Code, ["message"] = Message, ["data"] = JsonSerializer.SerializeToNode(Details) },
    };
}

public sealed record McpServerServices(IMcpToolProvider Tools)
{
    public Func<JsonObject, object>? HotReload { get; init; }
    public Func<SupportContext>? Support { get; init; }
    public ITelemetry? Telemetry { get; init; }
    public Func<string>? Instructions { get; init; }
    public Action? OnInitialized { get; init; }
    public Resources.IMcpResourceGateway? Resources { get; init; }
}
