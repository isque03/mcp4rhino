using System.Text.Json;
using System.Text.Json.Nodes;

namespace MCP4Rhino.Logic;

public interface IMcpToolProvider
{
    object[] ListTools();
    object CallTool(JsonNode? @params);
}

/// <summary>
/// Live endpoint/log for <c>get_support_info</c>. Host injects running endpoint and log path;
/// unit tests may omit the callback and use configured defaults.
/// </summary>
public readonly record struct SupportContext(string Endpoint, string LogPath, bool ServerRunning);

/// <summary>Pure MCP JSON-RPC request dispatch (no HTTP).</summary>
public static class McpJsonRpc
{
    public const string ProtocolVersion = "2024-11-05";
    public const string ServerName = "MCP4Rhino";
    public const string ServerVersion = "0.5.2";

    public static JsonNode? Dispatch(
        JsonNode? req,
        IMcpToolProvider tools,
        Func<JsonObject, object>? hotReload = null,
        Func<SupportContext>? supportContext = null)
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
                    instructions = SupportInfo.ServerInstructions,
                },
                "ping" => new { },
                "tools/list" => new { tools = ListAllTools(tools, hotReload is not null) },
                "tools/call" => CallTool(@params, tools, hotReload, supportContext),
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
        var hostTools = new List<object>
        {
            new
            {
                name = "get_support_info",
                description =
                    "Return MCP4Rhino version, endpoint, log path, and GitHub Issues URLs for filing bugs or enhancement requests. " +
                    "Call this before opening an issue so the report includes the right metadata.",
                inputSchema = new { type = "object", properties = new { } },
            },
        };

        if (includeReload)
        {
            hostTools.Add(new
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
            });
        }

        return hostTools.Concat(tools.ListTools()).ToArray();
    }

    private static object CallTool(
        JsonNode? @params,
        IMcpToolProvider tools,
        Func<JsonObject, object>? hotReload,
        Func<SupportContext>? supportContext)
    {
        var name = @params?["name"]?.GetValue<string>()
            ?? throw new ArgumentException("tools/call missing name");
        var args = @params?["arguments"] as JsonObject ?? new JsonObject();

        if (name == "get_support_info")
        {
            // Design rule: when host injects SupportContext, use live endpoint/log.
            // Without injection (unit tests), use ConfigureEndpoint/DefaultLogPath defaults.
            if (supportContext is not null)
            {
                var ctx = supportContext();
                return SupportInfo.ToolContentResult(ctx.Endpoint, ctx.LogPath, ctx.ServerRunning);
            }

            return SupportInfo.ToolContentResult();
        }

        if (name == "mcp4rhino_reload")
        {
            if (hotReload is null)
                throw new InvalidOperationException("Hot-reload is not available.");
            return hotReload(args);
        }

        return tools.CallTool(@params);
    }
}
