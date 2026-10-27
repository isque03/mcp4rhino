using System.Text.Json.Nodes;
using MCP4Rhino.Logic.Protocol;
using Xunit;

namespace MCP4Rhino.Tests;

public class McpProtocolValidationTests
{
    [Fact]
    public void MetadataNeverSilentlyDowngradesToLegacy()
    {
        var message = McpTestRequests.Message("ping");
        var meta = message["params"]!["_meta"]!.AsObject();
        meta["io.modelcontextprotocol/protocolVersion"] = 12;
        Assert.Throws<McpRpcException>(() => McpRequestContext.From(message));
        meta["io.modelcontextprotocol/protocolVersion"] = McpRequestContext.Modern;
        meta.Remove("io.modelcontextprotocol/clientCapabilities");
        Assert.Throws<McpRpcException>(() => McpRequestContext.From(message));
        meta["io.modelcontextprotocol/clientCapabilities"] = new JsonObject();
        meta["io.modelcontextprotocol/clientInfo"] = new JsonObject { ["name"] = "x" };
        Assert.Throws<McpRpcException>(() => McpRequestContext.From(message));
        meta.Remove("io.modelcontextprotocol/clientInfo");
        Assert.True(McpRequestContext.From(message).IsModern);
    }

    [Fact]
    public void LegacyNegotiationDoesNotClaimAnUnsupportedVersion()
    {
        var request = McpTestRequests.Message("initialize", McpRequestContext.Legacy, new() { ["protocolVersion"] = "unknown" });
        Assert.Equal(McpRequestContext.Streamable, McpRequestContext.From(request).Version);
        request["params"]!["protocolVersion"] = McpRequestContext.Legacy;
        Assert.Equal(-32020, Assert.Throws<McpRpcException>(() => McpRequestContext.From(request, McpRequestContext.Streamable)).Code);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{\"jsonrpc\":\"1.0\",\"id\":1,\"method\":\"ping\"}")]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":null,\"method\":\"ping\"}")]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":{},\"method\":\"ping\"}")]
    [InlineData("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"ping\",\"params\":[]}")]
    public void InvalidEnvelopesAreRejected(string json)
    {
        var response = McpDispatcher.Dispatch(JsonNode.Parse(json), new(new RecordingTools()), new(McpRequestContext.Legacy, new()));
        Assert.Equal(-32600, response!["error"]!["code"]!.GetValue<int>());
        if (json.Contains("\"id\":{}")) Assert.Null(response["id"]);
    }

    [Fact]
    public void UnsupportedNotificationsAndOversizedBodiesAreExplicit()
    {
        var message = McpTestRequests.Message("notifications/unknown"); message.Remove("id");
        var result = McpDispatcher.Dispatch(message, new(new RecordingTools()), McpRequestContext.From(message));
        Assert.Equal(-32601, result!["error"]!["code"]!.GetValue<int>());
        var request = McpTestRequests.Http(McpTestRequests.Message("ping"));
        Assert.Equal(413, McpHttpTransport.Process(request with { Body = new string('x', McpHttpTransport.MaxRequestBytes + 1) }, new(new RecordingTools())).Status);
    }

    [Fact]
    public void UnsupportedMediaTypesFailBeforeToolExecution()
    {
        var tools = new RecordingTools(); var request = McpTestRequests.Http(McpTestRequests.Message("tools/list"));
        foreach (var (key, value) in new[] { ("Content-Type", "text/plain"), ("Accept", "application/json"), ("Accept", "application/json, text/event-stream;q=0") })
        {
            var headers = new Dictionary<string, string>(request.Headers) { [key] = value };
            Assert.Equal(400, McpHttpTransport.Process(request with { Headers = headers }, new(tools)).Status);
        }
        Assert.Equal(0, tools.Calls);
    }
}
