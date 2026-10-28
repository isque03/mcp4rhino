using MCP4Rhino.Logic.Grasshopper;
using Xunit;

namespace MCP4Rhino.Tests;

public class ComponentTrustTests
{
    private static readonly string NativeRoot = Path.Combine(Path.GetTempPath(), "Rhino", "Grasshopper");
    private static readonly string GhPath = Path.Combine(NativeRoot, "Grasshopper.dll");
    private const string Token = "dda4f5ec2cd80803";

    [Theory]
    [InlineData("Grasshopper")]
    [InlineData("CurveComponents")]
    [InlineData("SurfaceComponents")]
    [InlineData("MathComponents")]
    [InlineData("VectorComponents")]
    [InlineData("XformComponents")]
    [InlineData("FieldComponents")]
    [InlineData("TriangulationComponents")]
    public void KnownSignedModelingLibrariesUnderNativeRuntimeAreRecognized(string name) =>
        Assert.True(BuiltInComponentAssembly.Matches(name, Token, Path.Combine(NativeRoot, "Components", name + ".gha"), GhPath, Token));

    [Theory]
    [InlineData("IOComponents")]
    [InlineData("ScriptComponents")]
    [InlineData("GalapagosComponents")]
    [InlineData("ThirdParty")]
    public void OtherAssembliesNeverGetAutomaticOriginTrust(string name) =>
        Assert.False(BuiltInComponentAssembly.Matches(name, Token, Path.Combine(NativeRoot, name + ".gha"), GhPath, Token));

    [Fact]
    public void NamesAloneAndSimilarPathsDoNotGrantNativeTrust()
    {
        var native = Path.Combine(NativeRoot, "Components", "CurveComponents.gha");
        Assert.False(BuiltInComponentAssembly.Matches("CurveComponents", "", native, GhPath, ""));
        Assert.False(BuiltInComponentAssembly.Matches("CurveComponents", "other", native, GhPath, Token));
        Assert.False(BuiltInComponentAssembly.Matches("CurveComponents", Token, native, "Grasshopper.dll", Token));
        Assert.False(BuiltInComponentAssembly.Matches("CurveComponents", Token, "CurveComponents.gha", GhPath, Token));
        Assert.False(BuiltInComponentAssembly.Matches("CurveComponents", Token, Path.Combine(NativeRoot + "Other", "CurveComponents.gha"), GhPath, Token));
        Assert.False(BuiltInComponentAssembly.Matches("CurveComponents", Token, Path.Combine(NativeRoot, "..", "CurveComponents.gha"), GhPath, Token));
    }

    [Fact]
    public void SavedGrantsRoundTripAndMatchExactLibraryVersions()
    {
        var trust = ComponentTrust.Load("[\"Plugin, Version=1.0.0.0\",\"Plugin, Version=1.0.0.0\"]");
        Assert.Single(trust.Identities);
        Assert.True(ComponentTrust.Load(trust.ToJson()).Allows("Plugin, Version=1.0.0.0"));
        Assert.False(trust.Allows("Plugin, Version=2.0.0.0"));
    }

    [Fact]
    public void ApplyingVisibleChoicesPreservesOtherGrantsAndAllowsRevocation()
    {
        var initial = new ComponentTrust(["installed", "temporarily-uninstalled"]);
        var revoked = initial.ReplaceVisible(["installed", "new"], ["new", "not-shown"]);
        Assert.False(revoked.Allows("installed"));
        Assert.True(revoked.Allows("temporarily-uninstalled"));
        Assert.True(revoked.Allows("new"));
        Assert.False(revoked.Allows("not-shown"));
        Assert.True(initial.Allows("installed"));
    }
}
