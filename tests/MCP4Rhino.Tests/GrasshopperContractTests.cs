using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Logic.Grasshopper;
using Xunit;

namespace MCP4Rhino.Tests;

public class GrasshopperContractTests
{
    private static JsonObject Args(string json) => JsonNode.Parse(json)!.AsObject();

    [Fact]
    public void TreesPreserveSparseMixedDepthEmptyAndNullBranches()
    {
        var tree = JsonNode.Parse("""[{"path":[0],"items":[1,null]},{"path":[3,0],"items":[]},{"path":[7],"items":[4]}]""");
        var branches = DataTree.Parse(tree);
        Assert.Equal(new[] { 3, 0 }, branches[1].Path);
        Assert.Empty(branches[1].Items); Assert.Null(branches[0].Items[1]);
        tree![0]!["items"]![0] = 99;
        Assert.Equal(1, branches[0].Items[0]!.GetValue<int>());
    }

    [Theory]
    [InlineData("[{\"path\":[],\"items\":[]}]", "invalid_tree")]
    [InlineData("[{\"path\":[-1],\"items\":[]}]", "invalid_tree")]
    [InlineData("[{\"path\":[0],\"items\":[]},{\"path\":[0],\"items\":[]}]", "invalid_tree")]
    [InlineData("{}", "invalid_arguments")]
    public void InvalidTreesFailBeforeAnySdkAccess(string json, string code) =>
        Assert.Equal(code, Assert.Throws<GhException>(() => DataTree.Parse(JsonNode.Parse(json))).Code);

    [Fact]
    public void TreeLimitsBoundInputMemory()
    {
        var items = new JsonArray(Enumerable.Range(0, 10001).Select(i => JsonValue.Create(i)).ToArray());
        Assert.Equal("limit_exceeded", Assert.Throws<GhException>(() => DataTree.Parse(new JsonArray(
            new JsonObject { ["path"] = new JsonArray(0), ["items"] = items }))).Code);
        var branches = new JsonArray(Enumerable.Range(0, 2001).Select(i => (JsonNode)new JsonObject
        { ["path"] = new JsonArray(i), ["items"] = new JsonArray() }).ToArray());
        Assert.Throws<GhException>(() => DataTree.Parse(branches));
    }

    [Fact]
    public void GraphValidationAcceptsDiamondAndRejectsMissingNodesAndCycles()
    {
        GraphRules.RequireAcyclic(new[] { "a", "b", "c", "d" }, new[]
        { new GraphEdge("a","b"), new GraphEdge("a","c"), new GraphEdge("c","d"), new GraphEdge("b","d"), new GraphEdge("a","b") });
        Assert.Equal("cyclic_graph", Assert.Throws<GhException>(() => GraphRules.RequireAcyclic(
            new[] { "a", "b" }, new[] { new GraphEdge("a", "b"), new GraphEdge("b", "a") })).Code);
        Assert.Throws<GhException>(() => GraphRules.RequireAcyclic(new[] { "a" }, new[] { new GraphEdge("a", "a") }));
        Assert.Equal("invalid_connection", Assert.Throws<GhException>(() => GraphRules.RequireAcyclic(
            new[] { "a" }, new[] { new GraphEdge("a", "missing") })).Code);
    }

    [Fact]
    public void LayoutFollowsDependenciesAndSeparatesParallelNodes()
    {
        var layout = GraphRules.Layout(new[] { "a", "b", "c", "d" }, new[]
        { new GraphEdge("a","b"), new GraphEdge("a","c"), new GraphEdge("b","d"), new GraphEdge("c","d") });
        Assert.True(layout["a"].X < layout["b"].X);
        Assert.Equal(layout["b"].X, layout["c"].X);
        Assert.NotEqual(layout["b"].Y, layout["c"].Y);
        Assert.True(layout["c"].X < layout["d"].X);
    }

    [Fact]
    public void MutationRetriesReturnOriginalReceiptAndRejectDifferentIntent()
    {
        var ledger = new RequestLedger(); var calls = 0;
        var args = Args("""{"request_id":"bake-1","session_id":"s","expected_revision":1}""");
        Assert.Equal("receipt", ledger.Execute("bake", args, () => { calls++; return "receipt"; }));
        Assert.Equal("receipt", ledger.Execute("bake", (JsonObject)args.DeepClone(), () => throw new Exception()));
        Assert.Equal(1, calls);
        args["expected_revision"] = 2;
        Assert.Equal("request_conflict", Assert.Throws<GhException>(() => ledger.Execute("bake", args, () => "wrong")).Code);
        Assert.Equal("other-tool", ledger.Execute("save", args, () => "other-tool"));
    }

