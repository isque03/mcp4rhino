using System.Text.Json;

namespace MCP4Rhino.Logic.Grasshopper;

public static class ComponentSupport
{
    private static readonly Dictionary<string, string[]> Manifest = Read();

    public static object[] Catalog() => Manifest.SelectMany(entry => entry.Value.Select(type =>
        (object)new { assembly = entry.Key, type })).ToArray();

    private static Dictionary<string, string[]> Read()
    {
        using var stream = typeof(ComponentSupport).Assembly.GetManifestResourceStream("GrasshopperNativeComponents.json")!;
        return JsonSerializer.Deserialize<Dictionary<string, string[]>>(stream)!;
    }

    public static bool Allows(string assemblyName, string typeName, bool obsolete = false) =>
        !obsolete && Manifest.TryGetValue(assemblyName, out var types) && types.Contains(typeName);
}
