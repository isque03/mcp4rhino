using MCP4Rhino.Logic;
using Xunit;

namespace MCP4Rhino.Tests;

public class UnitConversionTests
{
    [Theory]
    [InlineData("in", DocumentUnitSystem.Inches)]
    [InlineData("inch", DocumentUnitSystem.Inches)]
    [InlineData("inches", DocumentUnitSystem.Inches)]
    [InlineData("ft", DocumentUnitSystem.Feet)]
    [InlineData("Feet", DocumentUnitSystem.Feet)]
    [InlineData("mm", DocumentUnitSystem.Millimeters)]
    [InlineData("Millimeters", DocumentUnitSystem.Millimeters)]
    public void ParseUnitSystem_Aliases(string s, DocumentUnitSystem expected)
    {
        Assert.Equal(expected, UnitConversion.ParseUnitSystem(s));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1")]
    [InlineData("999")]
    [InlineData("furlong")]
    [InlineData("CustomUnits")]
    public void ParseUnitSystem_Rejected(string s)
    {
        Assert.Throws<ArgumentException>(() => UnitConversion.ParseUnitSystem(s));
        Assert.False(UnitConversion.TryParseUnitSystem(s, out _));
    }

    [Fact]
    public void ToModelUnits_InchesIntoFeet()
    {
        var model = UnitConversion.ToModelUnits(32, "in", DocumentUnitSystem.Feet);
        Assert.Equal(32.0 / 12.0, model, precision: 12);
    }

    [Fact]
    public void FromModelUnits_FeetToInches()
    {
        var inches = UnitConversion.FromModelUnits(32.0 / 12.0, "in", DocumentUnitSystem.Feet);
        Assert.Equal(32.0, inches, precision: 12);
    }

    [Fact]
    public void Scale_InchesToFeet_IsOneTwelfth()
    {
        Assert.Equal(1.0 / 12.0, UnitConversion.Scale(DocumentUnitSystem.Inches, DocumentUnitSystem.Feet), precision: 12);
    }

    [Fact]
    public void Breakdown_FeetHeightMatchesInches()
    {
        var b = UnitConversion.Breakdown(32.0 / 12.0, DocumentUnitSystem.Feet);
        Assert.Equal(32.0, b.Inches, precision: 12);
        Assert.Equal("Feet", b.UnitSystem);
    }

    [Fact]
    public void DocumentFactors_Feet()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(UnitConversion.DocumentFactorsJson(DocumentUnitSystem.Feet));
        Assert.Contains("Feet", json);
        Assert.Contains("model_units_per_inch", json);
    }

    [Fact]
    public void Convert_InchesToMm_IndependentOfDocument()
    {
        var mm = UnitConversion.Convert(32, DocumentUnitSystem.Inches, DocumentUnitSystem.Millimeters);
        Assert.Equal(32 * 25.4, mm, precision: 10);
    }
}
