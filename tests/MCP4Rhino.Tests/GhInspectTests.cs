using System.Text.Json.Nodes;
using MCP4Rhino.Logic.Grasshopper;
using Xunit;
using Xunit.Abstractions;

namespace MCP4Rhino.Tests;

public class GhInspectTests(ITestOutputHelper output)
{
    private static JsonObject Args(string json) => JsonNode.Parse(json)!.AsObject();
    private static readonly JsonNode Session = Args("""{"gh_session":"s","revision":3}""");

    private static (GhNodeInfo[] Nodes, GhGroupInfo[] Groups) Graph(int count = 200)
    {
        var nodes = Enumerable.Range(0, count).Select(i =>
        {
            var kind = i % 10 == 0 ? "GH_NumberSlider" : i % 10 == 1 ? "GH_Panel" : "GH_Component";
            var name = kind == "GH_NumberSlider" ? "Number Slider" : kind == "GH_Panel" ? "Panel" : "Move " + (i % 7);
            JsonObject? control = kind == "GH_NumberSlider" ? new JsonObject { ["kind"] = "slider", ["value"] = i, ["minimum"] = 0, ["maximum"] = 1000 } :
                kind == "GH_Panel" ? new JsonObject { ["kind"] = "panel", ["text"] = "text " + i } : null;
            var errors = i % 25 == 3 ? new[] { new string('e', 400) } : [];
            var warnings = i % 40 == 5 ? new[] { "Input mismatch" } : [];
            return new GhNodeInfo(Guid.NewGuid().ToString(), name, "n" + i, kind, i * 10.55, i % 17 * 8.2,
                i < 60 ? ["Studs"] : i < 90 ? ["Plates", "Outer"] : [], errors, warnings, 3, 2, control);
        }).ToArray();
        var groups = new[]
        {
            new GhGroupInfo(Guid.NewGuid().ToString(), "Studs", nodes.Take(60).Select(n => n.Id).ToArray()),
            new GhGroupInfo(Guid.NewGuid().ToString(), "Plates", nodes.Skip(60).Take(30).Select(n => n.Id).ToArray()),
        };
        return (nodes, groups);
    }

    private static JsonObject Page(JsonObject args, GhNodeInfo[] nodes, GhGroupInfo[] groups, Func<GhNodeInfo, JsonNode>? describe = null)
    {
        var options = GhInspectOptions.Parse(args);
        var matches = GhInspect.Filter(nodes, groups, options);
        return GhInspect.Page(Session, matches, nodes.Length, options, describe ?? (n => new JsonObject { ["node"] = n.Id }));
    }

    private static JsonNode HugeDetail(GhNodeInfo n, int ports = 4)
    {
        var o = new JsonObject { ["node"] = n.Id, ["name"] = n.Name, ["settings"] = null };
        JsonArray Ports(string side) => new(Enumerable.Range(0, ports).Select(p => (JsonNode)new JsonObject
        {
            ["port"] = side + p, ["description"] = new string('d', 2000),
            ["sources"] = new JsonArray(Enumerable.Range(0, 30).Select(s => (JsonNode)new JsonObject { ["node"] = "x" }).ToArray()),
            ["persistent"] = new JsonObject { ["item_count"] = 100, ["sample"] = new JsonArray(
                Enumerable.Range(0, 10).Select(s => (JsonNode)new JsonObject { ["value"] = new string('v', 900) }).ToArray()) },
        }).ToArray());
        o["inputs"] = Ports("in:"); o["outputs"] = Ports("out:");
        return o;
    }

    [Fact]
    public void OverviewOfLargeGraphIsSmallAndHasNoPerNodePorts()
    {
        var (nodes, groups) = Graph();
        var overview = GhInspect.Overview(Session, nodes, groups);
        var size = GhInspect.Size(overview);
        output.WriteLine($"overview chars for 200 nodes: {size}");
        Assert.True(size < GhInspect.MaxResponseChars);
        Assert.Equal(200, overview["total"]!.GetValue<int>());
        Assert.Equal(2, overview["groups"]!.AsArray().Count);
        Assert.Equal(60, overview["groups"]![0]!["members"]!.GetValue<int>());
        Assert.Equal(40, overview["inputs_total"]!.GetValue<int>());
        Assert.Equal(20, overview["counts_by_type"]!["GH_NumberSlider"]!.GetValue<int>());
        Assert.True(overview["diagnostic_nodes_total"]!.GetValue<int>() > 0);
        Assert.DoesNotContain("\"ports\"", overview.ToJsonString());
        Assert.False(overview["truncated"]!.GetValue<bool>());
        Assert.NotNull(overview["next"]);
    }

