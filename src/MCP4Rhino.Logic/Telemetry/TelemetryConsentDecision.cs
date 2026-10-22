namespace MCP4Rhino.Logic.Telemetry;

/// <summary>
/// Usage-data dialog policy, independent of Eto.
/// Closing without a button leaves the stored choice unchanged.
/// Turning telemetry back on does not record <c>plugin_started</c> until the next Rhino start.
/// </summary>
public static class TelemetryConsentDecision
{
    public readonly record struct Outcome(
        TelemetryConsent Consent,
        bool Persist,
        bool RevokeSending,
        bool RecordPluginStarted);

    public static Outcome Apply(TelemetryConsent current, bool? dialogResult)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (dialogResult is null)
            return new Outcome(Copy(current), Persist: false, RevokeSending: false, RecordPluginStarted: false);

        var next = Copy(current);
        if (dialogResult == true)
        {
            next.Enable();
            return new Outcome(next, Persist: true, RevokeSending: false, RecordPluginStarted: false);
        }

        next.Disable();
        return new Outcome(next, Persist: true, RevokeSending: true, RecordPluginStarted: false);
    }

    private static TelemetryConsent Copy(TelemetryConsent current) =>
        TelemetryConsent.Load(current.ConsentValue, current.InstallationId);
}
