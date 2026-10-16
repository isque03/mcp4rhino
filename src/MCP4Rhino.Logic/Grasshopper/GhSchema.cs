using System.Text.Json;
using System.Text.Json.Nodes;
using System.Globalization;

namespace MCP4Rhino.Logic.Grasshopper;

/// <summary>Validator for the JSON Schema subset used by the public GH tools.</summary>
public static class GhSchema
{
    public static void Validate(JsonObject schema, JsonNode? value, string path = "arguments")
    {
        if (schema["oneOf"] is JsonArray choices) { OneOf(choices, value, path); return; }
        if (schema["const"] is { } constant && !JsonNode.DeepEquals(constant, value)) Fail(path, "unexpected operation");
        if (schema["enum"] is JsonArray options && !options.Any(n => JsonNode.DeepEquals(n, value))) Fail(path, "unsupported value");
        if (schema["type"] is null) return;
        var type = schema["type"]!.GetValue<string>();
        var kind = value?.GetValueKind() ?? JsonValueKind.Null;
        var valid = type switch
        {
            "object" => value is JsonObject, "array" => value is JsonArray,
            "string" => kind == JsonValueKind.String, "boolean" => kind is JsonValueKind.True or JsonValueKind.False,
            "number" => kind == JsonValueKind.Number,
            "integer" => kind == JsonValueKind.Number && decimal.TryParse(value!.ToJsonString(),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var integer) && integer == decimal.Truncate(integer)
                && integer >= long.MinValue && integer <= long.MaxValue,
            _ => false,
        };
        if (!valid) Fail(path, $"expected {type}");
        if (value is JsonObject obj) Object(schema, obj, path);
        if (value is JsonArray arr) Array(schema, arr, path);
        if (kind == JsonValueKind.Number) Number(schema, value!, path);
    }

    private static void Object(JsonObject schema, JsonObject value, string path)
    {
        foreach (var key in (schema["required"] as JsonArray ?? new JsonArray()).Select(n => n!.GetValue<string>()))
            if (!value.ContainsKey(key)) Fail(path + "." + key, "required");
        var properties = schema["properties"] as JsonObject ?? new JsonObject();
        foreach (var (key, item) in value)
        {
            if (properties[key] is JsonObject child) Validate(child, item, path + "." + key);
            else if (schema["additionalProperties"]?.GetValue<bool>() == false) Fail(path + "." + key, "unknown property");
        }
    }

    private static void Array(JsonObject schema, JsonArray value, string path)
    {
        if (schema["minItems"] is { } min && value.Count < min.GetValue<int>()) Fail(path, "too few items");
        if (schema["maxItems"] is { } max && value.Count > max.GetValue<int>()) Fail(path, "too many items");
        if (schema["items"] is not JsonObject itemSchema) return;
        for (var i = 0; i < value.Count; i++) Validate(itemSchema, value[i], $"{path}[{i}]");
    }

    private static void Number(JsonObject schema, JsonNode value, string path)
    {
        var number = GhJson.Number(value);
        if (schema["minimum"] is { } min && number < GhJson.Number(min)) Fail(path, "below minimum");
        if (schema["maximum"] is { } max && number > GhJson.Number(max)) Fail(path, "above maximum");
    }

    private static void OneOf(JsonArray choices, JsonNode? value, string path)
    {
        var matches = 0;
        foreach (var choice in choices)
        {
            try { Validate(choice!.AsObject(), value, path); matches++; }
            catch (GhException) { }
        }
        if (matches != 1) Fail(path, "must match exactly one operation schema; see gh_help(workflow)");
    }

    private static void Fail(string path, string message) => throw new GhException("invalid_arguments", path + ": " + message);
}
