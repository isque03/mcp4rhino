using MCP4Rhino.Contracts;

namespace MCP4Rhino.Logic.Telemetry;

/// <summary>Process-wide telemetry entry points used by the Rhino host.</summary>
public static class Telemetry
{
    private static readonly TelemetryService Service = new();

    public static ITelemetry Gateway => Service;

    public static void Configure(TelemetryOptions options)
    {
        try { Service.Configure(options); }
        catch { /* telemetry must not affect startup */ }
    }

    public static void Track(string eventName)
    {
        try { Service.Track(eventName); }
        catch { /* telemetry must not affect callers */ }
    }

    public static void TrackTool(string toolName, long durationMs, bool success)
        => TrackTool(toolName, durationMs, success, null);

    public static void TrackTool(string toolName, long durationMs, bool success, ToolDiagnostics? diagnostics)
    {
        try { Service.TrackTool(toolName, durationMs, success, diagnostics); }
        catch { /* telemetry must not affect callers */ }
    }

    public static void Revoke()
    {
        try { Service.Revoke(); }
        catch { /* telemetry must not affect callers */ }
    }

    public static void Shutdown()
    {
        try { Service.Shutdown(); }
        catch { /* shutdown stays best-effort */ }
    }
}
