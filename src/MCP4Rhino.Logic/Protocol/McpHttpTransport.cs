using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;

namespace MCP4Rhino.Logic.Protocol;

public sealed record McpHttpRequest(string Method, string Path, IReadOnlyDictionary<string, string> Headers, string Body, int Port)
{
    public string? Header(string name) => Headers.FirstOrDefault(pair => pair.Key.Equals(name, StringComparison.OrdinalIgnoreCase)).Value;
}

public sealed record McpHttpResponse(int Status, string ContentType, string Body);

/// <summary>Transport policy and dispatch, independent of HttpListener and native Rhino state.</summary>
public static class McpHttpTransport
{
    public const int MaxRequestBytes = 4 * 1024 * 1024;

    public static McpHttpResponse Process(McpHttpRequest request, McpServerServices services)
    {
        JsonNode? body = null;
        try
        {
            ValidateAuthority(request);
            var path = request.Path.TrimEnd('/');
            if (path == "/health" && request.Method == "GET") return new(200, "text/plain", "MCP4Rhino OK\n");
            if (path != "/mcp") return new(404, "text/plain", "Not found");
            if (request.Method != "POST") return Probe(request);
            if (Encoding.UTF8.GetByteCount(request.Body) > MaxRequestBytes) return new(413, "text/plain", "Request too large");
            try { body = JsonNode.Parse(request.Body); }
            catch { throw new McpRpcException(-32700, "Parse error"); }
            if (body is JsonArray batch) return Batch(request, batch, services);
            if (body is not JsonObject message) throw new McpRpcException(-32600, "Invalid JSON-RPC request");
            var context = McpRequestContext.From(message, request.Header("MCP-Protocol-Version"));
            ValidateHeaders(request, message, context);
            return Serialize(McpDispatcher.Dispatch(message, services, context), context.IsModern);
        }
        catch (McpRpcException ex)
        {
            var status = ex.Code == -32003 ? 403 : 400;
            return Json(status, ex.Response(body is JsonObject obj ? obj["id"] : null));
        }
    }

    public static void ValidateAuthority(McpHttpRequest request)
    {
        var host = request.Header("Host");
        if (host != $"127.0.0.1:{request.Port}" && host != $"localhost:{request.Port}")
            throw new McpRpcException(-32003, "Non-local Host rejected");
        if (request.Header("Origin") is { } origin &&
            origin != $"http://127.0.0.1:{request.Port}" && origin != $"http://localhost:{request.Port}")
            throw new McpRpcException(-32003, "Foreign Origin rejected");
    }

    private static McpHttpResponse Probe(McpHttpRequest request) => request.Method == "GET" &&
        request.Header("MCP-Protocol-Version") is null && !(request.Header("Accept") ?? "").Contains("text/event-stream", StringComparison.OrdinalIgnoreCase)
        ? new(200, "text/plain", "MCP4Rhino OK\n") : new(405, "text/plain", "Use POST for MCP requests");

    private static void ValidateHeaders(McpHttpRequest request, JsonObject message, McpRequestContext context)
    {
        if (context.Version == McpRequestContext.Legacy) return;
        var contentType = request.Header("Content-Type")?.Split(';')[0].Trim();
        if (!string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase) ||
            !Accepts(request, "application/json") || !Accepts(request, "text/event-stream"))
            throw new McpRpcException(-32600, "MCP POST requires application/json and Accept for JSON and event streams");
        if (!context.IsModern) return;
        Match(request, "MCP-Protocol-Version", context.Version);
        Match(request, "Mcp-Method", message["method"]?.GetValue<string>());
        if (McpRequestContext.Text(message["method"]) is "tools/call" or "prompts/get" or "resources/read")
            Match(request, "Mcp-Name", McpRequestContext.Text(message["params"]?["name"] ?? message["params"]?["uri"]), true);
    }

    private static bool Accepts(McpHttpRequest request, string type) => (request.Header("Accept") ?? "").Split(',').Any(part =>
        MediaTypeWithQualityHeaderValue.TryParse(part.Trim(), out var item) && item.Quality != 0 &&
        string.Equals(item.MediaType, type, StringComparison.OrdinalIgnoreCase));

    private static void Match(McpHttpRequest request, string name, string? expected, bool encoded = false)
    {
        var actual = request.Header(name);
        if (actual is null || actual != actual.Trim() || actual.Any(c => c < 32 || c > 126))
            throw new McpRpcException(-32020, $"Missing or malformed {name} header");
        if (encoded && actual.StartsWith("=?base64?", StringComparison.Ordinal) && actual.EndsWith("?=", StringComparison.Ordinal))
        {
            try { actual = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(actual[9..^2])); }
            catch { throw new McpRpcException(-32020, $"Malformed {name} header encoding"); }
        }
        if (expected is null || actual != expected) throw new McpRpcException(-32020, $"{name} header does not match request body");
    }

    private static McpHttpResponse Batch(McpHttpRequest request, JsonArray batch, McpServerServices services)
    {
        if (batch.Count == 0 || request.Header("MCP-Protocol-Version") is not null || batch.Any(item =>
            item is not JsonObject obj || McpRequestContext.From(obj).Version != McpRequestContext.Legacy))
            throw new McpRpcException(-32600, "Batches are supported only by the legacy compatibility profile");
        var results = new JsonArray();
        foreach (var item in batch)
        {
            var result = McpDispatcher.Dispatch(item, services, new(McpRequestContext.Legacy, new JsonObject()));
            if (result is not null) results.Add(result);
        }
        return results.Count == 0 ? new(202, "application/json", "") : Json(200, results);
    }

    private static McpHttpResponse Serialize(JsonNode? response, bool modern)
    {
        if (response is null) return new(202, "application/json", "");
        var code = response["error"]?["code"]?.GetValue<int>();
        var status = modern && code == -32601 ? 404 : code is -32600 or -32602 or -32700 ? 400 : 200;
        return Json(status, response);
    }

    private static McpHttpResponse Json(int status, JsonNode response) => new(status, "application/json", response.ToJsonString());
}
