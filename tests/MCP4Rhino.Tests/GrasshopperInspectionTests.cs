using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Logic;
using MCP4Rhino.Logic.Grasshopper;
using Xunit;

namespace MCP4Rhino.Tests;

public class GrasshopperInspectionTests
{
    private static JsonObject Args(string json) => JsonNode.Parse(json)!.AsObject();

    [Fact]
    public void ResultGuardsRejectChangedRevisionAndExpiredSolutions()
    {
        var identity = new ResultIdentity("s", "d", 4, "r");
        identity.Require(Args("""{"expected_revision":4,"result_id":"r"}"""), true);
        identity.Require(new JsonObject());
        Assert.Equal("revision_conflict", Assert.Throws<GhException>(() => identity.Require(
            Args("""{"expected_revision":3,"result_id":"r"}"""), true)).Code);
        Assert.Equal("stale_result", Assert.Throws<GhException>(() => identity.Require(
            Args("""{"result_id":"old"}"""), true)).Code);
        Assert.Throws<GhException>(() => new ResultIdentity("s", "d", 4, null).Require(
            Args("""{"result_id":"r"}"""), true));
        Assert.Throws<GhException>(() => identity.Require(new JsonObject(), true));
    }

    [Fact]
    public void OutputNamesMustBeUniqueAndSelectionsBounded()
    {
        var args = Args("""{"outputs":[{"name":"shape","node":"n","port":"out:0"}]}""");
        Assert.Equal(new NamedOutput("shape", "n", "out:0"), Assert.Single(NamedOutput.Parse(args)));
        args["outputs"]!.AsArray().Add(args["outputs"]![0]!.DeepClone());
        Assert.Throws<GhException>(() => NamedOutput.Parse(args));
        args["outputs"] = new JsonArray();
        Assert.Throws<GhException>(() => NamedOutput.Parse(args));
        args["outputs"] = new JsonArray(Enumerable.Range(0, 21).Select(i => (JsonNode)new JsonObject
            { ["name"] = i.ToString(), ["node"] = "n", ["port"] = "out:0" }).ToArray());
        Assert.Throws<GhException>(() => NamedOutput.Parse(args));
    }

    [Fact]
    public void ReportPaginationKeepsOrderAndReadsOnlyOneLookahead()
    {
        var reads = 0;
        IEnumerable<object> Rows()
        {
            for (var i = 0; i < 100; i++) { reads++; yield return new { index = i }; }
        }
        var page = JsonSerializer.SerializeToNode(SolutionReport.Page(Rows(), Args("""{"offset":2,"limit":3}""")))!;
        Assert.Equal(6, reads);
        Assert.Equal(2, page["rows"]![0]!["index"]!.GetValue<int>());
        Assert.Equal(5, page["next_offset"]!.GetValue<int>());
        var end = JsonSerializer.SerializeToNode(SolutionReport.Page(new object[] { 1, 2 }, new JsonObject()))!;
        Assert.Null(end["next_offset"]);
    }

    [Theory]
    [InlineData("{}", 1600, "current")]
    [InlineData("{\"width\":640,\"framing\":\"fit\"}", 640, "fit")]
    [InlineData("{\"framing\":\"rectangle\",\"rectangle\":[-20,40,400,100]}", 1600, "rectangle")]
    public void CaptureFramesAreExplicitAndFitWithoutCropping(string json, int width, string framing)
    {
        var frame = CanvasFrame.Parse(Args(json));
        Assert.Equal(width, frame.Width); Assert.Equal(framing, frame.Framing);
        Assert.Equal(Math.Min(width / 400d, frame.Height / 100d), frame.Zoom(new(-20, 40, 400, 100)));
        var visible = frame.VisibleRegion(new(-20, 40, 400, 100));
        Assert.True(visible.Width >= 400); Assert.True(visible.Height >= 100);
        Assert.Equal(180, visible.X + visible.Width / 2, 6);
        Assert.Equal(90, visible.Y + visible.Height / 2, 6);
    }

