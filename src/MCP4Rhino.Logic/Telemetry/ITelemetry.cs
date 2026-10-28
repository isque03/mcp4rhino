using MCP4Rhino.Contracts;

namespace MCP4Rhino.Logic.Telemetry;

/// <summary>Fire-and-forget usage events. Implementations must not throw.</summary>
public interface ITelemetry
{
    void Track(string eventName);

    void TrackTool(string toolName, long durationMs, bool success);

    void TrackTool(string toolName, long durationMs, bool success, ToolDiagnostics? diagnostics) =>
        TrackTool(toolName, durationMs, success);
}

/// <summary>
/// Delivers one batch. Return <c>false</c> for a non-success response.
/// Throw on network failure; the caller discards the batch either way.
/// </summary>
public interface ITelemetryTransport : IDisposable
{
    Task<bool> SendAsync(IReadOnlyList<TelemetryEvent> events, CancellationToken cancellationToken);
}
