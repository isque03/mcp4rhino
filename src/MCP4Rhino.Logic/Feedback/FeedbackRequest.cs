using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using MCP4Rhino.Logic.Telemetry;

namespace MCP4Rhino.Logic.Feedback;

/// <summary>Validated arguments of the <c>send_feedback</c> tool.</summary>
public sealed record FeedbackRequest(string Message, string Category, string? Tool)
{
    public const string ToolName = "send_feedback";
    public const int MaxMessageLength = 4000;
    public const string DefaultCategory = "other";

    public static readonly IReadOnlyList<string> Categories = ["bug", "feature_request", "praise", "other"];

    private static readonly HashSet<string> KnownArguments = ["message", "category", "tool"];

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>Parses tool arguments. Throws <see cref="ArgumentException"/> with a user-facing message.</summary>
    public static FeedbackRequest Parse(JsonObject args, IReadOnlySet<string> registeredTools)
    {
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(registeredTools);

        foreach (var pair in args)
        {
            if (!KnownArguments.Contains(pair.Key))
                throw new ArgumentException($"Unknown argument '{pair.Key}'. Allowed: message, category, tool.");
        }

        var message = OptionalString(args, "message")?.Trim();
        if (string.IsNullOrEmpty(message))
            throw new ArgumentException("message is required and must not be empty.");
        try { StrictUtf8.GetByteCount(message); }
        catch (EncoderFallbackException) { throw new ArgumentException("message must be valid text."); }
        if (message.EnumerateRunes().Count() > MaxMessageLength)
            throw new ArgumentException($"message must be at most {MaxMessageLength} characters.");

        var category = OptionalString(args, "category")?.Trim();
        if (string.IsNullOrEmpty(category))
            category = DefaultCategory;
        if (!Categories.Contains(category, StringComparer.Ordinal))
            throw new ArgumentException($"category must be one of: {string.Join(", ", Categories)}.");

        var tool = OptionalString(args, "tool")?.Trim();
        if (string.IsNullOrEmpty(tool))
            tool = null;
        else if (!registeredTools.Contains(tool))
            throw new ArgumentException($"tool '{tool}' is not an MCP4Rhino tool name. Omit it if the feedback is general.");

        return new FeedbackRequest(message, category, tool);
    }

    private static string? OptionalString(JsonObject args, string key)
    {
        var node = args[key];
        if (node is null)
            return null;
        if (node is JsonValue value && value.TryGetValue<string>(out var text))
            return text;
        throw new ArgumentException($"{key} must be a string.");
    }
}

/// <summary>
/// The complete feedback request body. It has no installation id and no document, path or
/// geometry data. Do not add fields without updating docs/TELEMETRY.md.
/// </summary>
public sealed class FeedbackPayload
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("category")]
    public required string Category { get; init; }

    [JsonPropertyName("tool")]
    public string? Tool { get; init; }

    [JsonPropertyName("pluginVersion")]
    public required string PluginVersion { get; init; }

    [JsonPropertyName("rhinoVersion")]
    public required string RhinoVersion { get; init; }

    [JsonPropertyName("os")]
    public required string Os { get; init; }

    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; init; }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static string FormatTimestamp(DateTimeOffset utc) => TelemetryEvent.FormatTimestamp(utc);
}
