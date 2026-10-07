using Rhino;

namespace MCP4Rhino.Logic;

/// <summary>Document-unit helpers for MCP tools (no Rhino native libs required).</summary>
public static class UnitConversion
{
    private static readonly Dictionary<string, UnitSystem> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mm"] = UnitSystem.Millimeters,
        ["millimeter"] = UnitSystem.Millimeters,
        ["millimeters"] = UnitSystem.Millimeters,
        ["cm"] = UnitSystem.Centimeters,
        ["centimeter"] = UnitSystem.Centimeters,
        ["centimeters"] = UnitSystem.Centimeters,
        ["m"] = UnitSystem.Meters,
        ["meter"] = UnitSystem.Meters,
        ["meters"] = UnitSystem.Meters,
        ["metre"] = UnitSystem.Meters,
        ["metres"] = UnitSystem.Meters,
        ["km"] = UnitSystem.Kilometers,
        ["kilometer"] = UnitSystem.Kilometers,
        ["kilometers"] = UnitSystem.Kilometers,
        ["in"] = UnitSystem.Inches,
        ["inch"] = UnitSystem.Inches,
        ["inches"] = UnitSystem.Inches,
        ["ft"] = UnitSystem.Feet,
        ["foot"] = UnitSystem.Feet,
        ["feet"] = UnitSystem.Feet,
        ["yd"] = UnitSystem.Yards,
        ["yard"] = UnitSystem.Yards,
        ["yards"] = UnitSystem.Yards,
        ["mi"] = UnitSystem.Miles,
        ["mile"] = UnitSystem.Miles,
        ["miles"] = UnitSystem.Miles,
        ["micron"] = UnitSystem.Microns,
        ["microns"] = UnitSystem.Microns,
        ["um"] = UnitSystem.Microns,
    };

    /// <summary>Meters per one unit of each allowed system (OpenNURBS / Rhino conventions).</summary>
    private static readonly Dictionary<UnitSystem, double> MetersPerUnit = new()
    {
        [UnitSystem.Microns] = 1e-6,
        [UnitSystem.Millimeters] = 1e-3,
        [UnitSystem.Centimeters] = 1e-2,
        [UnitSystem.Meters] = 1.0,
        [UnitSystem.Kilometers] = 1e3,
        [UnitSystem.Microinches] = 2.54e-8,
        [UnitSystem.Mils] = 2.54e-5,
        [UnitSystem.Inches] = 0.0254,
        [UnitSystem.Feet] = 0.3048,
        [UnitSystem.Yards] = 0.9144,
        [UnitSystem.Miles] = 1609.344,
        [UnitSystem.Angstroms] = 1e-10,
        [UnitSystem.Nanometers] = 1e-9,
        [UnitSystem.Decimeters] = 0.1,
        [UnitSystem.Dekameters] = 10.0,
        [UnitSystem.Hectometers] = 100.0,
        [UnitSystem.Megameters] = 1e6,
        [UnitSystem.Gigameters] = 1e9,
        [UnitSystem.AstronomicalUnits] = 149_597_870_700.0,
        [UnitSystem.LightYears] = 9.460_730_472_580_8e15,
        [UnitSystem.Parsecs] = 3.085_677_581_491_367e16,
        [UnitSystem.NauticalMiles] = 1852.0,
        [UnitSystem.PrinterPoints] = 0.0254 / 72.0,
        [UnitSystem.PrinterPicas] = 0.0254 / 6.0,
    };

    public static bool TryParseUnitSystem(string? s, out UnitSystem us)
    {
        us = UnitSystem.Unset;
        if (string.IsNullOrWhiteSpace(s)) return false;
        var t = s.Trim();
        // Reject numeric enum ordinals ("1" → Microns) — force named aliases or enum names.
        if (t.Length > 0 && t.All(c => char.IsDigit(c) || c == '-' || c == '+'))
            return false;
        if (Aliases.TryGetValue(t, out us))
            return MetersPerUnit.ContainsKey(us);
        if (!Enum.TryParse(t, ignoreCase: true, out us))
            return false;
        return MetersPerUnit.ContainsKey(us);
    }

    public static UnitSystem ParseUnitSystem(string s) =>
        TryParseUnitSystem(s, out var us)
            ? us
            : throw new ArgumentException($"Unknown or unsupported unit system: {s}");

    /// <summary>Scale factor to convert a length from <paramref name="from"/> into <paramref name="to"/>.</summary>
    public static double Scale(UnitSystem from, UnitSystem to)
    {
        if (!MetersPerUnit.TryGetValue(from, out var fromM) || !MetersPerUnit.TryGetValue(to, out var toM))
            throw new ArgumentException($"Unsupported unit scale: {from} → {to}");
        return fromM / toM;
    }

    /// <summary>Convert <paramref name="value"/> in <paramref name="from"/> into <paramref name="to"/>.</summary>
    public static double Convert(double value, UnitSystem from, UnitSystem to) => value * Scale(from, to);

    /// <summary>Convert a length in a named unit into document model units.</summary>
    public static double ToModelUnits(double value, string fromUnit, UnitSystem model) =>
        Convert(value, ParseUnitSystem(fromUnit), model);

    /// <summary>Convert a length in document model units into a named unit.</summary>
    public static double FromModelUnits(double modelValue, string toUnit, UnitSystem model) =>
        Convert(modelValue, model, ParseUnitSystem(toUnit));

    public readonly record struct LengthBreakdown(
        double Model,
        string UnitSystem,
        double Meters,
        double Millimeters,
        double Inches,
        double Feet);

    public static LengthBreakdown Breakdown(double modelValue, UnitSystem model) => new(
        Model: modelValue,
        UnitSystem: model.ToString(),
        Meters: Convert(modelValue, model, UnitSystem.Meters),
        Millimeters: Convert(modelValue, model, UnitSystem.Millimeters),
        Inches: Convert(modelValue, model, UnitSystem.Inches),
        Feet: Convert(modelValue, model, UnitSystem.Feet));

    /// <summary>Anonymous-object form with camelCase-friendly names for MCP JSON.</summary>
    public static object BreakdownObject(double modelValue, UnitSystem model)
    {
        var b = Breakdown(modelValue, model);
        return new
        {
            model = b.Model,
            unit_system = b.UnitSystem,
            m = b.Meters,
            mm = b.Millimeters,
            inches = b.Inches,
            feet = b.Feet,
        };
    }

    public static object DocumentFactorsJson(UnitSystem model) => new
    {
        unit_system = model.ToString(),
        units_per_meter = Scale(UnitSystem.Meters, model),
        model_units_per_inch = Scale(UnitSystem.Inches, model),
        model_units_per_foot = Scale(UnitSystem.Feet, model),
        model_units_per_mm = Scale(UnitSystem.Millimeters, model),
        inches_per_model_unit = Scale(model, UnitSystem.Inches),
        feet_per_model_unit = Scale(model, UnitSystem.Feet),
        mm_per_model_unit = Scale(model, UnitSystem.Millimeters),
        meters_per_model_unit = Scale(model, UnitSystem.Meters),
    };
}