    [Fact]
    public void OverviewStaysBoundedWhenEverythingIsUnique()
    {
        var nodes = Enumerable.Range(0, 3000).Select(i => new GhNodeInfo(Guid.NewGuid().ToString(), "Name " + i, new string('n', 50),
            "T" + i, 0, 0, [], [new string('x', 500)], [], 1, 1, new JsonObject { ["kind"] = "panel", ["text"] = new string('t', 100) })).ToArray();
        var groups = Enumerable.Range(0, 500).Select(i => new GhGroupInfo("g" + i, "Group " + i, ["a"])).ToArray();
        var overview = GhInspect.Overview(Session, nodes, groups);
        var size = GhInspect.Size(overview);
        output.WriteLine($"overview chars for 3000 unique nodes: {size}");
        Assert.True(size <= GhInspect.MaxResponseChars);
        Assert.True(overview["truncated"]!.GetValue<bool>());
        Assert.Equal(3000, overview["total"]!.GetValue<int>());
    }

    [Fact]
    public void CompactPageIsSmallPerNodeAndPages()
    {
        var (nodes, groups) = Graph();
        var page = Page(Args("""{"gh_session":"s","limit":200}"""), nodes, groups);
        var size = GhInspect.Size(page);
        output.WriteLine($"compact page chars for 200 nodes: {size} ({size / 200} per node)");
        Assert.True(size < GhInspect.MaxResponseChars);
        Assert.Equal("compact", page["mode"]!.GetValue<string>());
        Assert.Equal(200, page["nodes"]!.AsArray().Count);
        Assert.False(page["truncated"]!.GetValue<bool>());
        Assert.Null(page["next_offset"]);
        var row = page["nodes"]![5]!.AsObject();
        foreach (var key in new[] { "node", "name", "nickname", "type", "position", "groups", "errors", "warnings", "inputs", "outputs" })
            Assert.True(row.ContainsKey(key), key);

        var first = Page(Args("""{"gh_session":"s","limit":50}"""), nodes, groups);
        Assert.Equal(50, first["next_offset"]!.GetValue<int>());
        Assert.Equal(50, first["next"]!["offset"]!.GetValue<int>());
        var second = Page(Args("""{"gh_session":"s","offset":50,"limit":50}"""), nodes, groups);
        Assert.Equal(nodes[50].Id, second["nodes"]![0]!["node"]!.GetValue<string>());
    }

    [Fact]
    public void DefaultModeIsOverviewWithoutPagingOrFiltersAndCompactOtherwise()
    {
        Assert.Equal(GhInspectMode.Overview, GhInspectOptions.Parse(Args("""{"gh_session":"s"}""")).Mode);
        Assert.Equal(GhInspectMode.Compact, GhInspectOptions.Parse(Args("""{"gh_session":"s","offset":0}""")).Mode);
        Assert.Equal(GhInspectMode.Compact, GhInspectOptions.Parse(Args("""{"gh_session":"s","limit":50}""")).Mode);
        Assert.Equal(GhInspectMode.Compact, GhInspectOptions.Parse(Args("""{"gh_session":"s","group":"x"}""")).Mode);
        Assert.Equal(GhInspectMode.Detail, GhInspectOptions.Parse(Args("""{"gh_session":"s","mode":"detail"}""")).Mode);
        Assert.Equal("invalid_arguments", Assert.Throws<GhException>(() =>
            GhInspectOptions.Parse(Args("""{"gh_session":"s","mode":"all"}"""))).Code);
    }

    [Fact]
    public void FiltersReturnOnlyMatchingNodes()
    {
        var (nodes, groups) = Graph();
        var ids = Page(Args($$"""{"gh_session":"s","nodes":["{{nodes[7].Id}}","{{nodes[9].Id.ToUpperInvariant()}}","missing"]}"""), nodes, groups);
        Assert.Equal(new[] { nodes[7].Id, nodes[9].Id }, ids["nodes"]!.AsArray().Select(n => n!["node"]!.GetValue<string>()));

        var byGroup = Page(Args("""{"gh_session":"s","group":"plates","limit":200}"""), nodes, groups);
        Assert.Equal(30, byGroup["matched"]!.GetValue<int>());
        Assert.All(byGroup["nodes"]!.AsArray(), n => Assert.Contains("Plates", n!["groups"]!.AsArray().Select(g => g!.GetValue<string>())));

        var byName = Page(Args("""{"gh_session":"s","match":"slider","limit":200}"""), nodes, groups);
        Assert.Equal(20, byName["matched"]!.GetValue<int>());
        var byNick = Page(Args("""{"gh_session":"s","match":"N17","limit":200}"""), nodes, groups);
        Assert.Contains(byNick["nodes"]!.AsArray(), n => n!["nickname"]!.GetValue<string>() == "n17");
        var byType = Page(Args("""{"gh_session":"s","match":"gh_panel","limit":200}"""), nodes, groups);
        Assert.Equal(20, byType["matched"]!.GetValue<int>());

        var diagnostics = Page(Args("""{"gh_session":"s","has_diagnostics":true,"limit":200}"""), nodes, groups);
        Assert.Equal(nodes.Count(n => n.HasDiagnostics), diagnostics["matched"]!.GetValue<int>());
        Assert.All(diagnostics["nodes"]!.AsArray(), n => Assert.True(n!["errors"]!.GetValue<int>() + n["warnings"]!.GetValue<int>() > 0));

        var combined = Page(Args("""{"gh_session":"s","group":"Studs","match":"slider","limit":200}"""), nodes, groups);
        Assert.Equal(6, combined["matched"]!.GetValue<int>());
    }

