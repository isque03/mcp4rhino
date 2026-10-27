using MCP4Rhino.Logic;
using Xunit;

namespace MCP4Rhino.Tests;

public class SupportInfoTests
{
    [Fact]
    public void Urls_And_Payload_Are_Stable()
    {
        Assert.StartsWith("https://github.com/isque03/mcp4rhino", SupportInfo.RepositoryUrl);
        Assert.Equal("https://github.com/isque03/mcp4rhino/issues", SupportInfo.IssuesUrl);
        Assert.Contains("labels=bug", SupportInfo.NewBugUrl);
        Assert.Contains("labels=enhancement", SupportInfo.NewEnhancementUrl);
        Assert.Contains("get_support_info", SupportInfo.ServerInstructions);
        Assert.Contains("mcp4rhino://server/capabilities", SupportInfo.ServerInstructions);
        Assert.Contains("getting_started", SupportInfo.ServerInstructions);
        Assert.Contains("mcp4rhino://server/getting-started", SupportInfo.ServerInstructions);
        Assert.Contains("ASD-STE100 style", SupportInfo.ServerInstructions);
        Assert.Contains("mcp4rhino://server/writing-style", SupportInfo.ServerInstructions);
        Assert.DoesNotContain("Learn parametric design and this server", SupportInfo.ServerInstructions);
        Assert.DoesNotContain("usage", SupportInfo.ServerInstructions, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("consent", SupportInfo.ServerInstructions, StringComparison.OrdinalIgnoreCase);

        var json = System.Text.Json.JsonSerializer.Serialize(SupportInfo.BuildPayload(
            endpoint: "http://127.0.0.1:4010/mcp",
            logPath: "/tmp/mcp4rhino.log",
            serverRunning: true));
        Assert.Contains("issues_url", json);
        Assert.Contains("new_bug_url", json);
        Assert.Contains("4010", json);
        Assert.Contains("server_running", json);
    }

    [Fact]
    public void ResolvePort_Uses_Default_When_Env_Unset_Or_Invalid()
    {
        // Design rule: unset/invalid MCP4RHINO_PORT → SupportInfo.DefaultPort (4010).
        var prev = Environment.GetEnvironmentVariable("MCP4RHINO_PORT");
        try
        {
            Environment.SetEnvironmentVariable("MCP4RHINO_PORT", null);
            Assert.Equal(SupportInfo.DefaultPort, SupportInfo.ResolvePort());
            Assert.Equal("http://127.0.0.1:4010/mcp", SupportInfo.ConfiguredEndpoint());

            Environment.SetEnvironmentVariable("MCP4RHINO_PORT", "not-a-port");
            Assert.Equal(SupportInfo.DefaultPort, SupportInfo.ResolvePort());

            Environment.SetEnvironmentVariable("MCP4RHINO_PORT", "0");
            Assert.Equal(SupportInfo.DefaultPort, SupportInfo.ResolvePort());

            Environment.SetEnvironmentVariable("MCP4RHINO_PORT", "4123");
            Assert.Equal(4123, SupportInfo.ResolvePort());
            Assert.Equal("http://127.0.0.1:4123/mcp", SupportInfo.ConfiguredEndpoint());
        }
        finally
        {
            Environment.SetEnvironmentVariable("MCP4RHINO_PORT", prev);
        }
    }
}
