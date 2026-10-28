using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Logic;
using MCP4Rhino.Logic.Grasshopper;
using Xunit;

namespace MCP4Rhino.Tests;

public class GrasshopperHelpTests
{
    [Fact]
    public void PermissionHelpPointsToRhinoPreferencesWithoutEnvironmentSetup()
    {
        var help = JsonNode.Parse(GhHelp.Json("workflow"))!["markdown"]!.GetValue<string>();
        Assert.Contains("MCP4RhinoGrasshopper", help);
        Assert.Contains("authorized:true", help);
        Assert.Contains("Apply", help);
        Assert.DoesNotContain("MCP4RHINO_GH_TRUSTED_ASSEMBLIES", help);
    }

    [Fact]
    public void EveryPackagedTopicLoadsWithoutGrasshopperAssemblies()
    {
        var index = JsonNode.Parse(GhHelp.Json())!;
        Assert.Equal(McpJsonRpc.ServerVersion, index["version"]!.GetValue<string>());
        foreach (var topic in index["topics"]!.AsArray())
        {
            var result = JsonNode.Parse(GhHelp.Json(topic!.GetValue<string>()))!;
            Assert.False(string.IsNullOrWhiteSpace(result["markdown"]!.GetValue<string>()));
        }
        Assert.Throws<GhException>(() => GhHelp.Read("../../private"));
        Assert.Contains("gh_help", SupportInfo.ServerInstructions);
        Assert.Contains("convert_length", SupportInfo.ServerInstructions);
        Assert.Contains("architecture", SupportInfo.ServerInstructions);
        Assert.Contains("not for ordinary one-off Rhino edits", SupportInfo.ServerInstructions);
        Assert.Contains("grasshopper_getting_started", SupportInfo.ServerInstructions);
        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), a => a.GetName().Name == "Grasshopper");
    }

    [Theory]
    [InlineData("array")]
    [InlineData("facade")]
    [InlineData("loft")]
    [InlineData("profile")]
    [InlineData("stair")]
    public void WorkedRecipesUseSupportedTypesAndReleasedPatchSchemas(string name)
    {
        var help = JsonNode.Parse(GhHelp.Json("examples/" + name))!;
        var recipe = help["recipe"]!.AsObject();
        foreach (var (_, type) in recipe["components"]!.AsObject())
        {
            var fullName = type!.GetValue<string>();
            var assembly = fullName.StartsWith("LinearXForms.") ? "XformComponents" : fullName.Split('.')[0];
            Assert.True(ComponentSupport.Allows(assembly, fullName), fullName);
        }
        var operations = recipe["operations"]!.AsArray();
        foreach (var batch in operations.Chunk(100))
        {
            var patch = new JsonObject { ["session_id"] = "fixture-session", ["expected_revision"] = 0,
                ["request_id"] = "fixture", ["operations"] = new JsonArray(batch.Select(n => n!.DeepClone()).ToArray()) };
            GhToolCatalog.Validate("gh_apply_patch", patch);
        }
        foreach (var value in operations.Where(n => n!["op"]!.GetValue<string>() == "set_value"))
            DataTree.Parse(value!["tree"]);
        var aliases = operations.Where(n => n!["op"]!.GetValue<string>() == "add")
            .Select(n => n!["alias"]!.GetValue<string>()).ToArray();
        Assert.Contains(recipe["output"]!["node"]!.GetValue<string>(), aliases);
        Assert.Equal(aliases.Length, aliases.Distinct().Count());
        foreach (var op in operations)
        {
            foreach (var endpoint in new[] { op, op!["source"], op["target"] }.Where(n => n?["node"] is not null))
                Assert.Contains(endpoint!["node"]!.GetValue<string>(), aliases);
        }
    }
}
