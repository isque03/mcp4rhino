namespace MCP4Rhino.Logic.Telemetry;

public sealed class TelemetryOptions
{
    public bool ConsentGranted { get; init; }

    public string? InstallationId { get; init; }

    public string PluginVersion { get; init; } = McpJsonRpc.ServerVersion;

    public string RhinoVersion { get; init; } = "8";

    public string Os { get; init; } = "macOS";

    public ITelemetryTransport? Transport { get; init; }

    public Func<DateTimeOffset>? UtcNow { get; init; }

    public Action<string>? Log { get; init; }

    /// <summary>Production starts a 30-second timer. Tests flush through <see cref="TelemetryService.NotifyFlushInterval"/>.</summary>
    public bool UseTimer { get; init; } = true;
}
