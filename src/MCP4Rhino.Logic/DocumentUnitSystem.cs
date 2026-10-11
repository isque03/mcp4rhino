namespace MCP4Rhino.Logic;

/// <summary>
/// Document length units matching <c>Rhino.UnitSystem</c> ordinals so Tools can cast
/// <c>doc.ModelUnitSystem</c> without Logic loading RhinoCommon at test time.
/// </summary>
public enum DocumentUnitSystem : byte
{
    None = 0,
    Microns = 1,
    Millimeters = 2,
    Centimeters = 3,
    Meters = 4,
    Kilometers = 5,
    Microinches = 6,
    Mils = 7,
    Inches = 8,
    Feet = 9,
    Miles = 10,
    CustomUnits = 11,
    Angstroms = 12,
    Nanometers = 13,
    Decimeters = 14,
    Dekameters = 15,
    Hectometers = 16,
    Megameters = 17,
    Gigameters = 18,
    Yards = 19,
    PrinterPoints = 20,
    PrinterPicas = 21,
    NauticalMiles = 22,
    AstronomicalUnits = 23,
    LightYears = 24,
    Parsecs = 25,
    Unset = 255,
}
