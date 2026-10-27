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

    public static bool TryMarkdown(string kind, out string markdown)
    {
        markdown = "";
        if (!Names.TryGetValue(kind, out var name)) return false;
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (stream is null) return false;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        markdown = reader.ReadToEnd();
        return true;
    }

    public static string Markdown(string kind) =>
        TryMarkdown(kind, out var markdown) ? markdown : throw new Protocol.McpRpcException(-32002, "Guide not found");
}
