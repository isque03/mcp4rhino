using MCP4Rhino.Logic;
using Rhino;
using Xunit;

namespace MCP4Rhino.Tests;

public class UnitConversionTests
{
    [Theory]
    [InlineData("in", UnitSystem.Inches)]
    [InlineData("inch", UnitSystem.Inches)]
    [InlineData("inches", UnitSystem.Inches)]
    [InlineData("ft", UnitSystem.Feet)]
    [InlineData("Feet", UnitSystem.Feet)]
    [InlineData("mm", UnitSystem.Millimeters)]
    [InlineData("Millimeters", UnitSystem.Millimeters)]
    public void ParseUnitSystem_Aliases(string s, UnitSystem expected)
    {
        Assert.Equal(expected, UnitConversion.ParseUnitSystem(s));
    }

    [Theory]
    [InlineData("furlongs")]
    [InlineData("None")]
    [InlineData("Unset")]
    [InlineData("CustomUnits")]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("-3")]
    public void ParseUnitSystem_Rejected(string s)
    {
        Assert.Throws<ArgumentException>(() => UnitConversion.ParseUnitSystem(s));
        Assert.False(UnitConversion.TryParseUnitSystem(s, out _));
    }

    [Fact]
    public void ToModelUnits_InchesIntoFeet()
    {
        var model = UnitConversion.ToModelUnits(32, "in", UnitSystem.Feet);
        Assert.Equal(32.0 / 12.0, model, precision: 10);
    }

    [Fact]
    public void FromModelUnits_FeetToInches()
    {
        var inches = UnitConversion.FromModelUnits(32.0 / 12.0, "in", UnitSystem.Feet);
        Assert.Equal(32.0, inches, precision: 10);
    }

    [Fact]
    public void Scale_InchesToFeet_IsOneTwelfth()
    {
        Assert.Equal(1.0 / 12.0, UnitConversion.Scale(UnitSystem.Inches, UnitSystem.Feet), precision: 12);
    }

    [Fact]
    public void Breakdown_FeetHeightMatchesInches()
    {
        var b = UnitConversion.Breakdown(32.0 / 12.0, UnitSystem.Feet);
        Assert.Equal(32.0, b.Inches, precision: 10);
        Assert.Equal(32.0 / 12.0, b.Feet, precision: 10);
        Assert.Equal(0.8128, b.Meters, precision: 6);
    }

    [Fact]
    public void DocumentFactors_Feet()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(UnitConversion.DocumentFactorsJson(UnitSystem.Feet));
        Assert.Contains("model_units_per_inch", json);
        Assert.Contains("\"inches_per_model_unit\":12", json.Replace(" ", ""));
    }

    [Fact]
    public void Convert_InchesToMm_IndependentOfDocument()
    {
        var mm = UnitConversion.Convert(32, UnitSystem.Inches, UnitSystem.Millimeters);
        Assert.Equal(812.8, mm, precision: 6);
    }
}
