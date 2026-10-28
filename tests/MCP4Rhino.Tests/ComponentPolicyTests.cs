using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Logic.Grasshopper;
using Xunit;

namespace MCP4Rhino.Tests;

public class ComponentPolicyTests
{
    private const string AssemblyIdentity = "Plugin, Version=1.2.3.0, Culture=neutral, PublicKeyToken=null";
    private static ComponentIdentity Available(string type = "Plugin.Geometry.Component") => new("Plugin", AssemblyIdentity, type);
    private static ComponentPolicy Policy(string? json) => new(ComponentTrust.Load(json).Allows);

    [Fact]
    public void ManifestRemainsVerifiedWithoutTrustAndDisplayNamesGrantNothing()
    {
        var policy = new ComponentPolicy();
        var verified = policy.Classify(new("Grasshopper", "Grasshopper, Version=8.0.0.0", "Grasshopper.Kernel.Special.GH_NumberSlider"));
        Assert.Equal("verified", verified.SupportLevel);
        verified.Require();
        Assert.False(policy.Classify(Available("Grasshopper.Kernel.Special.GH_NumberSlider")).Authorized);
        Assert.NotEmpty(ComponentSupport.Catalog());
    }

    [Fact]
    public void ExactAssemblyGrantEnablesAvailableWithoutMakingItVerified()
    {
        var policy = Policy(JsonSerializer.Serialize(new[] { AssemblyIdentity }));
        var permission = policy.Classify(Available());
        Assert.Equal("available", permission.SupportLevel);
        Assert.True(permission.Authorized);
        permission.Require();
        Assert.False(policy.Classify(Available() with { AssemblyIdentity = AssemblyIdentity.Replace("1.2.3", "1.2.4") }).Authorized);
        Assert.False(Policy("[\"Plugin\"]").Classify(Available()).Authorized);
    }

    [Theory]
    [InlineData("ScriptComponents.CSharp")]
    [InlineData("RhinoCodePluginGH.PythonComponent")]
    [InlineData("Grasshopper.Kernel.Special.GH_Cluster")]
    [InlineData("Grasshopper.Kernel.Special.GH_Timer")]
    [InlineData("Native.Component_ReadFile")]
    [InlineData("Native.FileComponents.Reader")]
    [InlineData("Native.Component_FileWriter")]
    [InlineData("Plugin.Network.Client")]
    [InlineData("Plugin.HopsComponent")]
    public void KnownRiskCannotBeOverriddenByAssemblyTrust(string type)
    {
        var policy = Policy(JsonSerializer.Serialize(new[] { AssemblyIdentity }));
        var permission = policy.Classify(Available(type));
        Assert.Equal("restricted", permission.SupportLevel);
        Assert.False(permission.Authorized);
        Assert.Equal("restricted_component", Assert.Throws<GhException>(permission.Require).Code);
    }

    [Theory]
    [InlineData("Plugin.Geometry.Profile")]
    [InlineData("Grasshopper.Kernel.GH_InstanceDescription")]
    [InlineData("Grasshopper.Kernel.GH_DocumentObject")]
    [InlineData("Grasshopper.Kernel.GH_ActiveObject")]
    [InlineData("Grasshopper.Kernel.GH_Component")]
    public void OrdinaryTypesDoNotAccidentallyMatchRiskNames(string type) =>
        Assert.Equal("available", new ComponentPolicy().Classify(Available(type)).SupportLevel);

    [Theory]
    [InlineData("not-json")]
    [InlineData("[1]")]
    [InlineData("[null]")]
    [InlineData("[\"\"]")]
    [InlineData("null")]
    [InlineData("{}")]
    public void MalformedConfigurationFailsClosed(string configuration)
    {
        var policy = Policy(configuration);
        Assert.False(policy.Classify(Available()).Authorized);
    }

    [Fact]
    public void NoConfigurationOrEmptyGrantRequiresAuthorization()
    {
        foreach (var configuration in new string?[] { null, "", " ", "[]" })
        {
            var policy = Policy(configuration);
            var permission = policy.Classify(Available());
            Assert.Equal("available", permission.SupportLevel);
            Assert.Equal("component_authorization_required", Assert.Throws<GhException>(permission.Require).Code);
        }
    }

    [Fact]
    public void MissingUserObjectMetadataAndObsoleteTypesAreRestricted()
    {
        var policy = Policy(JsonSerializer.Serialize(new[] { AssemblyIdentity }));
        foreach (var identity in new[] { Available() with { Compiled = false }, Available() with { TypeName = "" },
            Available() with { AssemblyIdentity = "" }, Available() with { Obsolete = true } })
            Assert.Equal("restricted", policy.Classify(identity).SupportLevel);
    }

    [Fact]
    public void McpArgumentsCannotAuthorizeInstalledCode()
    {
        foreach (var tool in new[] { "gh_describe_components", "gh_solve", "gh_apply_patch" })
            Assert.Throws<GhException>(() => GhToolCatalog.Validate(tool, new JsonObject { ["allow_unverified"] = true }));
    }

    [Theory]
    [InlineData("CurveComponents", "CurveComponents.Component_Interpolate")]
    [InlineData("SurfaceComponents", "SurfaceComponents.Component_PipeVariable")]
    [InlineData("Grasshopper", "Grasshopper.Kernel.Special.GH_BooleanToggle")]
    [InlineData("Grasshopper", "Grasshopper.Kernel.Special.GH_CustomPreview")]
    public void BuiltInModelingTypesAreAvailableWithoutAnyUserConfiguration(string assembly, string type)
    {
        var identity = new ComponentIdentity(assembly, assembly + ", Version=8.36.26296.17002", type) { BuiltIn = true };
        var permission = new ComponentPolicy().Classify(identity);
        Assert.Equal("available", permission.SupportLevel);
        Assert.True(permission.Authorized);
    }

    [Fact]
    public void BuiltInOriginDoesNotOverrideKnownRestrictions()
    {
        var identity = new ComponentIdentity("Grasshopper", "Grasshopper, Version=8.36", "Grasshopper.Kernel.Special.GH_Timer") { BuiltIn = true };
        Assert.Equal("restricted", new ComponentPolicy(_ => true).Classify(identity).SupportLevel);
    }

    [Fact]
    public void GrantAndRevocationTakeEffectOnExistingPolicyWithoutRestart()
    {
        var trusted = new HashSet<string>(StringComparer.Ordinal);
        var policy = new ComponentPolicy(trusted.Contains);
        Assert.False(policy.Classify(Available()).Authorized);
        trusted.Add(AssemblyIdentity);
        Assert.True(policy.Classify(Available()).Authorized);
        trusted.Clear();
        var denied = policy.Classify(Available());
        Assert.False(denied.Authorized);
        Assert.Contains(ComponentPolicy.PreferencesCommand, denied.Reason);
        Assert.DoesNotContain("environment", denied.Reason!, StringComparison.OrdinalIgnoreCase);
    }
}
