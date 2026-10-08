using Rhino;
using Rhino.ApplicationSettings;

namespace MCP4Rhino.Logic;

/// <summary>Rhino license / evaluation status for MCP save gating.</summary>
public sealed record RhinoLicenseStatus(
    bool CanSave,
    string InstallationType,
    string InstallationTypeString,
    bool IsEvaluation,
    int? DaysUntilExpiration,
    int? SavesLeft,
    string Message)
{
    public const string AllowedMessage =
        "Rhino allows saving. A valid license or active evaluation is in effect.";

    public const string DeniedMessage =
        "A valid Rhino license or a non-expired evaluation is required to save. " +
        "Rhino currently reports that saving is not allowed (expired evaluation, expired lease, or inactive license seat).";

    /// <summary>Build status from already-resolved fields (unit-testable).</summary>
    public static RhinoLicenseStatus FromSnapshot(
        bool canSave,
        Installation installationType,
        string? installationTypeString = null,
        int? daysUntilExpiration = null,
        int? savesLeft = null)
    {
        var isEval = RhinoApp.IsInstallationEvaluation(installationType);
        return new RhinoLicenseStatus(
            CanSave: canSave,
            InstallationType: installationType.ToString(),
            InstallationTypeString: installationTypeString ?? installationType.ToString(),
            IsEvaluation: isEval,
            DaysUntilExpiration: daysUntilExpiration,
            SavesLeft: savesLeft,
            Message: canSave ? AllowedMessage : DeniedMessage);
    }

    /// <summary>Query live RhinoApp license state.</summary>
    public static RhinoLicenseStatus Query()
    {
        var type = RhinoApp.InstallationType;
        int? days = null;

        try
        {
            if (RhinoApp.LicenseExpires)
                days = RhinoApp.DaysUntilExpiration;
        }
        catch
        {
            // InvalidLicenseTypeException or similar when days are not applicable.
        }

        // RhinoCommon 8.21 (net7.0 ref) does not expose LicenseSavesLeft; leave null.

        return FromSnapshot(
            RhinoApp.CanSave,
            type,
            RhinoApp.InstallationTypeString,
            days,
            savesLeft: null);
    }

    public object ToJsonObject() => new
    {
        can_save = CanSave,
        installation_type = InstallationType,
        installation_type_string = InstallationTypeString,
        is_evaluation = IsEvaluation,
        days_until_expiration = DaysUntilExpiration,
        saves_left = SavesLeft,
        message = Message,
    };
}
