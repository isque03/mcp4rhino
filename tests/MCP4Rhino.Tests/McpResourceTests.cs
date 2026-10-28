using System.Text;
using System.Text.Json.Nodes;
using MCP4Rhino.Contracts;
using MCP4Rhino.Logic;
using MCP4Rhino.Logic.Grasshopper;
using MCP4Rhino.Logic.Protocol;
using MCP4Rhino.Logic.Resources;
using Xunit;

namespace MCP4Rhino.Tests;

public class McpResourceTests
{
    private const string Id = "df4f2d20-a272-443e-ab48-df5a84a065e0";
    private static readonly McpRequestContext Context = new(McpRequestContext.Modern, new());
    private static JsonObject Read(string uri, IMcpResourceGateway? gateway = null) =>
        McpResources.Read(new JsonObject { ["uri"] = uri }, gateway, Context);
    private static JsonNode Data(JsonObject result) => JsonNode.Parse(result["contents"]![0]!["text"]!.GetValue<string>())!;

    [Theory]
    [InlineData("mcp4rhino://server/capabilities", "capabilities")]
    [InlineData("mcp4rhino://server/getting-started", "getting_started")]
    [InlineData("mcp4rhino://server/grasshopper-getting-started", "grasshopper_getting_started")]
    [InlineData("mcp4rhino://server/writing-style", "writing_style")]
    [InlineData("mcp4rhino://rhino/documents/active", "document")]
    [InlineData("mcp4rhino://rhino/documents/" + Id, "document")]
    [InlineData("mcp4rhino://rhino/documents/" + Id + "/objects/" + Id, "object")]
    [InlineData("mcp4rhino://rhino/documents/" + Id + "/layers/" + Id, "layer")]
    [InlineData("mcp4rhino://rhino/documents/" + Id + "/views/" + Id, "view")]
    [InlineData("mcp4rhino://grasshopper/sessions", "sessions")]
    [InlineData("mcp4rhino://grasshopper/sessions/" + Id + "/graph", "graph")]
    [InlineData("mcp4rhino://grasshopper/sessions/" + Id + "/nodes/" + Id, "node")]
    [InlineData("mcp4rhino://grasshopper/sessions/" + Id + "/results/" + Id, "result")]
    [InlineData("mcp4rhino://grasshopper/sessions/" + Id + "/results/" + Id + "/nodes/" + Id + "/outputs/0", "output")]
    [InlineData("mcp4rhino://grasshopper/components/" + Id, "component")]
    [InlineData("mcp4rhino://grasshopper/guides/examples/profile", "guide")]
    [InlineData("mcp4rhino://grasshopper/recipes/0.7.1/profile", "recipe")]
    public void CatalogRoutesHaveUnambiguousTypedIdentifiers(string uri, string expected) =>
        Assert.Equal(expected, ResourceAddress.Parse(uri).Kind);

    [Theory]
    [InlineData("file:///etc/passwd")]
    [InlineData("https://example.com/data")]
    [InlineData("mcp4rhino://grasshopper/guides/../private")]
    [InlineData("mcp4rhino://grasshopper/guides/%2e%2e/private")]
    [InlineData("mcp4rhino://grasshopper/guides/%5Cprivate")]
    [InlineData("mcp4rhino://user@grasshopper/sessions")]
    [InlineData("mcp4rhino://grasshopper:99/sessions")]
    [InlineData("mcp4rhino://grasshopper/sessions#fragment")]
    [InlineData("mcp4rhino://rhino/documents/not-an-id")]
    [InlineData("mcp4rhino://grasshopper/sessions?path=/etc/passwd")]
    [InlineData("mcp4rhino://grasshopper/sessions?limit=201")]
    [InlineData("mcp4rhino://grasshopper/sessions?offset=-1")]
    [InlineData("mcp4rhino://grasshopper/sessions?limit=2&limit=3")]
    [InlineData("mcp4rhino://grasshopper/guides/index?offset=1")]
    public void InvalidUrisCannotReachNativeGateways(string uri) => Assert.Throws<McpRpcException>(() => Read(uri, new ForbiddenGateway()));

    [Fact]
    public void ResourcesAndTemplatesAreDiscoverableWithoutNativeAccess()
    {
        var list = ResourceCatalog.List(new()); Assert.Equal(7, list["resources"]!.AsArray().Count);
        foreach (var resource in list["resources"]!.AsArray())
            Assert.NotNull(ResourceAddress.Parse(resource!["uri"]!.GetValue<string>()));
        var templates = ResourceCatalog.Templates(new())["resourceTemplates"]!.AsArray();
        Assert.Equal(ResourceAddress.Routes.Count(route => route.Pattern.Contains('{') || route.Kind == "sessions"), templates.Count);
        Assert.Contains(templates, item => item!["uriTemplate"]!.GetValue<string>().Contains("{?offset,limit,expected_revision}"));
    }

