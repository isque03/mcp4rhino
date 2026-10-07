using System.Text.Json.Nodes;
using MCP4Rhino.Logic;
using Xunit;

namespace MCP4Rhino.Tests;

public class JsonArgsTests
{
    [Fact]
    public void ParsePoint_ArrayAndObject()
    {
        var a = JsonArgs.ParsePoint(JsonNode.Parse("[1,2,3]"), "p");
        Assert.Equal(new Vec3(1, 2, 3), a);

        var b = JsonArgs.ParsePoint(JsonNode.Parse("""{"x":4,"y":5,"z":6}"""), "p");
        Assert.Equal(new Vec3(4, 5, 6), b);

        var c = JsonArgs.ParsePoint(JsonNode.Parse("[1,2]"), "p", defaultZ: 9);
        Assert.Equal(new Vec3(1, 2, 9), c);
    }

    [Fact]
    public void ParsePoints_NestedAndFlat()
    {
        var nested = JsonArgs.ParsePoints(JsonNode.Parse("[[0,0],[1,0],[1,1]]"), "pts");
        Assert.Equal(3, nested.Count);

        var flat3 = JsonArgs.ParsePoints(JsonNode.Parse("[0,0,0,1,0,0,1,1,0]"), "pts");
        Assert.Equal(3, flat3.Count);

        // length 4 → stride 2 (not multiple of 3)
        var flat2 = JsonArgs.ParsePoints(JsonNode.Parse("[0,0,1,0]"), "pts", defaultZ: 2);
        Assert.Equal(2, flat2[0].Z);
        Assert.Equal(2, flat2.Count);
    }

    [Fact]
    public void Unwrap_StringifiedJson()
    {
        var node = JsonValue.Create("[1,2,3]");
        var unwrapped = JsonArgs.Unwrap(node);
        Assert.IsType<JsonArray>(unwrapped);
    }

    [Fact]
    public void ParseIds_And_IdList()
    {
        var g1 = Guid.NewGuid();
        var g2 = Guid.NewGuid();
        var list = JsonArgs.ParseIds($"{g1},{g2}");
        Assert.Equal(2, list.Count);

        var fromArr = JsonArgs.IdList(JsonNode.Parse($"[\"{g1}\",\"{g2}\"]"));
        Assert.Equal(2, fromArr.Count);

        Assert.Throws<ArgumentException>(() => JsonArgs.ParseIds("not-a-guid"));
    }

    [Fact]
    public void ParseTags_KeyValueAndMap()
    {
        var args = JsonNode.Parse("""{"key":"a","value":"1","tags":{"b":"2"}}""")!.AsObject();
        var tags = JsonArgs.ParseTags(args);
        Assert.Equal("1", tags["a"]);
        Assert.Equal("2", tags["b"]);
    }

    [Fact]
    public void Bool_Num_Str_Edges()
    {
        var args = JsonNode.Parse("""{"on":"true","n":"3.5","s":"hi","flag":true}""")!.AsObject();
        Assert.True(JsonArgs.Bool(args, "on"));
        Assert.True(JsonArgs.Bool(args, "flag"));
        Assert.False(JsonArgs.Bool(args, "missing", false));
        Assert.Equal(3.5, JsonArgs.Num(args, "n"));
        Assert.Equal(0, JsonArgs.Num(args, "missing"));
        Assert.Equal("hi", JsonArgs.Str(args, "s"));
        Assert.Null(JsonArgs.Str(args, "missing"));
    }

    [Fact]
    public void ParseColor_And_TryParse()
    {
        var c = JsonArgs.ParseColor("#FF0080");
        Assert.Equal(255, c.R);
        Assert.Equal(0, c.G);
        Assert.Equal(128, c.B);
        Assert.Equal("#FF0080", JsonArgs.ColorHex(c));
        Assert.Null(JsonArgs.TryParseColor("nope"));
        Assert.Throws<ArgumentException>(() => JsonArgs.ParseColor("zz"));
    }

    [Fact]
    public void Tool_And_ToolC_Schemas()
    {
        var tool = (Dictionary<string, object>)JsonArgs.Tool("t", "d", ("ids*", "array", "ids"), ("opt", "any", "x"));
        Assert.Equal("t", tool["name"]);
        var schema = (Dictionary<string, object>)tool["inputSchema"];
        Assert.Contains("ids", (List<string>)schema["required"]);

        var toolC = (Dictionary<string, object>)JsonArgs.ToolC("tc", "d", ("x*", "number", "x"));
        var props = (Dictionary<string, object>)((Dictionary<string, object>)toolC["inputSchema"])["properties"];
        Assert.True(props.ContainsKey("layer"));
        Assert.True(props.ContainsKey("x"));
    }

    [Fact]
    public void ToDouble_RejectsNull()
    {
        Assert.Throws<ArgumentException>(() => JsonArgs.ToDouble(null));
        Assert.Throws<ArgumentException>(() => JsonArgs.ParsePoint(JsonNode.Parse("\"x\""), "p"));
        Assert.Throws<ArgumentException>(() => JsonArgs.ParsePoints(JsonNode.Parse("[]"), "p"));
        Assert.Throws<ArgumentException>(() => JsonArgs.IdList(null));
        Assert.Throws<ArgumentException>(() => JsonArgs.ParsePoints(JsonNode.Parse("[1]"), "p"));
    }
}