    [Theory]
    [InlineData("{\"width\":63}")]
    [InlineData("{\"height\":4097}")]
    [InlineData("{\"framing\":\"all\"}")]
    [InlineData("{\"framing\":\"rectangle\"}")]
    [InlineData("{\"framing\":\"rectangle\",\"rectangle\":[0,0,0,100]}")]
    [InlineData("{\"framing\":\"rectangle\",\"rectangle\":[0,0,100]}")]
    [InlineData("{\"rectangle\":[0,0,100,100]}")]
    [InlineData("{\"framing\":\"rectangle\",\"rectangle\":[1e8,0,100,100]}")]
    public void InvalidFramesFailBeforeRendering(string json) =>
        Assert.Throws<GhException>(() => CanvasFrame.Parse(Args(json)));

    [Fact]
    public void TinyOrNonfiniteRegionsCannotOverflowNativeProjection()
    {
        var frame = CanvasFrame.Parse(new JsonObject());
        Assert.Throws<GhException>(() => frame.Zoom(new(0, 0, 1e-300, 1e-300)));
        Assert.Throws<GhException>(() => frame.Zoom(new(double.NaN, 0, 100, 100)));
    }

    [Fact]
    public void CapturesRetainTextFirstAndRespectImageLimit()
    {
        var png = new byte[33]; png[0] = 0x89; png[1] = 0x50; png[2] = 0x4e; png[3] = 0x47;
        var metadata = Args("""{"path":"canvas.png"}""");
        var small = JsonSerializer.SerializeToNode(CaptureContent.FromPng(metadata, png))!;
        Assert.Equal("image", small["content"]![1]!["type"]!.GetValue<string>());
        Assert.False(metadata.ContainsKey("image_embedded"));
        Array.Resize(ref png, McpContent.MaxEmbeddedPngBytes + 1);
        var large = JsonSerializer.SerializeToNode(CaptureContent.FromPng(metadata, png))!;
        Assert.Single(large["content"]!.AsArray());
        var text = JsonNode.Parse(large["content"]![0]!["text"]!.GetValue<string>())!;
        Assert.Equal("exceeds_max_bytes", text["image_omitted_reason"]!.GetValue<string>());
        Assert.Throws<ArgumentException>(() => CaptureContent.FromPng(metadata, new byte[33]));
    }

    [Fact]
    public void ViewportAssociationPreservesImagesAndDoesNotRelabelErrors()
    {
        var identity = new ResultIdentity("s", "d", 4, "r");
        var associated = JsonSerializer.SerializeToNode(CaptureContent.Associate(McpContent.TextOnly("{\"path\":\"v.png\"}"), identity))!;
        var metadata = JsonNode.Parse(associated["content"]![0]!["text"]!.GetValue<string>())!;
        Assert.Equal("r", metadata["grasshopper"]!["result_id"]!.GetValue<string>());
        var error = new { isError = true, content = new[] { new { type = "text", text = "failure" } } };
        var unchanged = JsonSerializer.SerializeToNode(CaptureContent.Associate(error, identity))!;
        Assert.Equal("failure", unchanged["content"]![0]!["text"]!.GetValue<string>());
        GhSchema.Validate(CaptureContent.ContextSchema, Args("""{"session_id":"s","expected_revision":4,"result_id":"r"}"""));
        Assert.Throws<GhException>(() => GhSchema.Validate(CaptureContent.ContextSchema, Args("""{"session_id":"s"}""")));
    }

    [Fact]
    public void NewToolContractsRejectMissingGuardsAndUnknownOptions()
    {
        var summary = Args("""{"session_id":"s","expected_revision":0,"result_id":"r","outputs":[{"name":"value","node":"n","port":"out:0"}]}""");
        GhToolCatalog.Validate("gh_get_solution_summary", summary);
        summary.Remove("result_id");
        Assert.Throws<GhException>(() => GhToolCatalog.Validate("gh_get_solution_summary", summary));
        GhToolCatalog.Validate("gh_capture_canvas", Args("""{"session_id":"s","framing":"fit"}"""));
        Assert.Throws<GhException>(() => GhToolCatalog.Validate("gh_capture_canvas", Args("""{"session_id":"s","width":8192}""")));
        Assert.Throws<GhException>(() => GhToolCatalog.Validate("gh_show_document", Args("""{"session_id":"s"}""")));
    }
}
