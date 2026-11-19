using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Logic.Feedback;
using MCP4Rhino.Logic.Telemetry;
using MCP4Rhino.Contracts;

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
    public const string ServerVersion = "0.7.5-alpha";

    public static JsonNode? Dispatch(
        JsonNode? req,
        IMcpToolProvider tools,
        Func<JsonObject, object>? hotReload = null,
        Func<SupportContext>? supportContext = null,
        ITelemetry? telemetry = null,
        Func<string>? instructions = null,
        Action? onInitialized = null,
        FeedbackService? feedback = null)
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
                "tools/call" => CallTool(@params, tools, hotReload, supportContext, telemetry, feedback),
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
            new
            {
                name = FeedbackRequest.ToolName,
                description =
                    "Send a short message to the MCP4Rhino maintainers. Call this ONLY when the user explicitly asks to send " +
                    "feedback, a bug report or a feature request to the MCP4Rhino maintainers. Never call it on your own " +
                    "initiative, and never to report your own observations. If the client supports it, show the user the exact " +
                    "text and get their approval before sending. Only the message, category, tool name, plugin version, Rhino " +
                    "version, OS and a timestamp are sent. No model content, document names, file paths or install identifiers " +
                    "are attached, so do not put them in the message. It works even when usage data is turned off.",
                inputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        message = new
                        {
                            type = "string",
                            minLength = 1,
                            maxLength = FeedbackRequest.MaxMessageLength,
                            description = "The feedback text, exactly as the user approved it.",
                        },
                        category = new
                        {
                            type = "string",
                            @enum = FeedbackRequest.Categories,
                            @default = FeedbackRequest.DefaultCategory,
                            description = "bug, feature_request, praise or other (default other).",
                        },
                        tool = new
                        {
                            type = "string",
                            description = "Optional. The MCP4Rhino tool name this feedback is about.",
                        },
                    },
                    required = new[] { "message" },
                    additionalProperties = false,
                },
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
        ITelemetry? telemetry,
        FeedbackService? feedback)
    {
        var name = @params?["name"]?.GetValue<string>()
            ?? throw new ArgumentException("tools/call missing name");
        var started = Stopwatch.GetTimestamp();
        try
        {
            var result = ExecuteTool(@params, name, tools, hotReload, supportContext, feedback);
            var diagnostics = (result as ToolCallResult)?.Diagnostics;
            if (result is ToolCallResult execution) result = execution.Result;
            ReportTool(telemetry, name, started, ToolTelemetry.FromResult(name, result, diagnostics));
            return result;
        }
        catch (Exception ex)
        {
            // The provider also loads assemblies and dispatches names, so its phase is unknown here.
            ToolErrorStage? stage = name switch
            {
                "mcp4rhino_reload" => hotReload is null ? ToolErrorStage.Dispatch : ToolErrorStage.Execution,
                "get_support_info" => ToolErrorStage.Execution,
                FeedbackRequest.ToolName => ToolErrorStage.Execution,
                _ => null,
            };
            ReportTool(telemetry, name, started, ToolTelemetry.FromException(ex, stage));
            throw;
        }
    }

    private static object ExecuteTool(
        JsonNode? @params,
        string name,
        IMcpToolProvider tools,
        Func<JsonObject, object>? hotReload,
        Func<SupportContext>? supportContext,
        FeedbackService? feedback)
    {
        var args = @params?["arguments"] as JsonObject ?? new JsonObject();

        if (name == FeedbackRequest.ToolName)
            return SendFeedback(args, tools, hotReload is not null, feedback);

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

    private static object SendFeedback(JsonObject args, IMcpToolProvider tools, bool includeReload, FeedbackService? feedback)
    {
        var request = FeedbackRequest.Parse(args, RegisteredToolNames(tools, includeReload));
        var result = feedback is null
            ? new FeedbackResult(false, "Feedback is not available in this install. Open an issue at " + SupportInfo.NewIssueUrl + " instead.")
            : feedback.Send(request);
        return new
        {
            content = new object[] { new { type = "text", text = result.Message } },
            isError = !result.Sent,
        };
    }

    private static HashSet<string> RegisteredToolNames(IMcpToolProvider tools, bool includeReload)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tool in ListAllTools(tools, includeReload))
        {
            var name = JsonSerializer.SerializeToNode(tool)?["name"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(name))
                names.Add(name);
        }

        return names;
    }

    private static void ReportTool(ITelemetry? telemetry, string name, long started, ToolOutcome outcome)
    {
        if (telemetry is null)
            return;
        try
        {
            var ms = (long)Stopwatch.GetElapsedTime(started).TotalMilliseconds;
            if (ms < 0)
                ms = 0;
            telemetry.TrackTool(name, ms, outcome.Success, outcome.Diagnostics);
        }
        catch
        {
            // Telemetry must not change the tool result.
        }
    }
}
