using System.Diagnostics.CodeAnalysis;
using MCP4Rhino.Logic.Telemetry;
using MCP4Rhino;

namespace MCP4Rhino.Host;

/// <summary>Rhino plugin settings for usage data and the installation id.</summary>
[ExcludeFromCodeCoverage]
public static class TelemetrySettingsStore
{
    public static TelemetryConsent Load()
    {
        var settings = MCP4RhinoPlugin.Instance.Settings;
        return TelemetryConsent.Load(
            settings.GetString(TelemetryConsent.ConsentKey, ""),
            settings.GetString(TelemetryConsent.InstallationIdKey, ""));
    }

    public static void Save(TelemetryConsent consent)
    {
        var settings = MCP4RhinoPlugin.Instance.Settings;
        settings.SetString(TelemetryConsent.ConsentKey, consent.ConsentValue ?? "");
        settings.SetString(TelemetryConsent.InstallationIdKey, consent.InstallationId ?? "");
    }

    public static bool NoticeShown()
    {
        var settings = MCP4RhinoPlugin.Instance.Settings;
        return string.Equals(
            settings.GetString(TelemetryUsePrompt.NoticeShownKey, ""),
            TelemetryUsePrompt.NoticeShownValue,
            StringComparison.Ordinal);
    }

    public static void MarkNoticeShown()
    {
        var settings = MCP4RhinoPlugin.Instance.Settings;
        settings.SetString(TelemetryUsePrompt.NoticeShownKey, TelemetryUsePrompt.NoticeShownValue);
    }
}
