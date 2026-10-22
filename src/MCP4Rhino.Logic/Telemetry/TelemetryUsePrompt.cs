namespace MCP4Rhino.Logic.Telemetry;

/// <summary>
/// MCP instructions stay free of usage-data text. Tool calls are not gated.
/// The choice lives in <c>Usage data…</c>.
/// </summary>
public static class TelemetryUsePrompt
{
    public const string NoticeShownKey = "TelemetryNoticeShown";
    public const string NoticeShownValue = "shown";

    public static bool IncludeNotice(bool enabled, bool endpointConfigured, bool noticeShown) => false;

    public static string Instructions(string serverInstructions, bool includeNotice) =>
        serverInstructions;
}
