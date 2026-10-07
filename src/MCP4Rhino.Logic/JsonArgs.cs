using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace MCP4Rhino.Logic;

/// <summary>Tolerant JSON argument parsing for MCP tool calls (no Rhino document).</summary>
public static class JsonArgs
{
    public static JsonNode? Unwrap(JsonNode? node)
    {
        if (node is JsonValue v && v.TryGetValue<string>(out var s))
        {
            var t = s.Trim();
            if (t.StartsWith('[') || t.StartsWith('{'))
                return JsonNode.Parse(t);
        }
        return node;
    }

    public static double ToDouble(JsonNode? node)
    {
        if (node is JsonValue v)
        {
            if (v.TryGetValue<double>(out var d)) return d;
            if (v.TryGetValue<string>(out var s) &&
                double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                return d;
        }
        throw new ArgumentException($"Expected a number, got: {node?.ToJsonString() ?? "null"}");
    }

    public static string? Str(JsonObject args, string key)
    {
        var n = args[key];
        if (n is null) return null;
        if (n is JsonValue v && v.TryGetValue<string>(out var s)) return s;
        return n.ToJsonString();
    }

    public static double Num(JsonObject args, string key, double fallback = 0)
    {
        var n = args[key];
        if (n is null) return fallback;
        return ToDouble(n);
    }

    public static bool Bool(JsonObject args, string key, bool fallback = false)
    {
        if (args[key] is not JsonValue v) return fallback;
        if (v.TryGetValue<bool>(out var b)) return b;
        if (v.TryGetValue<string>(out var s) && bool.TryParse(s, out b)) return b;
        return fallback;
    }

    public static bool TryPoint(JsonNode? node, out Vec3 p, double defaultZ = 0)
    {
        p = Vec3.Origin;
        node = Unwrap(node);
        if (node is JsonArray a && (a.Count == 2 || a.Count == 3))
        {
            p = new Vec3(ToDouble(a[0]), ToDouble(a[1]), a.Count == 3 ? ToDouble(a[2]) : defaultZ);
            return true;
        }
        if (node is JsonObject o && o["x"] is not null && o["y"] is not null)
        {
            p = new Vec3(ToDouble(o["x"]), ToDouble(o["y"]), o["z"] is null ? defaultZ : ToDouble(o["z"]));
            return true;
        }
        return false;
    }

    public static Vec3 ParsePoint(JsonNode? node, string label, double defaultZ = 0) =>
        TryPoint(node, out var p, defaultZ) ? p : throw new ArgumentException($"{label} must be [x,y,z] or {{x,y,z}}.");

    public static List<Vec3> ParsePoints(JsonNode? node, string label = "points", double defaultZ = 0)
    {
        node = Unwrap(node);
        if (node is not JsonArray arr || arr.Count == 0)
            throw new ArgumentException($"{label} must be a non-empty array of points.");

        var result = new List<Vec3>();
        if (arr[0] is JsonArray || arr[0] is JsonObject)
        {
            foreach (var item in arr)
                result.Add(ParsePoint(item, label, defaultZ));
            return result;
        }

        var stride = arr.Count % 3 == 0 ? 3 : arr.Count % 2 == 0 ? 2 : throw new ArgumentException(
            $"{label}: flat list length must be a multiple of 3 (x,y,z) or 2 (x,y).");
        for (var i = 0; i < arr.Count; i += stride)
            result.Add(new Vec3(ToDouble(arr[i]), ToDouble(arr[i + 1]), stride == 3 ? ToDouble(arr[i + 2]) : defaultZ));
        return result;
    }

    public static List<Guid> ParseIds(string ids)
    {
        var trimmed = ids.Trim();
        IEnumerable<string> parts = trimmed.StartsWith('[')
            ? JsonSerializer.Deserialize<string[]>(trimmed) ?? []
            : trimmed.Split([',', ';', '\n', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var result = new List<Guid>();
        foreach (var p in parts)
        {
            if (!Guid.TryParse(p, out var g)) throw new ArgumentException($"Invalid GUID: {p}");
            result.Add(g);
        }
        return result;
    }

    public static List<Guid> IdList(JsonNode? node, string label = "ids")
    {
        if (node is null) throw new ArgumentException($"{label} required");
        if (node is JsonArray arr)
            return ParseIds(JsonSerializer.Serialize(arr.Select(x => x?.GetValue<string>() ?? "").ToArray()));
        if (node is JsonValue v && v.TryGetValue<string>(out var s))
            return ParseIds(s);
        throw new ArgumentException($"{label} must be a string or array of GUIDs.");
    }

    public static Dictionary<string, string> ParseTags(JsonObject args)
    {
        var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var key = Str(args, "key");
        var value = Str(args, "value");
        if (!string.IsNullOrWhiteSpace(key))
            tags[key] = value ?? "";

        if (args["tags"] is JsonObject map)
        {
            foreach (var kv in map)
                tags[kv.Key] = kv.Value?.GetValue<string>() ?? kv.Value?.ToJsonString() ?? "";
        }
        return tags;
    }

    public static System.Drawing.Color ParseColor(string hex)
    {
        var h = hex.Trim().TrimStart('#');
        if (h.Length != 6 || !int.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            throw new ArgumentException($"Invalid color '{hex}'; use #RRGGBB.");
        return System.Drawing.Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
    }

    public static System.Drawing.Color? TryParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
        try { return ParseColor(hex); }
        catch { return null; }
    }

    public static string ColorHex(System.Drawing.Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public static readonly (string, string, string)[] CommonProps =
    [
        ("name", "string", "Optional object name"),
        ("layer", "string", "Optional layer full path (created if missing)"),
        ("group", "string", "Optional group name"),
        ("tags", "object", "Optional Attribute UserText key/values"),
    ];

    public static object Tool(string name, string description, params (string Name, string Type, string Desc)[] props)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();
        foreach (var (n, t, d) in props)
        {
            var key = n.TrimEnd('*');
            if (n.EndsWith('*')) required.Add(key);
            var schema = new Dictionary<string, object> { ["description"] = d };
            if (t != "any") schema["type"] = t;
            if (t == "array") schema["items"] = new Dictionary<string, object>();
            properties[key] = schema;
        }
        var input = new Dictionary<string, object> { ["type"] = "object", ["properties"] = properties };
        if (required.Count > 0) input["required"] = required;
        return new Dictionary<string, object>
        {
            ["name"] = name,
            ["description"] = description,
            ["inputSchema"] = input,
        };
    }

    public static object ToolC(string name, string description, params (string Name, string Type, string Desc)[] props) =>
        Tool(name, description, props.Concat(CommonProps).ToArray());
}
