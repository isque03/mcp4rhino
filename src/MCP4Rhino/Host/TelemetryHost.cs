using System.Diagnostics.CodeAnalysis;
using MCP4Rhino.Logic;
using MCP4Rhino.Logic.Telemetry;
using Rhino;

namespace MCP4Rhino.Host;

/// <summary>Connects plugin settings and the usage-data dialog to <see cref="Telemetry"/>.</summary>
[ExcludeFromCodeCoverage]
public static class TelemetryHost
{
    public static void OnStartupIdle()
    {
        var store = TelemetrySettingsStore.Load();
        if (!store.HasChoice || (store.InstallationId is null && store.Enabled))
        {
            store.Enable();
            TelemetrySettingsStore.Save(store);
        }

        Apply(store);
        if (store.Enabled)
            Telemetry.Track(TelemetryEvents.PluginStarted);
    }

    public static string InitializeInstructions()
    {
        try
        {
            return TelemetryUsePrompt.Instructions(SupportInfo.ServerInstructions, ShouldShowNotice());
        }
        catch (Exception ex)
        {
            PluginLog.Info($"telemetry notice skipped: {ex.Message}");
            return SupportInfo.ServerInstructions;
        }
    }

    public static void MarkNoticeDelivered()
    {
        try
        {
            if (!ShouldShowNotice())
                return;
            TelemetrySettingsStore.MarkNoticeShown();
        }
        catch (Exception ex)
        {
            PluginLog.Info($"telemetry notice save skipped: {ex.Message}");
        }
    }

    public static void ShowPreferences(Eto.Forms.Control? owner = null)
    {
        var store = TelemetrySettingsStore.Load();
        var outcome = TelemetryConsentDecision.Apply(
            store,
            dialogResult: TelemetryConsentDialog.ShowPreferences(store.Enabled, owner));
        if (!outcome.Persist)
            return;

        TelemetrySettingsStore.Save(outcome.Consent);
        if (outcome.RevokeSending)
        {
            Telemetry.Revoke();
            PluginLog.Info("telemetry consent disabled");
            return;
        }

        Apply(outcome.Consent);
        if (outcome.RecordPluginStarted)
            Telemetry.Track(TelemetryEvents.PluginStarted);
        PluginLog.Info("telemetry consent enabled");
    }

    public static void Shutdown() => Telemetry.Shutdown();

    public static string StatusLine()
    {
        TelemetryConsent store;
        try
        {
            store = TelemetrySettingsStore.Load();
        }
        catch (Exception ex)
        {
            return $"Usage data: unavailable ({ex.Message})";
        }

        if (!store.Enabled)
            return "Usage data: off";
        if (InactiveReason() is string reason)
            return $"Usage data: on, not sending ({reason})";
        return "Usage data: on";
    }

    private static bool ShouldShowNotice()
    {
        var store = TelemetrySettingsStore.Load();
        return TelemetryUsePrompt.IncludeNotice(
            store.Enabled,
            InactiveReason() is null,
            TelemetrySettingsStore.NoticeShown());
    }

    private static void Apply(TelemetryConsent store)
    {
        var endpoint = store.Enabled ? TelemetryEndpoint.Resolve() : null;
        var apiKey = store.Enabled ? ResolvedApiKey() : null;
        ITelemetryTransport? transport = null;
        if (endpoint is not null && apiKey is not null && store.InstallationId is not null)
        {
            transport = new TelemetryHttpTransport(endpoint, McpJsonRpc.ServerVersion, apiKey: apiKey);
            PluginLog.Info("telemetry sending enabled");
        }
        else if (store.Enabled && endpoint is null)
        {
            PluginLog.Info("telemetry enabled but MCP4RHINO_TELEMETRY_URL is unset");
        }
        else if (store.Enabled)
        {
            PluginLog.Info("telemetry enabled but install token is unset");
        }

        Telemetry.Configure(new TelemetryOptions
        {
            ConsentGranted = store.Enabled,
            InstallationId = store.InstallationId,
            PluginVersion = McpJsonRpc.ServerVersion,
            RhinoVersion = RhinoMajor(),
            Os = TelemetryEnvironment.CurrentOs(),
            Transport = transport,
            Log = PluginLog.Info,
            UseTimer = true,
        });
    }

    private static string? InactiveReason()
    {
        if (TelemetryEndpoint.Resolve() is null)
            return "MCP4RHINO_TELEMETRY_URL unset";
        if (ResolvedApiKey() is null)
            return "install token unset";
        return null;
    }

    private static string? ResolvedApiKey() =>
        TelemetryEndpoint.ApiKey(packaged: PackagedInstallToken());

    private static string? PackagedInstallToken()
    {
        try
        {
            var directory = Path.GetDirectoryName(typeof(TelemetryHost).Assembly.Location);
            return TelemetryEndpoint.ReadInstallToken(directory);
        }
        catch (Exception ex)
        {
            PluginLog.Info($"telemetry install token unreadable ({ex.GetType().Name})");
            return null;
        }
    }

    private static string RhinoMajor()
    {
        try
        {
            var major = RhinoApp.Version.Major;
            return major > 0 ? major.ToString() : "8";
        }
        catch
        {
            return "8";
        }
    }
}
