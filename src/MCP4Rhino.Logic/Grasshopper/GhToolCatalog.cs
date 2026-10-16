using System.Text.Json.Nodes;

namespace MCP4Rhino.Logic.Grasshopper;

public static class GhToolCatalog
{
    private static readonly IReadOnlyDictionary<string, JsonObject> Definitions = Read();

    private static IReadOnlyDictionary<string, JsonObject> Read()
    {
        using var stream = typeof(GhToolCatalog).Assembly.GetManifestResourceStream("GrasshopperTools.json")!;
        return JsonNode.Parse(stream)!.AsArray().Select(n => n!.AsObject()).ToDictionary(n => n["name"]!.GetValue<string>());
    }

    public static object[] ListTools() => Definitions.Values.Select(n => (object)n.DeepClone()).ToArray();

    public static bool Contains(string name) => Definitions.ContainsKey(name);

    public static void Validate(string name, JsonObject args)
    {
        var definition = Definitions.GetValueOrDefault(name) ??
            throw new GhException("unknown_tool", $"Unknown Grasshopper tool {name}.", "index");
        GhSchema.Validate(definition["inputSchema"]!.AsObject(), args);
    }
}
