using System.Text.Json;

namespace MCP4Rhino.Logic.Grasshopper;

public static class GhHelp
{
    private static readonly IReadOnlyDictionary<string, string> Topics = new Dictionary<string, string>
    {
        ["index"] = "SKILL.md", ["quickstart"] = "references/quickstart.md",
        ["design"] = "references/design.md", ["data-trees"] = "references/data-trees.md",
        ["workflow"] = "references/workflow.md", ["debugging"] = "references/debugging.md",
        ["examples/array"] = "references/array.md", ["examples/facade"] = "references/facade.md",
        ["examples/loft"] = "references/loft.md",
    };

    public static object Read(string topic = "index")
    {
        if (!Topics.TryGetValue(topic, out var path))
            throw new GhException("unknown_help_topic", $"Unknown topic. Available: {string.Join(", ", Topics.Keys)}", "index");
        var resource = typeof(GhHelp).Assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.Replace('\\', '/') == "GrasshopperHelp/" + path);
        using var stream = resource is null ? null : typeof(GhHelp).Assembly.GetManifestResourceStream(resource);
        if (stream is null) throw new InvalidOperationException($"Missing packaged help {path}.");
        using var reader = new StreamReader(stream);
        var markdown = reader.ReadToEnd();
        object? recipe = null;
        if (topic.StartsWith("examples/"))
        {
            using var example = typeof(GhHelp).Assembly.GetManifestResourceStream("GrasshopperExample/" + topic[9..] + ".json")!;
            recipe = JsonSerializer.Deserialize<System.Text.Json.Nodes.JsonObject>(example);
        }
        return new { version = McpJsonRpc.ServerVersion, topic, topics = Topics.Keys, markdown, recipe };
    }

    public static string Json(string topic = "index") => JsonSerializer.Serialize(Read(topic));
}
