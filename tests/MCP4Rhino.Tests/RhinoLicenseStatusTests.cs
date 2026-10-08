using MCP4Rhino.Logic;
using Rhino.ApplicationSettings;
using Xunit;

namespace MCP4Rhino.Tests;

public class RhinoLicenseStatusTests
{
    [Fact]
    public void FromSnapshot_CanSave_UsesAllowedMessage()
    {
        var status = RhinoLicenseStatus.FromSnapshot(
            canSave: true,
            installationType: Installation.Commercial,
            installationTypeString: "Commercial",
            daysUntilExpiration: null,
            savesLeft: null);

        Assert.True(status.CanSave);
        Assert.False(status.IsEvaluation);
        Assert.Equal("Commercial", status.InstallationType);
        Assert.Equal("Commercial", status.InstallationTypeString);
        Assert.Null(status.DaysUntilExpiration);
        Assert.Null(status.SavesLeft);
        Assert.Equal(RhinoLicenseStatus.AllowedMessage, status.Message);
    }

    [Fact]
    public void FromSnapshot_CannotSave_UsesDeniedMessage()
    {
        var status = RhinoLicenseStatus.FromSnapshot(
            canSave: false,
            installationType: Installation.EvaluationTimed,
            installationTypeString: "Evaluation",
            daysUntilExpiration: 0,
            savesLeft: null);

        Assert.False(status.CanSave);
        Assert.True(status.IsEvaluation);
        Assert.Equal(0, status.DaysUntilExpiration);
        Assert.Equal(RhinoLicenseStatus.DeniedMessage, status.Message);
        Assert.Contains("valid Rhino license", status.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("non-expired evaluation", status.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(Installation.Evaluation, true)]
    [InlineData(Installation.EvaluationTimed, true)]
    [InlineData(Installation.Commercial, false)]
    [InlineData(Installation.Educational, false)]
    [InlineData(Installation.Beta, false)]
    public void FromSnapshot_IsEvaluation_MatchesInstallation(Installation type, bool expectedEval)
    {
        var status = RhinoLicenseStatus.FromSnapshot(canSave: true, installationType: type);
        Assert.Equal(expectedEval, status.IsEvaluation);
        Assert.Equal(type.ToString(), status.InstallationType);
    }

    [Fact]
    public void FromSnapshot_IncludesOptionalEvalFields()
    {
        var status = RhinoLicenseStatus.FromSnapshot(
            canSave: true,
            installationType: Installation.Evaluation,
            daysUntilExpiration: 12,
            savesLeft: 7);

        Assert.Equal(12, status.DaysUntilExpiration);
        Assert.Equal(7, status.SavesLeft);
        Assert.True(status.IsEvaluation);
    }

    [Fact]
    public void ToJsonObject_UsesSnakeCaseKeys()
    {
        var status = RhinoLicenseStatus.FromSnapshot(
            canSave: false,
            installationType: Installation.EvaluationTimed,
            installationTypeString: "Evaluation Timed",
            daysUntilExpiration: 0,
            savesLeft: null);

        var json = status.ToJsonObject();
        var props = json.GetType().GetProperties();
        var names = props.Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("can_save", names);
        Assert.Contains("installation_type", names);
        Assert.Contains("installation_type_string", names);
        Assert.Contains("is_evaluation", names);
        Assert.Contains("days_until_expiration", names);
        Assert.Contains("saves_left", names);
        Assert.Contains("message", names);

        Assert.Equal(false, props.First(p => p.Name == "can_save").GetValue(json));
        Assert.Equal(RhinoLicenseStatus.DeniedMessage, props.First(p => p.Name == "message").GetValue(json));
    }
}