    [Fact]
    public void DetailPageIsTruncatedByBudgetAndPointsToNextCall()
    {
        var (nodes, groups) = Graph();
        var page = Page(Args("""{"gh_session":"s","mode":"detail","limit":200,"group":"Studs"}"""), nodes, groups, n => HugeDetail(n));
        var size = GhInspect.Size(page);
        output.WriteLine($"detail page chars: {size}, rows {page["nodes"]!.AsArray().Count} of 60");
        Assert.True(size <= GhInspect.MaxResponseChars);
        Assert.True(page["truncated"]!.GetValue<bool>());
        var next = page["next"]!.AsObject();
        Assert.Equal(page["nodes"]!.AsArray().Count, next["offset"]!.GetValue<int>());
        Assert.Equal("detail", next["mode"]!.GetValue<string>());
        Assert.Equal("Studs", next["group"]!.GetValue<string>());
        Assert.Equal(page["next_offset"]!.GetValue<int>(), next["offset"]!.GetValue<int>());
    }

    [Fact]
    public void DetailCapsPortsSamplesSourcesAndStrings()
    {
        var (nodes, _) = Graph(1);
        var full = new JsonObject { ["node"] = nodes[0].Id, ["outputs"] = new JsonArray() };
        var inputs = new JsonArray(HugeDetail(nodes[0], 1)["inputs"]![0]!.DeepClone());
        for (var i = 0; i < 85; i++) inputs.Add(new JsonObject { ["port"] = "in:" + (i + 1) });
        full["inputs"] = inputs;
        var page = GhInspect.Page(Session, nodes, 1, GhInspectOptions.Parse(Args("""{"gh_session":"s","mode":"detail"}""")), _ => full);
        var node = page["nodes"]![0]!;
        Assert.Equal(GhInspect.MaxPorts, node["inputs"]!.AsArray().Count);
        Assert.Equal(86, node["inputs_total"]!.GetValue<int>());
        var port = node["inputs"]![0]!;
        Assert.Equal(GhInspect.MaxSources, port["sources"]!.AsArray().Count);
        Assert.Equal(30, port["sources_total"]!.GetValue<int>());
        Assert.Equal(GhInspect.MaxSamples, port["persistent"]!["sample"]!.AsArray().Count);
        Assert.True(port["persistent"]!["sample_truncated"]!.GetValue<bool>());
        Assert.Contains("[truncated", port["description"]!.GetValue<string>());
        Assert.True(GhInspect.Size(page) <= GhInspect.MaxResponseChars);
    }

    [Fact]
    public void OneOversizedNodeBecomesAMarkedStub()
    {
        var (nodes, _) = Graph(1);
        var huge = new JsonObject { ["node"] = nodes[0].Id };
        huge["inputs"] = new JsonArray(Enumerable.Range(0, 50).Select(i => (JsonNode)new JsonObject
        { ["port"] = "in:" + i, ["sources"] = new JsonArray(), ["persistent"] = new JsonObject { ["sample"] = new JsonArray(
            Enumerable.Range(0, 5).Select(s => (JsonNode)new JsonObject { ["value"] = new string('v', 290), ["a"] = new string('a', 290), ["b"] = new string('b', 290) }).ToArray()) } }).ToArray());
        var page = GhInspect.Page(Session, nodes, 1, GhInspectOptions.Parse(Args("""{"gh_session":"s","mode":"detail"}""")), _ => huge);
        Assert.True(page["nodes"]![0]!["detail_truncated"]!.GetValue<bool>());
        Assert.True(GhInspect.Size(page) <= GhInspect.MaxResponseChars);
    }

    [Fact]
    public void LegacyArgumentsStillValidateAndSchemaStaysStrict()
    {
        GhToolCatalog.Validate("gh_inspect_document", Args("""{"gh_session":"s"}"""));
        GhToolCatalog.Validate("gh_inspect_document", Args("""{"gh_session":"s","offset":50,"limit":200}"""));
        GhToolCatalog.Validate("gh_inspect_document", Args("""{"gh_session":"s","mode":"detail","nodes":["a"],"group":"g","match":"m","has_diagnostics":true}"""));
        Assert.Throws<GhException>(() => GhToolCatalog.Validate("gh_inspect_document", Args("""{"gh_session":"s","verbose":true}""")));
        Assert.Throws<GhException>(() => GhToolCatalog.Validate("gh_inspect_document", Args("""{"gh_session":"s","mode":"everything"}""")));
        Assert.Throws<GhException>(() => GhToolCatalog.Validate("gh_inspect_document", Args("""{"gh_session":"s","nodes":[1]}""")));
        Assert.Throws<GhException>(() => GhToolCatalog.Validate("gh_inspect_document", Args("""{"gh_session":"s","has_diagnostics":"yes"}""")));
        Assert.Throws<GhException>(() => GhToolCatalog.Validate("gh_inspect_document", new JsonObject()));
    }
}
