using System.Text.Json.Nodes;

namespace MCP4Rhino.Logic.Grasshopper;

public sealed record DataBranch(int[] Path, JsonArray Items);

/// <summary>Explicit paths also represent sparse, mixed-depth and empty branches.</summary>
public static class DataTree
{
    public static IReadOnlyList<DataBranch> Parse(JsonNode? node)
    {
        var branches = GhJson.Array(node);
        if (branches.Count > 2000) throw new GhException("limit_exceeded", "At most 2000 branches per value.", "data-trees");
        var result = new List<DataBranch>();
        var paths = new HashSet<string>();
        var count = 0;
        foreach (var value in branches)
        {
            var branch = GhJson.Object(value);
            var path = GhJson.Array(branch["path"]).Select(n => n!.GetValue<int>()).ToArray();
            if (path.Length == 0 || path.Any(i => i < 0) || !paths.Add(string.Join(";", path)))
                throw new GhException("invalid_tree", "Paths must be unique non-empty arrays of nonnegative integers.", "data-trees");
            var items = GhJson.Array(branch["items"]);
            count += items.Count;
            if (count > 10000) throw new GhException("limit_exceeded", "At most 10000 items per value.", "data-trees");
            result.Add(new DataBranch(path, (JsonArray)items.DeepClone()));
        }
        return result;
    }
}
