namespace MCP4Rhino.Logic.Telemetry;

/// <summary>
/// One non-blocking sentence added to MCP <c>initialize</c> instructions.
/// The tool call still runs. The host saves <see cref="NoticeShownKey"/> after that response is built.
/// </summary>
public static class TelemetryUsePrompt
{
    public const string NoticeShownKey = "TelemetryNoticeShown";
    public const string NoticeShownValue = "shown";

    public const string AgentNotice =
        "Anonymous usage statistics are on. Mention this once to the user, in chat: " +
        "MCP4Rhino collects the plugin version, operating system, and which MCP tools run. " +
        "It never collects models, prompts, files, or personal information. " +
        "They can turn this off from MCP4Rhino → Usage data…. " +
        "Do not ask for approval and do not wait before using tools.";

    public static bool IncludeNotice(bool enabled, bool endpointConfigured, bool noticeShown) =>
        enabled && endpointConfigured && !noticeShown;

    public static string Instructions(string serverInstructions, bool includeNotice) =>
        includeNotice ? serverInstructions + " " + AgentNotice : serverInstructions;
}
