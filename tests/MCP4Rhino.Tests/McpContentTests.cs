using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Logic;
using Xunit;

namespace MCP4Rhino.Tests;

public class McpContentTests
{
    // Minimal valid PNG: signature + IHDR chunk header bytes (length ≥ 33 for RequireValidPng).
    private static byte[] MinimalPng()
    {
        var png = new byte[33];
        png[0] = 0x89;
        png[1] = 0x50;
        png[2] = 0x4E;
        png[3] = 0x47;
        png[4] = 0x0D;
        png[5] = 0x0A;
        png[6] = 0x1A;
        png[7] = 0x0A;
        return png;
    }

    [Fact]
    public void TextOnly_Single_Text_Block()
    {
        var json = JsonSerializer.SerializeToNode(McpContent.TextOnly("hi"))!;
        Assert.Single(json["content"]!.AsArray());
        Assert.Equal("text", json["content"]![0]!["type"]!.GetValue<string>());
        Assert.Equal("hi", json["content"]![0]!["text"]!.GetValue<string>());
    }

    [Fact]
    public void TextAndPng_Puts_Text_First_Then_Image_Block()
    {
        var png = MinimalPng();
        var result = McpContent.TextAndPng("""{"path":"/tmp/v.png","width":1}""", png);
        var json = JsonSerializer.SerializeToNode(result)!;

        var content = json["content"]!.AsArray();
        Assert.Equal(2, content.Count);

        Assert.Equal("text", content[0]!["type"]!.GetValue<string>());
        Assert.Contains("path", content[0]!["text"]!.GetValue<string>());

        Assert.Equal("image", content[1]!["type"]!.GetValue<string>());
        Assert.Equal("image/png", content[1]!["mimeType"]!.GetValue<string>());
        Assert.Equal(Convert.ToBase64String(png), content[1]!["data"]!.GetValue<string>());
        Assert.False(json["isError"]!.GetValue<bool>());
    }

    [Fact]
    public void TextAndPng_Rejects_Empty()
    {
        Assert.Throws<ArgumentException>(() => McpContent.TextAndPng("{}", Array.Empty<byte>()));
        Assert.Throws<ArgumentException>(() => McpContent.TextAndPng("{}", null!));
        Assert.Throws<ArgumentException>(() => McpContent.TextAndPng("{}", new byte[8]));
    }

    [Fact]
    public void TextAndPng_Rejects_NonPng_Signature()
    {
        var junk = new byte[40];
        Assert.Throws<ArgumentException>(() => McpContent.TextAndPng("{}", junk));
    }

    [Fact]
    public void MaxEmbeddedPngBytes_Is_One_Mebibyte()
    {
        Assert.Equal(1_048_576, McpContent.MaxEmbeddedPngBytes);
    }

    [Fact]
    public void ToolsCall_Serializes_Image_Content_Through_JsonRpc()
    {
        var tools = new ImageFakeTools();
        var call = McpJsonRpc.Dispatch(JsonNode.Parse("""
            {"jsonrpc":"2.0","id":9,"method":"tools/call","params":{"name":"shot","arguments":{}}}
            """), tools);

        Assert.Null(call!["error"]);
        var result = call["result"]!;
        Assert.Equal("text", result["content"]![0]!["type"]!.GetValue<string>());
        Assert.Equal("image", result["content"]![1]!["type"]!.GetValue<string>());
        Assert.Equal("image/png", result["content"]![1]!["mimeType"]!.GetValue<string>());
        Assert.False(string.IsNullOrEmpty(result["content"]![1]!["data"]!.GetValue<string>()));
    }

    private sealed class ImageFakeTools : IMcpToolProvider
    {
        public object[] ListTools() =>
        [
            new { name = "shot", description = "shot", inputSchema = new { type = "object" } },
        ];

        public object CallTool(JsonNode? @params) =>
            McpContent.TextAndPng("{\"ok\":true}", MinimalPng());
    }
}
