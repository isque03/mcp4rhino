using System.Reflection;
using System.Text;

namespace MCP4Rhino.Logic.Resources;

/// <summary>Packaged server markdown guides (no native Rhino access).</summary>
public static class ServerGuides
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.Ordinal)
    {
        ["writing_style"] = "ServerGuide.writing-style.md",
        ["getting_started"] = "ServerGuide.getting-started.md",
    };

    public static string Markdown(string kind)
    {
        if (!Names.TryGetValue(kind, out var name))
            throw new Protocol.McpRpcException(-32002, "Guide not found");
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name)
            ?? throw new Protocol.McpRpcException(-32002, "Guide not found");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