    [Fact]
    public void EveryGuideAndVersionedRecipeUsesEmbeddedHelpOnly()
    {
        foreach (var topic in GhHelp.TopicsIndex.Keys)
        {
            var guide = Read("mcp4rhino://grasshopper/guides/" + topic, new ForbiddenGateway());
            Assert.Equal("text/markdown", guide["contents"]![0]!["mimeType"]!.GetValue<string>());
        }
        foreach (var recipe in McpResources.Recipes)
        {
            var contents = Data(Read($"mcp4rhino://grasshopper/recipes/{McpJsonRpc.ServerVersion}/{recipe}", new ForbiddenGateway()));
            Assert.Equal(recipe, contents["recipe_id"]!.GetValue<string>());
            Assert.NotEmpty(contents["recipe"]!["operations"]!.AsArray());
        }
        var index = Read("mcp4rhino://grasshopper/guides/index", new ForbiddenGateway())["contents"]![0]!["text"]!.GetValue<string>();
        foreach (System.Text.RegularExpressions.Match link in System.Text.RegularExpressions.Regex.Matches(index, @"\]\((mcp4rhino://[^)]+)\)"))
            Assert.NotNull(Read(link.Groups[1].Value, new ForbiddenGateway()));
        Assert.Throws<McpRpcException>(() => Read("mcp4rhino://grasshopper/guides/unknown"));
        Assert.Throws<McpRpcException>(() => Read("mcp4rhino://grasshopper/recipes/0.1.0/profile"));
        Assert.Throws<McpRpcException>(() => Read("mcp4rhino://grasshopper/recipes/0.7.1/unknown"));
    }

    [Fact]
    public void StatusAndPrivacyAreExplicitAndTreePathsSurviveProjection()
    {
        var unknown = Data(Read("mcp4rhino://grasshopper/sessions"));
        Assert.Equal("unavailable", unknown["status"]!.GetValue<string>()); Assert.Null(unknown["sessions"]);
        var gateway = new FixedGateway(JsonNode.Parse("""{"status":"uninitialized","path":"/Users/private/model.3dm","tags":{"secret":"x"},"data":[{"path":[3,0],"value":{"type":"string","value":"private text"}}],"diagnostic":"Cannot read /etc/secret.txt or C:\\private\\model.gh"}""")!.AsObject());
        var data = Data(Read("mcp4rhino://grasshopper/sessions", gateway));
        Assert.Null(data["path"]); Assert.Null(data["tags"]);
        Assert.Equal(3, data["data"]![0]!["path"]![0]!.GetValue<int>());
        Assert.True(data["data"]![0]!["value"]!["redacted"]!.GetValue<bool>());
        Assert.DoesNotContain("private text", data.ToJsonString()); Assert.DoesNotContain("secret.txt", data.ToJsonString());
        Assert.Equal("live_read", data["freshness"]!.GetValue<string>());
        Assert.NotNull(data["observed_at_utc"]);
        Assert.Equal("mcp4rhino://server/getting-started",
            ResourcePrivacy.Project(JsonValue.Create("mcp4rhino://server/getting-started"))!.GetValue<string>());
        Assert.Equal("mcp4rhino://rhino/documents/active",
            ResourcePrivacy.Project(JsonValue.Create("mcp4rhino://rhino/documents/active"))!.GetValue<string>());
        Assert.Contains("[local path redacted]", ResourcePrivacy.Project(JsonValue.Create("C:\\private\\model.3dm"))!.GetValue<string>());
    }

