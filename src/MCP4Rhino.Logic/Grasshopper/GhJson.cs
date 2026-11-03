using System.Text.Json.Nodes;

namespace MCP4Rhino.Logic.Grasshopper;

public sealed class GhException(string code, string message, string helpTopic = "workflow") : Exception(message)
{
    public string Code { get; } = code;
    public string HelpTopic { get; } = helpTopic;
}

public static class GhJson
{
    public const string SessionKey = "gh_session";
    public const string LegacySessionKey = "session_id";

    public static string Text(JsonObject args, string key) =>
        args[key]?.GetValue<string>() is { Length: > 0 } s ? s :
            throw new GhException("invalid_arguments", $"{key} is required.");

    /// <summary>
    /// Claude Desktop drops the tool argument name <c>session_id</c> before it reaches MCP servers
    /// (collision with Streamable-HTTP / Mcp-Session-Id). Public schemas use <c>gh_session</c>;
    /// accept the legacy key and rewrite it before schema validation.
    /// </summary>
    public static void AcceptSessionAlias(JsonObject args)
    {
        var current = args[SessionKey]?.GetValue<string>();
        var legacy = args[LegacySessionKey]?.GetValue<string>();
        if (!string.IsNullOrEmpty(current) && !string.IsNullOrEmpty(legacy) && current != legacy)
            throw new GhException("invalid_arguments",
                $"{SessionKey} and {LegacySessionKey} disagree; pass only {SessionKey}.");
        if (string.IsNullOrEmpty(current) && !string.IsNullOrEmpty(legacy))
            args[SessionKey] = legacy;
        args.Remove(LegacySessionKey);
    }

    public static string SessionId(JsonObject args) => Text(args, SessionKey);

    public static int Integer(JsonObject args, string key, int fallback = 0) =>
        args[key]?.GetValue<int>() ?? fallback;

    public static int PageSize(JsonObject args) => Math.Clamp(Integer(args, "limit", 50), 1, 200);

    public static int Offset(JsonObject args) => Math.Max(0, Integer(args, "offset"));

    public static bool Flag(JsonObject args, string key, bool fallback = false) =>
        args[key]?.GetValue<bool>() ?? fallback;

    public static JsonObject Object(JsonNode? value) => value as JsonObject ??
        throw new GhException("invalid_arguments", "Expected an object.");

    public static JsonArray Array(JsonNode? value) => value as JsonArray ??
        throw new GhException("invalid_arguments", "Expected an array.");

    public static double Number(JsonNode? value)
    {
        var v = value as JsonValue ?? throw new GhException("invalid_value", "Number required.", "design");
        var number = v.TryGetValue<double>(out var d) ? d : v.TryGetValue<int>(out var i) ? i :
            v.TryGetValue<long>(out var l) ? l : v.TryGetValue<decimal>(out var m) ? (double)m :
            throw new GhException("invalid_value", "Number required.", "design");
        if (!double.IsFinite(number)) throw new GhException("invalid_value", "Number must be finite.", "design");
        return number;
    }

    public static void ExpectRevision(long current, JsonObject args)
    {
        var expected = args["expected_revision"]?.GetValue<long>() ??
            throw new GhException("invalid_arguments", "expected_revision is required.");
        if (expected != current)
            throw new GhException("revision_conflict", $"Expected revision {expected}; current revision is {current}. Inspect and retry.");
    }

    public static string DefinitionPath(JsonObject args)
    {
        var path = Path.GetFullPath(Text(args, "path"));
        if (!new[] { ".gh", ".ghx" }.Contains(Path.GetExtension(path).ToLowerInvariant()))
            throw new GhException("invalid_path", "Use a .gh or .ghx path.");
        return path;
    }
}
