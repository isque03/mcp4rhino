using System.Text.Json.Nodes;
using MCP4Rhino.Logic;
using Xunit;

namespace MCP4Rhino.Tests;

public class McpJsonRpcTests
{
    private sealed class FakeTools : IMcpToolProvider
    {
        public object[] ListTools() =>
        [
            new { name = "echo", description = "echo", inputSchema = new { type = "object" } },
        ];

        public object CallTool(JsonNode? @params)
        {
            var name = @params?["name"]?.GetValue<string>();
            if (name != "echo") throw new InvalidOperationException($"unknown tool {name}");
            var args = @params?["arguments"] as JsonObject ?? new JsonObject();
            return new { content = new[] { new { type = "text", text = args["msg"]?.ToString() ?? "" } }, isError = false };
        }
    }

    [Fact]
    public void Initialize_And_Ping()
    {
        var init = McpJsonRpc.Dispatch(JsonNode.Parse("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}"""), new FakeTools());
        Assert.NotNull(init);
        Assert.Contains(McpJsonRpc.ProtocolVersion, init!.ToJsonString());
        Assert.Contains(McpJsonRpc.ServerName, init.ToJsonString());

        var ping = McpJsonRpc.Dispatch(JsonNode.Parse("""{"jsonrpc":"2.0","id":2,"method":"ping"}"""), new FakeTools());
        Assert.NotNull(ping?["result"]);
    }

    [Fact]
    public void ToolsList_And_Call_Success()
    {
        var tools = new FakeTools();
        var list = McpJsonRpc.Dispatch(JsonNode.Parse("""{"jsonrpc":"2.0","id":3,"method":"tools/list","params":{}}"""), tools);
        Assert.Contains("echo", list!.ToJsonString());
        Assert.DoesNotContain("mcp4rhino_reload", list.ToJsonString());

        var call = McpJsonRpc.Dispatch(JsonNode.Parse("""
            {"jsonrpc":"2.0","id":4,"method":"tools/call","params":{"name":"echo","arguments":{"msg":"hi"}}}
            """), tools);
        Assert.Contains("hi", call!.ToJsonString());
        Assert.Null(call["error"]);
    }

    [Fact]
    public void ToolsCall_Error_And_BadMethod()
    {
        var err = McpJsonRpc.Dispatch(JsonNode.Parse("""
            {"jsonrpc":"2.0","id":5,"method":"tools/call","params":{"name":"missing","arguments":{}}}
            """), new FakeTools());
        Assert.NotNull(err?["error"]);

        var bad = McpJsonRpc.Dispatch(JsonNode.Parse("""{"jsonrpc":"2.0","id":6,"method":"nope"}"""), new FakeTools());
        Assert.Contains("Method not found", bad!["error"]!["message"]!.GetValue<string>());
    }

    [Fact]
    public void Notification_Returns_Null()
    {
        var n = McpJsonRpc.Dispatch(JsonNode.Parse("""{"jsonrpc":"2.0","method":"notifications/initialized"}"""), new FakeTools());
        Assert.Null(n);
    }

    [Fact]
    public void ParseError_And_HotReload_Tool()
    {
        var pe = McpJsonRpc.ParseError(1, "Parse error");
        Assert.Contains("Parse error", System.Text.Json.JsonSerializer.Serialize(pe));

        var reloaded = false;
        var list = McpJsonRpc.Dispatch(
            JsonNode.Parse("""{"jsonrpc":"2.0","id":7,"method":"tools/list","params":{}}"""),
            new FakeTools(),
            _ => { reloaded = true; return new { ok = true }; });
        Assert.Contains("mcp4rhino_reload", list!.ToJsonString());

        var call = McpJsonRpc.Dispatch(
            JsonNode.Parse("""{"jsonrpc":"2.0","id":8,"method":"tools/call","params":{"name":"mcp4rhino_reload","arguments":{}}}"""),
            new FakeTools(),
            _ => { reloaded = true; return new { ok = true }; });
        Assert.True(reloaded);
        Assert.Contains("ok", call!.ToJsonString());
    }

    [Fact]
    public void Null_Request()
    {
        Assert.Null(McpJsonRpc.Dispatch(null, new FakeTools()));
    }
}
