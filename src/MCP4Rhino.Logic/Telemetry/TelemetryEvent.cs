using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MCP4Rhino.Logic.Telemetry;

public static class TelemetryEvents
{
    public const string PluginStarted = "plugin_started";
    public const string McpConnected = "mcp_connected";
    public const string ToolExecuted = "tool_executed";
}

/// <summary>One anonymous usage event. Tool fields are omitted unless this is <c>tool_executed</c>.</summary>
public sealed class TelemetryEvent
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    [JsonPropertyName("installationId")]
    public required string InstallationId { get; init; }

    [JsonPropertyName("event")]
    public required string EventName { get; init; }

    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; init; }

    [JsonPropertyName("pluginVersion")]
    public required string PluginVersion { get; init; }

    [JsonPropertyName("rhinoVersion")]
    public required string RhinoVersion { get; init; }

    [JsonPropertyName("os")]
    public required string Os { get; init; }

    [JsonPropertyName("tool")]
    public string? Tool { get; init; }

    [JsonPropertyName("durationMs")]
    public long? DurationMs { get; init; }

    [JsonPropertyName("success")]
    public bool? Success { get; init; }

    [JsonPropertyName("errorCode")]
    public string? ErrorCode { get; init; }

    [JsonPropertyName("errorType")]
    public string? ErrorType { get; init; }

    [JsonPropertyName("errorStage")]
    public string? ErrorStage { get; init; }

    [JsonPropertyName("rhinoCommandName")]
    public string? RhinoCommandName { get; init; }

    public static string FormatTimestamp(DateTimeOffset utc) =>
        utc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    public static string SerializeBatch(IReadOnlyList<TelemetryEvent> events) =>
        JsonSerializer.Serialize(new Batch(events), JsonOptions);

    private sealed class Batch(IReadOnlyList<TelemetryEvent> events)
    {
        [JsonPropertyName("events")]
        public IReadOnlyList<TelemetryEvent> Events { get; } = events;
    }
}
