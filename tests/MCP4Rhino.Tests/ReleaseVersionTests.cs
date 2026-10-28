using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Logic;
using Xunit;

namespace MCP4Rhino.Tests;

public class ReleaseVersionTests
{
    [Fact]
    public void Initialize_Support_And_Compiled_Assembly_Report_The_Same_Version()
    {
        var assembly = typeof(McpJsonRpc).Assembly;
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
        Assert.Equal(McpJsonRpc.ServerVersion, informational.Split('+')[0]);
        Assert.Equal(new Version(0, 7, 2, 0), assembly.GetName().Version);
        Assert.Equal("0.7.2.0", assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version);

        var response = McpJsonRpc.Dispatch(new JsonObject { ["id"] = 1, ["method"] = "initialize" }, new EmptyTools());
        Assert.Equal(McpJsonRpc.ServerVersion, response!["result"]!["serverInfo"]!["version"]!.GetValue<string>());
        var support = JsonSerializer.SerializeToNode(SupportInfo.BuildPayload());
        Assert.Equal(McpJsonRpc.ServerVersion, support!["server_version"]!.GetValue<string>());
    }

    private sealed class EmptyTools : IMcpToolProvider
    {
        public object[] ListTools() => [];
        public object CallTool(JsonNode? arguments) => throw new NotSupportedException();
    }
}
