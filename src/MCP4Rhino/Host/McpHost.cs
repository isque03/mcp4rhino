using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Logic;

namespace MCP4Rhino.Host;

/// <summary>
/// Lightweight MCP JSON-RPC host over HTTP POST /mcp (no ASP.NET).
/// Compatible with clients using mcp-remote against http://localhost:PORT/mcp.
/// </summary>
[ExcludeFromCodeCoverage]
public static class McpHost
{
    public const int DefaultPort = 4010;

    private static HttpListener? _listener;
    private static CancellationTokenSource? _cts;
    private static Task? _loop;

    public static bool IsRunning => _listener is { IsListening: true };

    public static int Port { get; private set; } = DefaultPort;

    public static string Endpoint => $"http://127.0.0.1:{Port}/mcp";

    public static bool Start(int? port = null)
    {
        if (IsRunning)
            return false;

        Port = port is > 0 and < 65536 ? port.Value : DefaultPort;
        PluginLog.Info($"McpHost.Start begin port={Port}");

        var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        try
        {
            listener.Start();
        }
        catch (Exception ex)
        {
            PluginLog.Info($"McpHost.Start failed to bind: {ex.Message}");
            return false;
        }

        _listener = listener;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        _loop = Task.Run(() => ListenLoopAsync(listener, token), token);

        PluginLog.Info($"McpHost.Start listening on {Endpoint}");
        return true;
    }

    public static void Stop()
    {
        if (_listener is null)
            return;

        PluginLog.Info("McpHost.Stop begin");
        try
        {
            _cts?.Cancel();
            _listener.Stop();
            _listener.Close();
        }
        catch
        {
            // best-effort
        }
        finally
        {
            _listener = null;
            _loop = null;
            _cts?.Dispose();
            _cts = null;
            PluginLog.Info("McpHost.Stop done");
        }
    }

    private static async Task ListenLoopAsync(HttpListener listener, CancellationToken token)
    {
        while (!token.IsCancellationRequested && listener.IsListening)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await listener.GetContextAsync().WaitAsync(token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException)
            {
                break;
            }
            catch (Exception ex)
            {
                PluginLog.Info($"ListenLoop accept error: {ex.Message}");
                continue;
            }

            _ = Task.Run(() => HandleRequestAsync(ctx), token);
        }
    }

    private static async Task HandleRequestAsync(HttpListenerContext ctx)
    {
        try
        {
            var path = ctx.Request.Url?.AbsolutePath.TrimEnd('/') ?? "";
            if (!path.Equals("/mcp", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.StatusCode = 404;
                ctx.Response.Close();
                return;
            }

            if (!string.Equals(ctx.Request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                await WriteTextAsync(ctx, 200, "text/plain", "MCP4Rhino OK\n");
                return;
            }

            string body;
            using (var reader = new StreamReader(ctx.Request.InputStream, ctx.Request.ContentEncoding))
                body = await reader.ReadToEndAsync();

            JsonNode? root;
            try
            {
                root = JsonNode.Parse(body);
            }
            catch
            {
                await WriteJsonAsync(ctx, McpJsonRpc.ParseError(null, "Parse error"));
                return;
            }

            if (root is JsonArray arr)
            {
                var results = new JsonArray();
                foreach (var item in arr)
                {
                    var r = Dispatch(item);
                    if (r is not null)
                        results.Add(r);
                }
                await WriteJsonAsync(ctx, results);
                return;
            }

            var response = Dispatch(root);
            if (response is null)
            {
                ctx.Response.StatusCode = 202;
                ctx.Response.Close();
                return;
            }

            await WriteJsonAsync(ctx, response);
        }
        catch (Exception ex)
        {
            PluginLog.Info($"HandleRequest error: {ex.Message}");
            try
            {
                await WriteJsonAsync(ctx, new
                {
                    jsonrpc = "2.0",
                    id = (object?)null,
                    error = new { code = -32603, message = ex.Message },
                });
            }
            catch
            {
                // ignore
            }
        }
    }

    private static JsonNode? Dispatch(JsonNode? req) =>
        McpJsonRpc.Dispatch(req, ToolProvider.Instance, HotReload);

    private sealed class ToolProvider : IMcpToolProvider
    {
        public static readonly ToolProvider Instance = new();

        public object[] ListTools()
        {
            ToolLoader.EnsureLoaded();
            var bridge = ToolLoader.Bridge
                ?? throw new InvalidOperationException("Tools bridge not loaded.");
            return bridge.ListTools();
        }

        public object CallTool(JsonNode? @params)
        {
            ToolLoader.EnsureLoaded();
            var bridge = ToolLoader.Bridge
                ?? throw new InvalidOperationException("Tools bridge not loaded.");
            return bridge.CallTool(@params);
        }
    }

    private static object HotReload(JsonObject args)
    {
        var path = args["path"]?.GetValue<string>();
        if (!string.IsNullOrWhiteSpace(path))
            Environment.SetEnvironmentVariable("MCP4RHINO_TOOLS_PATH", path);

        PluginLog.Info("mcp4rhino_reload via MCP tools/call");
        ToolLoader.Reload();

        var text = JsonSerializer.Serialize(new
        {
            ok = true,
            loaded_from = ToolLoader.LoadedFrom,
            message = "Tools hot-reloaded. New tool code is active; HTTP server kept running.",
        });

        return McpContent.TextOnly(text);
    }

    private static async Task WriteJsonAsync(HttpListenerContext ctx, object payload)
    {
        var json = payload is JsonNode node
            ? node.ToJsonString()
            : JsonSerializer.Serialize(payload);
        await WriteTextAsync(ctx, 200, "application/json", json);
    }

    private static async Task WriteTextAsync(HttpListenerContext ctx, int status, string contentType, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = contentType;
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }
}
