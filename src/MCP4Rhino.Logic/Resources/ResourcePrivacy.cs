using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace MCP4Rhino.Logic.Resources;

/// <summary>Resource-only projection. Existing explicit tool responses remain unchanged.</summary>
public static partial class ResourcePrivacy
{
    // Drive letters must not match letters inside URIs such as mcp4rhino://…
    [GeneratedRegex(@"(?:file://|(?<![A-Za-z0-9])[A-Za-z]:[\\/]|(?<![:/A-Za-z0-9])/(?!/)|\\\\)[^\s\""<>]*")]
    private static partial Regex LocalPath();

    public static JsonNode? Project(JsonNode? value)
    {
        if (value is JsonArray array) return new JsonArray(array.Select(Project).ToArray());
        if (value is JsonObject obj)
        {
            var output = new JsonObject();
            foreach (var (key, item) in obj)
            {
                if (key is "file_path" or "filePath" or "tags" or "user_text" or "rhino_document" ||
                    key == "path" && item is not JsonArray) continue;
                output[key] = Project(item);
            }
            if (Protocol.McpRequestContext.Text(obj["type"]) == "string" && obj.ContainsKey("value"))
            { output["value"] = null; output["redacted"] = true; }
            return output;
        }
        if (value is JsonValue scalar && scalar.TryGetValue<string>(out var text))
        {
            var sanitized = LocalPath().Replace(text, "[local path redacted]");
            return JsonValue.Create(sanitized.Length <= 2000 ? sanitized : sanitized[..2000] + " [truncated]");
        }
        return value?.DeepClone();
    }
}
