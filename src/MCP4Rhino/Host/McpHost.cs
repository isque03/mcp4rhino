using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Logic;
using MCP4Rhino.Logic.Telemetry;
using MCP4Rhino.Logic.Protocol;

namespace MCP4Rhino.Host;

/// <summary>
/// Lightweight MCP JSON-RPC host over HTTP POST /mcp (no ASP.NET).
/// Compatible with clients using mcp-remote against http://localhost:PORT/mcp.
/// </summary>
[ExcludeFromCodeCoverage]
public static class McpHost
{
    public const int DefaultPort = SupportInfo.DefaultPort;

    private static HttpListener? _listener;
    private static CancellationTokenSource? _cts;
    private static Task? _loop;
    private static readonly NativeResourceGateway Resources = new();

    public static bool IsRunning => _listener is { IsListening: true };

    public static int Port { get; private set; } = SupportInfo.DefaultPort;

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

        // Preload the read bridge during server startup, never as a side effect of resources/read.
        try { ToolLoader.EnsureLoaded(); }
        catch (Exception ex) { PluginLog.Info($"Tools unavailable at startup: {ex.Message}"); }
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
            var headers = ctx.Request.Headers.AllKeys.Where(key => key is not null)
                .ToDictionary(key => key!, key => ctx.Request.Headers[key!] ?? "", StringComparer.OrdinalIgnoreCase);
            var request = new McpHttpRequest(ctx.Request.HttpMethod, ctx.Request.Url?.AbsolutePath ?? "", headers, "", Port);
            McpHttpTransport.ValidateAuthority(request);
            var body = await ReadBodyAsync(ctx.Request).WaitAsync(TimeSpan.FromSeconds(30));
            if (body is null) { await WriteTextAsync(ctx, 413, "text/plain", "Request too large"); return; }
            var response = McpHttpTransport.Process(request with { Body = body }, Services());
            ctx.Response.Headers["Cache-Control"] = "no-store";
            if (response.Status == 405) ctx.Response.Headers["Allow"] = "POST";
            await WriteTextAsync(ctx, response.Status, response.ContentType, response.Body);
        }
        catch (McpRpcException ex) { await WriteTextAsync(ctx, ex.Code == -32003 ? 403 : 400, "application/json", ex.Response(null).ToJsonString()); }
        catch (DecoderFallbackException) { await WriteTextAsync(ctx, 400, "application/json", new McpRpcException(-32700, "Invalid UTF-8 JSON").Response(null).ToJsonString()); }
        catch (TimeoutException) { await WriteTextAsync(ctx, 408, "text/plain", "Request body timeout"); }
        catch (Exception)
        {
            try { await WriteTextAsync(ctx, 500, "application/json", new McpRpcException(-32603, "Internal server error").Response(null).ToJsonString()); }
            catch { /* disconnected client */ }
        }
    }

    private static async Task<string?> ReadBodyAsync(HttpListenerRequest request)
    {
        if (request.ContentLength64 > McpHttpTransport.MaxRequestBytes) return null;
        using var reader = new StreamReader(request.InputStream, new UTF8Encoding(false, true));
        var body = new StringBuilder(); var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory())) > 0)
        {
            body.Append(buffer, 0, read);
            if (body.Length > McpHttpTransport.MaxRequestBytes) return null;
        }
        return body.ToString();
    }

    private static McpServerServices Services() => new(ToolProvider.Instance)
    {
        HotReload = HotReload, Support = LiveSupport, Telemetry = Telemetry.Gateway,
        Instructions = TelemetryHost.InitializeInstructions, OnInitialized = TelemetryHost.MarkNoticeDelivered,
        Resources = Resources,
    };

    private static SupportContext LiveSupport() => new(
        Endpoint: IsRunning ? Endpoint : SupportInfo.ConfiguredEndpoint(),
        LogPath: PluginLog.LogPath,
        ServerRunning: IsRunning);

    private sealed class ToolProvider : IMcpToolProvider
    {
        public static readonly ToolProvider Instance = new();

        public object[] ListTools()
        {
            ToolLoader.EnsureLoaded();
            var bridge = ToolLoader.Bridge
                ?? throw new InvalidOperationException("Tools bridge not loaded.");
            return bridge.ListTools().Concat(MCP4Rhino.Logic.Grasshopper.GhToolCatalog.ListTools()).ToArray();
        }

        public object CallTool(JsonNode? @params)
        {
            var name = @params?["name"]?.GetValue<string>() ?? "";
            if (MCP4Rhino.Logic.Grasshopper.GhToolCatalog.Contains(name))
                return GrasshopperHost.Call(name, @params?["arguments"] as JsonObject ?? new JsonObject());
            ToolLoader.EnsureLoaded();
            var bridge = ToolLoader.Bridge
                ?? throw new InvalidOperationException("Tools bridge not loaded.");
            if (name == "capture_viewport" && @params?["arguments"]?["grasshopper"] is { } context)
                return GrasshopperHost.CaptureViewport(MCP4Rhino.Logic.Grasshopper.GhJson.Object(context),
                    () => bridge.CallTool(@params));
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
