namespace MCP4Rhino.Logic.Telemetry;

/// <summary>
/// Usage-data choice and the random installation id.
/// A missing choice is on. The id is created the first time telemetry is on and kept across disable.
/// </summary>
public sealed class TelemetryConsent
{
    public const string ConsentKey = "TelemetryConsent";
    public const string InstallationIdKey = "TelemetryInstallationId";
    public const string EnabledValue = "enabled";
    public const string DisabledValue = "disabled";

    public bool HasChoice { get; private set; }

    public bool Enabled { get; private set; }

    public string? InstallationId { get; private set; }

    public string? ConsentValue => !HasChoice ? null : Enabled ? EnabledValue : DisabledValue;

    public static TelemetryConsent Load(string? consent, string? installationId)
    {
        var stored = new TelemetryConsent
        {
            InstallationId = NormalizeId(installationId),
        };

        var choice = consent?.Trim();
        if (string.Equals(choice, DisabledValue, StringComparison.Ordinal))
        {
            stored.HasChoice = true;
            stored.Enabled = false;
        }
        else if (string.Equals(choice, EnabledValue, StringComparison.Ordinal))
        {
            stored.HasChoice = true;
            stored.Enabled = true;
        }
        else
        {
            stored.Enabled = true;
        }

        return stored;
    }

    public void Enable()
    {
        HasChoice = true;
        Enabled = true;
        InstallationId ??= Guid.NewGuid().ToString("D");
    }

    public void Disable()
    {
        HasChoice = true;
        Enabled = false;
    }

    private static string? NormalizeId(string? installationId)
    {
        if (string.IsNullOrWhiteSpace(installationId))
            return null;
        var trimmed = installationId.Trim();
        return Guid.TryParse(trimmed, out var parsed) ? parsed.ToString("D") : null;
    }
}
