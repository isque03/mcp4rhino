namespace MCP4Rhino.Logic;

/// <summary>Document-unit helpers for MCP tools (no Rhino native libs required).</summary>
public static class UnitConversion
{
    private static readonly Dictionary<string, DocumentUnitSystem> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mm"] = DocumentUnitSystem.Millimeters,
        ["millimeter"] = DocumentUnitSystem.Millimeters,
        ["millimeters"] = DocumentUnitSystem.Millimeters,
        ["cm"] = DocumentUnitSystem.Centimeters,
        ["centimeter"] = DocumentUnitSystem.Centimeters,
        ["centimeters"] = DocumentUnitSystem.Centimeters,
        ["m"] = DocumentUnitSystem.Meters,
        ["meter"] = DocumentUnitSystem.Meters,
        ["meters"] = DocumentUnitSystem.Meters,
        ["metre"] = DocumentUnitSystem.Meters,
        ["metres"] = DocumentUnitSystem.Meters,
        ["km"] = DocumentUnitSystem.Kilometers,
        ["kilometer"] = DocumentUnitSystem.Kilometers,
        ["kilometers"] = DocumentUnitSystem.Kilometers,
        ["in"] = DocumentUnitSystem.Inches,
        ["inch"] = DocumentUnitSystem.Inches,
        ["inches"] = DocumentUnitSystem.Inches,
        ["ft"] = DocumentUnitSystem.Feet,
        ["foot"] = DocumentUnitSystem.Feet,
        ["feet"] = DocumentUnitSystem.Feet,
        ["yd"] = DocumentUnitSystem.Yards,
        ["yard"] = DocumentUnitSystem.Yards,
        ["yards"] = DocumentUnitSystem.Yards,
        ["mi"] = DocumentUnitSystem.Miles,
        ["mile"] = DocumentUnitSystem.Miles,
        ["miles"] = DocumentUnitSystem.Miles,
        ["micron"] = DocumentUnitSystem.Microns,
        ["microns"] = DocumentUnitSystem.Microns,
        ["um"] = DocumentUnitSystem.Microns,
    };

    /// <summary>Meters per one unit of each allowed system (OpenNURBS / Rhino conventions).</summary>
    private static readonly Dictionary<DocumentUnitSystem, double> MetersPerUnit = new()
    {
        [DocumentUnitSystem.Microns] = 1e-6,
        [DocumentUnitSystem.Millimeters] = 1e-3,
        [DocumentUnitSystem.Centimeters] = 1e-2,
        [DocumentUnitSystem.Meters] = 1.0,
        [DocumentUnitSystem.Kilometers] = 1e3,
        [DocumentUnitSystem.Microinches] = 2.54e-8,
        [DocumentUnitSystem.Mils] = 2.54e-5,
        [DocumentUnitSystem.Inches] = 0.0254,
        [DocumentUnitSystem.Feet] = 0.3048,
        [DocumentUnitSystem.Yards] = 0.9144,
        [DocumentUnitSystem.Miles] = 1609.344,
        [DocumentUnitSystem.Angstroms] = 1e-10,
        [DocumentUnitSystem.Nanometers] = 1e-9,
        [DocumentUnitSystem.Decimeters] = 0.1,
        [DocumentUnitSystem.Dekameters] = 10.0,
        [DocumentUnitSystem.Hectometers] = 100.0,
        [DocumentUnitSystem.Megameters] = 1e6,
        [DocumentUnitSystem.Gigameters] = 1e9,
        [DocumentUnitSystem.AstronomicalUnits] = 149_597_870_700.0,
        [DocumentUnitSystem.LightYears] = 9.460_730_472_580_8e15,
        [DocumentUnitSystem.Parsecs] = 3.085_677_581_491_367e16,
        [DocumentUnitSystem.NauticalMiles] = 1852.0,
        [DocumentUnitSystem.PrinterPoints] = 0.0254 / 72.0,
        [DocumentUnitSystem.PrinterPicas] = 0.0254 / 6.0,
    };

    public static bool TryParseUnitSystem(string? s, out DocumentUnitSystem us)
    {
        us = DocumentUnitSystem.Unset;
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

    public static DocumentUnitSystem ParseUnitSystem(string s) =>
        TryParseUnitSystem(s, out var us)
            ? us
            : throw new ArgumentException($"Unknown or unsupported unit system: {s}");

    /// <summary>Scale factor to convert a length from <paramref name="from"/> into <paramref name="to"/>.</summary>
    public static double Scale(DocumentUnitSystem from, DocumentUnitSystem to)
    {
        if (!MetersPerUnit.TryGetValue(from, out var fromM) || !MetersPerUnit.TryGetValue(to, out var toM))
            throw new ArgumentException($"Unsupported unit scale: {from} → {to}");
        return fromM / toM;
    }

    /// <summary>Convert <paramref name="value"/> in <paramref name="from"/> into <paramref name="to"/>.</summary>
    public static double Convert(double value, DocumentUnitSystem from, DocumentUnitSystem to) => value * Scale(from, to);

    /// <summary>Convert a length in a named unit into document model units.</summary>
    public static double ToModelUnits(double value, string fromUnit, DocumentUnitSystem model) =>
        Convert(value, ParseUnitSystem(fromUnit), model);

    /// <summary>Convert a length in document model units into a named unit.</summary>
    public static double FromModelUnits(double modelValue, string toUnit, DocumentUnitSystem model) =>
        Convert(modelValue, model, ParseUnitSystem(toUnit));

    public readonly record struct LengthBreakdown(
        double Model,
        string UnitSystem,
        double Meters,
        double Millimeters,
        double Inches,
        double Feet);

    public static LengthBreakdown Breakdown(double modelValue, DocumentUnitSystem model) => new(
        Model: modelValue,
        UnitSystem: model.ToString(),
        Meters: Convert(modelValue, model, DocumentUnitSystem.Meters),
        Millimeters: Convert(modelValue, model, DocumentUnitSystem.Millimeters),
        Inches: Convert(modelValue, model, DocumentUnitSystem.Inches),
        Feet: Convert(modelValue, model, DocumentUnitSystem.Feet));

    /// <summary>Anonymous-object form with camelCase-friendly names for MCP JSON.</summary>
    public static object BreakdownObject(double modelValue, DocumentUnitSystem model)
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

    public static object DocumentFactorsJson(DocumentUnitSystem model) => new
    {
        unit_system = model.ToString(),
        units_per_meter = Scale(DocumentUnitSystem.Meters, model),
        model_units_per_inch = Scale(DocumentUnitSystem.Inches, model),
        model_units_per_foot = Scale(DocumentUnitSystem.Feet, model),
        model_units_per_mm = Scale(DocumentUnitSystem.Millimeters, model),
        inches_per_model_unit = Scale(model, DocumentUnitSystem.Inches),
        feet_per_model_unit = Scale(model, DocumentUnitSystem.Feet),
        mm_per_model_unit = Scale(model, DocumentUnitSystem.Millimeters),
        meters_per_model_unit = Scale(model, DocumentUnitSystem.Meters),
    };
}
