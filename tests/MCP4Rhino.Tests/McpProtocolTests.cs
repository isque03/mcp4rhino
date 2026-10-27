using System.Text;
using System.Text.Json.Nodes;
using MCP4Rhino.Logic;
using MCP4Rhino.Logic.Protocol;
using MCP4Rhino.Logic.Resources;
using Xunit;

namespace MCP4Rhino.Tests;

internal sealed class RecordingTools : IMcpToolProvider
{
    public int Calls;
    public object[] ListTools() { Calls++; return [new { name = "echo", inputSchema = new { type = "object" } }]; }
    public object CallTool(JsonNode? args) { Calls++; return McpContent.TextOnly(args?["arguments"]?["text"]?.ToString() ?? "ok"); }
}

internal static class McpTestRequests
{
    public static JsonObject Message(string method, string version = McpRequestContext.Modern, JsonObject? args = null)
    {
        args = args is null ? new() : (JsonObject)args.DeepClone();
        if (version == McpRequestContext.Modern) args["_meta"] = new JsonObject
        {
            ["io.modelcontextprotocol/protocolVersion"] = version,
            ["io.modelcontextprotocol/clientCapabilities"] = new JsonObject(),
            ["io.modelcontextprotocol/clientInfo"] = new JsonObject { ["name"] = "test", ["version"] = "1" },
        };
        return new JsonObject { ["jsonrpc"] = "2.0", ["id"] = 1, ["method"] = method, ["params"] = args };
    }

    public static McpHttpRequest Http(JsonObject message, string version = McpRequestContext.Modern, int port = 4010)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        { ["Host"] = $"127.0.0.1:{port}", ["Content-Type"] = "application/json", ["Accept"] = "application/json, text/event-stream" };
        if (version != McpRequestContext.Legacy) headers["MCP-Protocol-Version"] = version;
        if (version == McpRequestContext.Modern)
        {
            headers["Mcp-Method"] = message["method"]!.GetValue<string>();
            if ((message["params"]?["name"] ?? message["params"]?["uri"]) is { } name)
                headers["Mcp-Name"] = name.GetValue<string>();
        }
        return new("POST", "/mcp", headers, message.ToJsonString(), port);
    }
}

