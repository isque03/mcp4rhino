using System.Text.Json;
using System.Text.Json.Nodes;

namespace MCP4Rhino.Logic;

public interface IMcpToolProvider
{
    object[] ListTools();
    object CallTool(JsonNode? @params);
}

/// <summary>Pure MCP JSON-RPC request dispatch (no HTTP).</summary>
public static class McpJsonRpc
{
    public const string ProtocolVersion = "2024-11-05";
    public const string ServerName = "MCP4Rhino";
    public const string ServerVersion = "0.4.0";

    public static JsonNode? Dispatch(JsonNode? req, IMcpToolProvider tools, Func<JsonObject, object>? hotReload = null)
    {
        if (req is null) return null;

        var method = req["method"]?.GetValue<string>();
        var id = req["id"];
        var @params = req["params"];

        if (method is not null && method.StartsWith("notifications/", StringComparison.Ordinal))
            return null;

        object? result;
        try
        {
            result = method switch
            {
                "initialize" => new
                {
                    protocolVersion = ProtocolVersion,
                    capabilities = new { tools = new { } },
                    serverInfo = new { name = ServerName, version = ServerVersion },
                },
                "ping" => new { },
                "tools/list" => new { tools = ListAllTools(tools, hotReload is not null) },
                "tools/call" => CallTool(@params, tools, hotReload),
                null => throw new InvalidOperationException("Missing method"),
                _ => throw new InvalidOperationException($"Method not found: {method}"),
            };
        }
        catch (Exception ex)
        {
            return JsonSerializer.SerializeToNode(new
            {
                jsonrpc = "2.0",
                id,
                error = new { code = -32000, message = ex.Message },
            });
        }

        return JsonSerializer.SerializeToNode(new
        {
            jsonrpc = "2.0",
            id,
            result,
        });
    }

    public static object ParseError(JsonNode? id, string message) => new
    {
        jsonrpc = "2.0",
        id,
        error = new { code = -32700, message },
    };

    private static object[] ListAllTools(IMcpToolProvider tools, bool includeReload)
    {
        var hostTools = includeReload
            ? new object[]
            {
                new
                {
                    name = "mcp4rhino_reload",
                    description = "Hot-reload MCP4Rhino.Tools.dll from disk (no Rhino restart). Call after rebuilding tools.",
                    inputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            path = new
                            {
                                type = "string",
                                description = "Optional path to MCP4Rhino.Tools.dll (or directory containing it).",
                            },
                        },
                    },
                },
            }
            : [];
        return hostTools.Concat(tools.ListTools()).ToArray();
    }

    private static object CallTool(JsonNode? @params, IMcpToolProvider tools, Func<JsonObject, object>? hotReload)
    {
        var name = @params?["name"]?.GetValue<string>()
            ?? throw new ArgumentException("tools/call missing name");
        var args = @params?["arguments"] as JsonObject ?? new JsonObject();

        if (name == "mcp4rhino_reload")
        {
            if (hotReload is null)
                throw new InvalidOperationException("Hot-reload is not available.");
            return hotReload(args);
        }

        return tools.CallTool(@params);
    }
}
