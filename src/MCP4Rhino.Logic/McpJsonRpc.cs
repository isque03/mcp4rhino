using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Logic.Telemetry;

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
    public const string ServerVersion = "0.6.1-alpha";

    public static JsonNode? Dispatch(
        JsonNode? req,
        IMcpToolProvider tools,
        Func<JsonObject, object>? hotReload = null,
        Func<SupportContext>? supportContext = null,
        ITelemetry? telemetry = null,
        Func<string>? instructions = null,
        Action? onInitialized = null)
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
                    instructions = ResolveInstructions(instructions),
                },
                "ping" => new { },
                "tools/list" => new { tools = ListAllTools(tools, hotReload is not null) },
                "tools/call" => CallTool(@params, tools, hotReload, supportContext, telemetry),
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

        if (string.Equals(method, "initialize", StringComparison.Ordinal))
        {
            try { telemetry?.Track(TelemetryEvents.McpConnected); }
            catch { /* telemetry must not change the initialize result */ }
            try { onInitialized?.Invoke(); }
            catch { /* marking the notice shown must not change the initialize result */ }
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

    private static string ResolveInstructions(Func<string>? instructions)
    {
        if (instructions is null)
            return SupportInfo.ServerInstructions;
        try
        {
            var text = instructions();
            return string.IsNullOrEmpty(text) ? SupportInfo.ServerInstructions : text;
        }
        catch
        {
            return SupportInfo.ServerInstructions;
        }
    }

    private static object CallTool(
        JsonNode? @params,
        IMcpToolProvider tools,
        Func<JsonObject, object>? hotReload,
        Func<SupportContext>? supportContext,
        ITelemetry? telemetry)
    {
        var name = @params?["name"]?.GetValue<string>()
            ?? throw new ArgumentException("tools/call missing name");
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = ExecuteTool(@params, name, tools, hotReload, supportContext);
            ReportTool(telemetry, name, started, ToolSucceeded(result));
            return result;
        }
        catch (Exception)
        {
            ReportTool(telemetry, name, started, success: false);
            throw;
        }
    }

    private static object ExecuteTool(
        JsonNode? @params,
        string name,
        IMcpToolProvider tools,
        Func<JsonObject, object>? hotReload,
        Func<SupportContext>? supportContext)
    {
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

    private static bool ToolSucceeded(object result)
    {
        var isError = result.GetType().GetProperty("isError", BindingFlags.Instance | BindingFlags.Public);
        return isError?.GetValue(result) is not true;
    }

    private static void ReportTool(ITelemetry? telemetry, string name, long started, bool success)
    {
        if (telemetry is null)
            return;
        try
        {
            var ms = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (ms < 0)
                ms = 0;
            telemetry.TrackTool(name, ms, success);
        }
        catch
        {
            // Telemetry must not change the tool result.
        }
    }
}