public class McpProtocolTests
{
    [Fact]
    public void ModernEnvelopePreservesToolMetadataAndTextFirstContent()
    {
        var message = McpTestRequests.Message("tools/call", args: new JsonObject { ["name"] = "metadata" });
        var response = McpDispatcher.Dispatch(message, new(new MetadataTools()), McpRequestContext.From(message))!;
        var result = response["result"]!;
        Assert.Equal("unchanged", result["_meta"]!["vendor/value"]!.GetValue<string>());
        Assert.NotNull(result["_meta"]!["io.modelcontextprotocol/serverInfo"]);
        Assert.Equal("text", result["content"]![0]!["type"]!.GetValue<string>());
        Assert.Equal("complete", result["resultType"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(McpRequestContext.Legacy)]
    [InlineData(McpRequestContext.Streamable)]
    [InlineData(McpRequestContext.Modern)]
    public void ProfilesAdvertiseImplementedCapabilitiesAndKeepToolContent(string version)
    {
        var tools = new RecordingTools(); var services = new McpServerServices(tools);
        var opening = version == McpRequestContext.Modern ? "server/discover" : "initialize";
        var init = McpTestRequests.Message(opening, version, new JsonObject { ["protocolVersion"] = version });
        var response = McpHttpTransport.Process(McpTestRequests.Http(init, version), services);
        Assert.Equal(200, response.Status);
        var result = JsonNode.Parse(response.Body)!["result"]!;
        foreach (var capability in new[] { "tools", "resources", "prompts" }) Assert.NotNull(result["capabilities"]![capability]);
        Assert.DoesNotContain("listChanged", result.ToJsonString()); Assert.DoesNotContain("subscribe", result.ToJsonString());
        Assert.Equal(version == McpRequestContext.Modern, result["resultType"] is not null);
        var call = McpTestRequests.Message("tools/call", version, new JsonObject { ["name"] = "echo", ["arguments"] = new JsonObject { ["text"] = "hello" } });
        var output = JsonNode.Parse(McpHttpTransport.Process(McpTestRequests.Http(call, version), services).Body)!["result"]!;
        Assert.Equal("hello", output["content"]![0]!["text"]!.GetValue<string>());
    }

    [Fact]
    public void ModernHeadersCannotBeOmittedOrDisagreeWithBody()
    {
        var services = new McpServerServices(new RecordingTools());
        var message = McpTestRequests.Message("tools/call", args: new JsonObject { ["name"] = "echo" });
        foreach (var key in new[] { "MCP-Protocol-Version", "Mcp-Method", "Mcp-Name" })
        {
            var request = McpTestRequests.Http(message);
            var headers = new Dictionary<string, string>(request.Headers); headers.Remove(key);
            var response = McpHttpTransport.Process(request with { Headers = headers }, services);
            Assert.Equal(400, response.Status); Assert.Contains("-32020", response.Body);
            headers[key] = "wrong";
            response = McpHttpTransport.Process(request with { Headers = headers }, services);
            Assert.Equal(400, response.Status);
        }
    }

    [Fact]
    public void HeaderNamesAreCaseInsensitiveAndNamesSupportBase64Sentinels()
    {
        var message = McpTestRequests.Message("tools/call", args: new JsonObject { ["name"] = "世界" });
        var request = McpTestRequests.Http(message);
        var headers = request.Headers.ToDictionary(p => p.Key.ToLowerInvariant(), p => p.Value);
        headers["mcp-name"] = "=?base64?" + Convert.ToBase64String(Encoding.UTF8.GetBytes("世界")) + "?=";
        Assert.Equal(200, McpHttpTransport.Process(request with { Headers = headers }, new(new RecordingTools())).Status);
        headers["mcp-name"] = "=?base64?not-base64?=";
        Assert.Contains("-32020", McpHttpTransport.Process(request with { Headers = headers }, new(new RecordingTools())).Body);
    }

    [Theory]
    [InlineData("Origin", "https://evil.example")]
    [InlineData("Origin", "null")]
    [InlineData("Host", "evil.example:4010")]
    public void ForeignOriginsAndHostsAreRejectedBeforeDispatch(string key, string value)
    {
        var tools = new RecordingTools(); var request = McpTestRequests.Http(McpTestRequests.Message("tools/list"));
        var headers = new Dictionary<string, string>(request.Headers) { [key] = value };
        Assert.Equal(403, McpHttpTransport.Process(request with { Headers = headers }, new(tools)).Status);
        Assert.Equal(0, tools.Calls);
    }

    [Fact]
    public void HealthAndMethodsHaveSeparateTransportSemantics()
    {
        var request = McpTestRequests.Http(McpTestRequests.Message("ping"), McpRequestContext.Legacy);
        var services = new McpServerServices(new RecordingTools());
        Assert.Equal(200, McpHttpTransport.Process(request with { Method = "GET", Path = "/health", Body = "" }, services).Status);
        Assert.Equal(200, McpHttpTransport.Process(request with { Method = "GET", Headers = new Dictionary<string, string> { ["Host"] = "127.0.0.1:4010" } }, services).Status);
        Assert.Equal(405, McpHttpTransport.Process(request with { Method = "GET" }, services).Status);
        Assert.Equal(405, McpHttpTransport.Process(request with { Method = "DELETE" }, services).Status);
        Assert.Equal(404, McpHttpTransport.Process(request with { Path = "/other" }, services).Status);
    }

    [Fact]
    public void LegacyNotificationsAndBatchesContinueWorking()
    {
        var request = McpTestRequests.Http(McpTestRequests.Message("ping", McpRequestContext.Legacy), McpRequestContext.Legacy);
        var services = new McpServerServices(new RecordingTools());
        var notification = McpTestRequests.Message("notifications/initialized", McpRequestContext.Legacy); notification.Remove("id");
        Assert.Equal(202, McpHttpTransport.Process(request with { Body = notification.ToJsonString() }, services).Status);
        notification["method"] = "notifications/roots/list_changed";
        Assert.Equal(202, McpHttpTransport.Process(request with { Body = notification.ToJsonString() }, services).Status);
        Assert.Equal(200, McpHttpTransport.Process(request with { Body = "[" + request.Body + "]" }, services).Status);
        var modern = McpTestRequests.Http(McpTestRequests.Message("ping"));
        Assert.Equal(400, McpHttpTransport.Process(modern with { Body = "[" + modern.Body + "]" }, services).Status);
        Assert.Equal(400, McpHttpTransport.Process(request with { Body = "[]" }, services).Status);
    }

    [Theory]
    [InlineData("{", -32700)]
    [InlineData("null", -32600)]
    [InlineData("{}", -32600)]
    public void MalformedMessagesReturnProtocolErrors(string body, int code)
    {
        var request = McpTestRequests.Http(McpTestRequests.Message("ping", McpRequestContext.Legacy), McpRequestContext.Legacy);
        var response = McpHttpTransport.Process(request with { Body = body }, new(new RecordingTools()));
        Assert.Contains(code.ToString(), response.Body); Assert.Equal(400, response.Status);
    }

    [Fact]
    public void UnsupportedVersionsAndMethodsUseCurrentErrors()
    {
        var request = McpTestRequests.Http(McpTestRequests.Message("missing"));
        var services = new McpServerServices(new RecordingTools());
        var response = McpHttpTransport.Process(request, services);
        Assert.Equal(404, response.Status); Assert.Contains("-32601", response.Body);
        var headers = new Dictionary<string, string>(request.Headers) { ["MCP-Protocol-Version"] = "1900-01-01" };
        var message = JsonNode.Parse(request.Body)!; message["params"]!["_meta"]!["io.modelcontextprotocol/protocolVersion"] = "1900-01-01";
        response = McpHttpTransport.Process(request with { Headers = headers, Body = message.ToJsonString() }, services);
        Assert.Equal(400, response.Status); Assert.Contains("-32022", response.Body); Assert.Contains("supported", response.Body);
    }

    private sealed class MetadataTools : IMcpToolProvider
    {
        public object[] ListTools() => [];
        public object CallTool(JsonNode? args) => new JsonObject
        {
            ["content"] = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = "ok" }),
            ["_meta"] = new JsonObject { ["vendor/value"] = "unchanged" },
        };
    }
}
