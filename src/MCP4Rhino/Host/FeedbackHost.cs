using System.Diagnostics.CodeAnalysis;
using MCP4Rhino.Logic;
using MCP4Rhino.Logic.Feedback;
using MCP4Rhino.Logic.Telemetry;

namespace MCP4Rhino.Host;

/// <summary>
/// Builds the <c>send_feedback</c> sender. It reuses the endpoint and install-token configuration but
/// not the usage-data choice or the installation id: feedback is an explicit user action.
/// </summary>
[ExcludeFromCodeCoverage]
public static class FeedbackHost
{
    private static readonly Lazy<FeedbackService?> Cached = new(Create);

    public static FeedbackService? Service => Cached.Value;

    private static FeedbackService? Create()
    {
        try
        {
            var endpoint = TelemetryEndpoint.ResolveFeedback();
            var apiKey = TelemetryHost.ResolvedApiKey();
            if (endpoint is null || apiKey is null)
                return null;
            var transport = new FeedbackHttpTransport(endpoint, McpJsonRpc.ServerVersion, apiKey);
            return new FeedbackService(
                transport,
                McpJsonRpc.ServerVersion,
                TelemetryHost.RhinoMajor(),
                TelemetryEnvironment.CurrentOs());
        }
        catch (Exception ex)
        {
            PluginLog.Info($"feedback unavailable ({ex.GetType().Name})");
            return null;
        }
    }
}