    [Fact]
    public void FailedMutationCanBeRetriedAndReceiptCapacityFailsClosed()
    {
        var ledger = new RequestLedger(); var args = Args("""{"request_id":"failed"}""");
        Assert.Throws<IOException>(() => ledger.Execute("patch", args, () => throw new IOException()));
        Assert.Equal("fixed", ledger.Execute("patch", args, () => "fixed"));
        for (var i = 0; i < 9999; i++) ledger.Execute("patch", new JsonObject { ["request_id"] = i.ToString() }, () => "ok");
        Assert.Equal("limit_exceeded", Assert.Throws<GhException>(() => ledger.Execute("patch",
            new JsonObject { ["request_id"] = "over-limit" }, () => "bad")).Code);
    }

    [Fact]
    public void UnitIndependentArgumentsRequireFiniteValuesAndExactRevisions()
    {
        Assert.Equal(2.5, GhJson.Number(JsonNode.Parse("2.5")));
        Assert.Throws<GhException>(() => GhJson.Number(null));
        Assert.Throws<GhException>(() => GhJson.Number(JsonValue.Create(double.NaN)));
        Assert.Throws<GhException>(() => GhJson.Text(new JsonObject(), "session_id"));
        Assert.Throws<GhException>(() => GhJson.Object(new JsonArray()));
        GhJson.ExpectRevision(7, Args("""{"expected_revision":7}"""));
        Assert.Equal("revision_conflict", Assert.Throws<GhException>(() => GhJson.ExpectRevision(8, Args("""{"expected_revision":7}"""))).Code);
        Assert.Throws<GhException>(() => GhJson.ExpectRevision(7, new JsonObject()));
        Assert.Equal(200, GhJson.PageSize(Args("""{"limit":900}""")));
        Assert.Equal(0, GhJson.Offset(Args("""{"offset":-1}""")));
        Assert.True(GhJson.Flag(Args("""{"solve":true}"""), "solve"));
        Assert.False(GhJson.Flag(new JsonObject(), "solve"));
        Assert.Equal(50, GhJson.PageSize(new JsonObject()));
        Assert.EndsWith(".ghx", GhJson.DefinitionPath(Args("""{"path":"study.ghx"}""")));
        Assert.Throws<GhException>(() => GhJson.DefinitionPath(Args("""{"path":"script.cs"}""")));
    }

    [Theory]
    [InlineData("Grasshopper", "Grasshopper.Kernel.Special.GH_NumberSlider", false, true)]
    [InlineData("Grasshopper", "Grasshopper.Kernel.Special.GH_NumberSlider", true, false)]
    [InlineData("ScriptComponents", "ScriptComponents.Component_CSharp", false, false)]
    [InlineData("ThirdParty", "Grasshopper.Kernel.Special.GH_NumberSlider", false, false)]
    public void ManifestDoesNotGrantExecutionByDisplayName(string assembly, string type, bool obsolete, bool expected) =>
        Assert.Equal(expected, ComponentSupport.Allows(assembly, type, obsolete));

    [Fact]
    public void SchemasRejectUnknownOptionsMissingArgumentsAndInvalidOperations()
    {
        GhToolCatalog.Validate("gh_help", Args("""{"topic":"index"}"""));
        Assert.True(GhToolCatalog.Contains("gh_solve")); Assert.False(GhToolCatalog.Contains("not_a_tool"));
        Assert.Throws<GhException>(() => GhToolCatalog.Validate("not_a_tool", new JsonObject()));
        Assert.Throws<GhException>(() => GhToolCatalog.Validate("gh_solve", new JsonObject()));
        Assert.Throws<GhException>(() => GhToolCatalog.Validate("gh_status", Args("""{"load":true}""")));
        Assert.Throws<GhException>(() => GhToolCatalog.Validate("gh_open_document", Args("""{"mode":"run-code","request_id":"1"}""")));
        var patch = Args("""{"session_id":"s","expected_revision":0,"request_id":"1","operations":[{"op":"remove","node":"n"}]}""");
        GhToolCatalog.Validate("gh_apply_patch", patch);
        patch["operations"]![0]!["code"] = "print('wrong')";
        Assert.Throws<GhException>(() => GhToolCatalog.Validate("gh_apply_patch", patch));
    }

    [Theory]
    [InlineData("{\"type\":\"array\",\"minItems\":1}", "[]")]
    [InlineData("{\"type\":\"array\",\"maxItems\":1}", "[1,2]")]
    [InlineData("{\"type\":\"integer\"}", "1.5")]
    [InlineData("{\"type\":\"boolean\"}", "\"true\"")]
    [InlineData("{\"type\":\"string\"}", "null")]
    [InlineData("{\"type\":\"number\",\"minimum\":1}", "0")]
    [InlineData("{\"type\":\"number\",\"maximum\":1}", "2")]
    public void SchemaFailuresAreActionable(string schema, string value) =>
        Assert.Equal("invalid_arguments", Assert.Throws<GhException>(() => GhSchema.Validate(Args(schema), JsonNode.Parse(value))).Code);
}
