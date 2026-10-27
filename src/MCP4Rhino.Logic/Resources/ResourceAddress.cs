using System.Text.Json.Nodes;
using MCP4Rhino.Logic.Protocol;

namespace MCP4Rhino.Logic.Resources;

public sealed record ResourceRoute(string Kind, string Area, string Pattern, bool Paged = false)
{
    public bool Revisioned => Kind is "graph" or "node" or "result" or "output";
    public string QueryTemplate => Paged ? (Revisioned ? "{?offset,limit,expected_revision}" : "{?offset,limit}") :
        Revisioned ? "{?expected_revision}" : "";
}

public sealed record ResourceAddress(string Uri, string Kind, string Area, JsonObject Arguments)
{
    public static readonly ResourceRoute[] Routes =
    [
        new("capabilities", "server", "capabilities"),
        new("writing_style", "server", "writing-style"),
        new("getting_started", "server", "getting-started"),
        new("document", "rhino", "documents/{document_id}", true),
        new("layer", "rhino", "documents/{document_id}/layers/{layer_id}"),
        new("object", "rhino", "documents/{document_id}/objects/{object_id}"),
        new("view", "rhino", "documents/{document_id}/views/{view_id}"),
        new("sessions", "grasshopper", "sessions", true),
        new("graph", "grasshopper", "sessions/{session_id}/graph", true),
        new("node", "grasshopper", "sessions/{session_id}/nodes/{node_id}"),
        new("component", "grasshopper", "components/{component_id}"),
        new("result", "grasshopper", "sessions/{session_id}/results/{result_id}", true),
        new("output", "grasshopper", "sessions/{session_id}/results/{result_id}/nodes/{node_id}/outputs/{output_index}", true),
        new("guide", "grasshopper", "guides/{topic}"),
        new("recipe", "grasshopper", "recipes/{version}/{recipe_id}"),
    ];

    public static ResourceAddress Parse(string value)
    {
        if (value.Length > 4096 || !value.StartsWith("mcp4rhino://", StringComparison.Ordinal) ||
            !System.Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.UserInfo.Length != 0 || uri.Port != -1 || uri.Fragment.Length != 0)
            throw Invalid();
        var raw = System.Uri.UnescapeDataString(value.Split('?')[0]);
        if (raw.Contains('\\') || raw.Any(char.IsControl) || raw.Split('/').Any(p => p is "." or "..")) throw Invalid();
        var parts = System.Uri.UnescapeDataString(uri.AbsolutePath).Trim('/').Split('/');
        foreach (var route in Routes.Where(r => r.Area == uri.Host))
        {
            var arguments = Match(route, parts);
            if (arguments is null) continue;
            ReadQuery(uri.Query, route, arguments);
            arguments["kind"] = route.Kind;
            return new(value, route.Kind, route.Area, arguments);
        }
        throw new McpRpcException(-32002, "Unknown resource URI");
    }

    private static JsonObject? Match(ResourceRoute route, string[] parts)
    {
        var pattern = route.Pattern.Split('/');
        // Guide topics may be allowlisted compound names such as examples/profile.
        if (route.Kind == "guide" && parts.Length >= 2 && parts[0] == "guides")
            return new JsonObject { ["topic"] = string.Join('/', parts.Skip(1)) };
        if (parts.Length != pattern.Length) return null;
        var args = new JsonObject();
        for (var i = 0; i < pattern.Length; i++)
        {
            if (!pattern[i].StartsWith('{')) { if (pattern[i] != parts[i]) return null; continue; }
            var name = pattern[i][1..^1]; var item = parts[i];
            if (name.EndsWith("_id") && name != "recipe_id" && !(name == "document_id" && item == "active") && !Guid.TryParseExact(item, "D", out _)) throw Invalid();
            if (name == "output_index" && (!int.TryParse(item, out var index) || index < 0 || index > 2000)) throw Invalid();
            if (item.Length == 0 || item.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not '-' and not '_' and not '.')) throw Invalid();
            args[name] = item;
        }
        return args;
    }

    private static void ReadQuery(string query, ResourceRoute route, JsonObject args)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = System.Uri.UnescapeDataString(parts[0]);
            var value = parts.Length == 2 ? System.Uri.UnescapeDataString(parts[1]) : "";
            if (args.ContainsKey(key) || (key == "expected_revision" ? !route.Revisioned : !route.Paged)) throw Invalid();
            if (key == "expected_revision" && long.TryParse(value, out var revision) && revision >= 0) args[key] = revision;
            else if (key == "offset" && int.TryParse(value, out var offset) && offset is >= 0 and <= 10000000) args[key] = offset;
            else if (key == "limit" && int.TryParse(value, out var limit) && limit is >= 1 and <= 200) args[key] = limit;
            else throw Invalid();
        }
    }

    public static McpRpcException Invalid() => new(-32602, "Invalid resource URI or paging parameters");
}
