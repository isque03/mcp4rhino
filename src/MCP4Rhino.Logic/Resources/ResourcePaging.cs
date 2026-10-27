using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Logic.Protocol;

namespace MCP4Rhino.Logic.Resources;

public static class ResourcePaging
{
    public const int MaxBytes = 256 * 1024;
    private const int PageBytes = 64 * 1024;
    public static JsonObject Catalog(string name, IEnumerable<object> values, JsonObject args)
    {
        var items = values.Select(v => JsonSerializer.SerializeToNode(v)!).ToArray();
        var offset = Cursor(args["cursor"], name, items.Length);
        var page = items.Skip(offset).Take(50).Select(n => n.DeepClone()).ToArray();
        var result = new JsonObject { [name] = new JsonArray(page) };
        if (offset + page.Length < items.Length)
            result["nextCursor"] = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{McpJsonRpc.ServerVersion}|{name}|{offset + page.Length}"));
        return result;
    }

    private static int Cursor(JsonNode? value, string name, int total)
    {
        if (value is null) return 0;
        try
        {
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(value.GetValue<string>())).Split('|');
            if (parts.Length == 3 && parts[0] == McpJsonRpc.ServerVersion && parts[1] == name &&
                int.TryParse(parts[2], out var offset) && offset > 0 && offset < total) return offset;
        }
        catch (Exception) { }
        throw new McpRpcException(-32602, "Invalid catalog cursor");
    }

    public static JsonObject Page(IEnumerable<object> values, JsonObject args, bool alreadySkipped = false)
    {
        var offset = args["offset"]?.GetValue<int>() ?? 0; var limit = args["limit"]?.GetValue<int>() ?? 50;
        var rows = new JsonArray(); var bytes = 0; var more = false;
        foreach (var value in values.Skip(alreadySkipped ? 0 : offset).Take(limit + 1))
        {
            var node = ResourcePrivacy.Project(JsonSerializer.SerializeToNode(value));
            var size = Encoding.UTF8.GetByteCount(node?.ToJsonString() ?? "null");
            if (rows.Count == limit || bytes + size > PageBytes) { more = true; break; }
            rows.Add(node); bytes += size;
        }
        if (more && rows.Count == 0) throw new McpRpcException(-32000, "Resource item exceeds the read limit; use an explicit inspection tool");
        return new JsonObject { ["offset"] = offset, ["items"] = rows,
            ["next_offset"] = more ? offset + rows.Count : null };
    }

    public static string Continue(string uri, int offset, long? revision = null)
    {
        var address = ResourceAddress.Parse(uri);
        var query = new List<string> { $"offset={offset}", $"limit={address.Arguments["limit"]?.GetValue<int>() ?? 50}" };
        var expected = revision ?? address.Arguments["expected_revision"]?.GetValue<long>();
        if (expected is not null) query.Add($"expected_revision={expected}");
        return uri.Split('?')[0] + "?" + string.Join('&', query);
    }
}
