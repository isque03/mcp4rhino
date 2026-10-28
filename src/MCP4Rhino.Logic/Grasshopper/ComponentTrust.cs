using System.Text.Json;

namespace MCP4Rhino.Logic.Grasshopper;

/// <summary>User-selected library permissions, stored by the host in Rhino plugin settings.</summary>
public sealed class ComponentTrust
{
    private readonly HashSet<string> _identities;
    public string[] Identities => _identities.Order(StringComparer.Ordinal).ToArray();

    public ComponentTrust(IEnumerable<string> identities) => _identities = new(identities, StringComparer.Ordinal);
    public bool Allows(string identity) => _identities.Contains(identity);
    public string ToJson() => JsonSerializer.Serialize(Identities);

    public static ComponentTrust Load(string? json)
    {
        try
        {
            var identities = JsonSerializer.Deserialize<string[]>(json ?? "[]");
            return identities is null || identities.Any(string.IsNullOrWhiteSpace) ? new([]) : new(identities);
        }
        catch (JsonException) { return new([]); }
    }

    public ComponentTrust ReplaceVisible(IEnumerable<string> visible, IEnumerable<string> selected)
    {
        var shown = visible.ToHashSet(StringComparer.Ordinal);
        return new(_identities.Where(id => !shown.Contains(id)).Concat(selected.Where(shown.Contains)));
    }
}

public static class BuiltInComponentAssembly
{
    private static readonly HashSet<string> ModelingLibraries = new(StringComparer.Ordinal)
    {
        "Grasshopper", "CurveComponents", "SurfaceComponents", "MathComponents", "VectorComponents",
        "XformComponents", "FieldComponents", "TriangulationComponents",
    };

    public static bool Matches(string name, string token, string path, string grasshopperPath, string grasshopperToken)
    {
        if (!ModelingLibraries.Contains(name) || string.IsNullOrWhiteSpace(token) ||
            !string.Equals(token, grasshopperToken, StringComparison.OrdinalIgnoreCase) ||
            !Path.IsPathFullyQualified(path) || !Path.IsPathFullyQualified(grasshopperPath)) return false;
        var relative = Path.GetRelativePath(Path.GetDirectoryName(grasshopperPath)!, path);
        return !Path.IsPathRooted(relative) && relative != ".." &&
            !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
}