    [Fact]
    public void CapabilityResourceReflectsCurrentRequestAndRealLimits()
    {
        var data = Data(Read("mcp4rhino://server/capabilities", new FixedGateway(new() { ["status"] = "available" })));
        Assert.Equal(McpRequestContext.Modern, data["protocol_version"]!.GetValue<string>());
        Assert.Equal(256 * 1024, data["limits"]!["resource_max_bytes"]!.GetValue<int>());
        Assert.Equal(3, data["supported_protocol_versions"]!.AsArray().Count);
        Assert.Equal(ComponentSupport.Catalog().Length, data["supported_components"]!.AsArray().Count);
        Assert.Contains("getting_started", data["lanes"]!.AsObject().Select(p => p.Key));
        Assert.Contains("architecture", data["lanes"]!.AsObject().Select(p => p.Key));
        Assert.Contains("grasshopper", data["lanes"]!.AsObject().Select(p => p.Key));
        Assert.Contains("Do not open Grasshopper", data["lane_guidance"]!.GetValue<string>());
        Assert.Equal("get_document_info", data["lanes"]!["document"]!["entry"]![0]!.GetValue<string>());
        Assert.Equal("mcp4rhino://server/getting-started", data["lanes"]!["getting_started"]!["resource"]!.GetValue<string>());
        Assert.Equal("getting_started", data["lanes"]!["getting_started"]!["prompt"]!.GetValue<string>());
        Assert.Equal("grasshopper_getting_started", data["lanes"]!["grasshopper"]!["prompt"]!.GetValue<string>());
        Assert.Equal("mcp4rhino://server/grasshopper-getting-started",
            data["lanes"]!["grasshopper"]!["resource"]!.GetValue<string>());
        var promptNames = PromptCatalog.List(new())["prompts"]!.AsArray()
            .Select(prompt => prompt!["name"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
        foreach (var lane in data["lanes"]!.AsObject())
        {
            foreach (var entry in lane.Value!["entry"]!.AsArray())
                Assert.False(promptNames.Contains(entry!.GetValue<string>()),
                    $"Lane {lane.Key} entry '{entry}' must be a tool name, not a prompt");
        }
    }

    [Theory]
    [InlineData("mcp4rhino://server/getting-started", "Getting started")]
    [InlineData("mcp4rhino://server/grasshopper-getting-started", "Grasshopper getting started")]
    [InlineData("mcp4rhino://server/writing-style", "Writing style")]
    public void ServerGuidesAreMarkdownWithoutNativeAccess(string uri, string heading)
    {
        var result = Read(uri, new ForbiddenGateway());
        var content = result["contents"]![0]!;
        Assert.Equal("text/markdown", content["mimeType"]!.GetValue<string>());
        Assert.Contains(heading, content["text"]!.GetValue<string>());
        Assert.Contains(McpJsonRpc.ServerVersion, content["text"]!.GetValue<string>());
    }

    [Fact]
    public void PaginationPreservesRevisionAndStopsAtByteAndItemLimits()
    {
        var args = new JsonObject { ["limit"] = 2, ["offset"] = 1 };
        var page = ResourcePaging.Page(new object[] { 0, 1, 2, 3 }, args);
        Assert.Equal(2, page["items"]!.AsArray().Count); Assert.Equal(3, page["next_offset"]!.GetValue<int>());
        var skipped = ResourcePaging.Page(new object[] { 5, 6, 7 }, new() { ["offset"] = 5, ["limit"] = 2 }, alreadySkipped: true);
        Assert.Equal(5, skipped["items"]![0]!.GetValue<int>()); Assert.Equal(7, skipped["next_offset"]!.GetValue<int>());
        var bytes = ResourcePaging.Page(Enumerable.Range(0, 200).Select(i => (object)new { index = i, name = new string('x', 2000) }), new() { ["limit"] = 200 });
        Assert.InRange(bytes["items"]!.AsArray().Count, 1, 100); Assert.NotNull(bytes["next_offset"]);
        var uri = ResourcePaging.Continue($"mcp4rhino://grasshopper/sessions/{Id}/graph?limit=2", 3, 7);
        var parsed = ResourceAddress.Parse(uri); Assert.Equal(7, parsed.Arguments["expected_revision"]!.GetValue<long>());
        var content = Data(Read($"mcp4rhino://grasshopper/sessions/{Id}/graph", new FixedGateway(new()
            { ["revision"] = 7, ["data"] = new JsonObject { ["next_offset"] = 2 } })));
        Assert.Contains("expected_revision=7", content["data"]!["next_uri"]!.GetValue<string>());
    }

    [Fact]
    public void CatalogCursorsAreBoundToVersionAndCatalog()
    {
        var items = Enumerable.Range(0, 55).Select(i => (object)new { name = i.ToString() }).ToArray();
        var first = ResourcePaging.Catalog("resources", items, new());
        var cursor = first["nextCursor"]!.GetValue<string>();
        Assert.Equal(5, ResourcePaging.Catalog("resources", items, new() { ["cursor"] = cursor })["resources"]!.AsArray().Count);
        Assert.Throws<McpRpcException>(() => ResourcePaging.Catalog("prompts", items, new() { ["cursor"] = cursor }));
        Assert.Throws<McpRpcException>(() => ResourceCatalog.List(new() { ["cursor"] = "garbage" }));
    }

    [Fact]
    public void OversizedResourcesAndIndividualItemsFailExplicitly()
    {
        var large = new JsonObject(); for (var i = 0; i < 200; i++) large[i.ToString()] = new string('x', 2000);
        Assert.Throws<McpRpcException>(() => Read("mcp4rhino://rhino/documents/active", new FixedGateway(large)));
        Assert.Throws<McpRpcException>(() => ResourcePaging.Page(new object[] { large }, new()));
    }

    [Fact]
    public void DocumentIdsAreOpaqueStableAndDoNotAuthorizeAnotherActiveDocument()
    {
        var first = DocumentResourceIds.For(90001); var second = DocumentResourceIds.For(90002);
        Assert.True(Guid.TryParse(first, out _)); Assert.Equal(first, DocumentResourceIds.For(90001));
        Assert.NotEqual(first, second); Assert.True(DocumentResourceIds.Matches(first, 90001));
        Assert.False(DocumentResourceIds.Matches(first, 90002)); Assert.True(Guid.TryParse(DocumentResourceIds.InstanceId, out _));
    }

    private sealed class ForbiddenGateway : IMcpResourceGateway
    { public JsonObject Read(ResourceAddress address) => throw new InvalidOperationException("Native reads forbidden for this request"); }
    private sealed class FixedGateway(JsonObject data) : IMcpResourceGateway
    { public JsonObject Read(ResourceAddress address) => (JsonObject)data.DeepClone(); }
}
