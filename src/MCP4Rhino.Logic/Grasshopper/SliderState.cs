using System.Text.Json.Nodes;

namespace MCP4Rhino.Logic.Grasshopper;

public enum SliderAccuracy { Float, Integer, Even, Odd }

/// <summary>Raw slider state; evaluated expressions are never used to restore a control.</summary>
public sealed record SliderState(decimal Value, decimal Minimum, decimal Maximum, SliderAccuracy Accuracy, int DecimalPlaces = 3)
{
    public SliderState Change(JsonObject operation)
    {
        var value = (decimal)GhJson.Number(operation["value"]);
        var min = operation["minimum"] is null ? Minimum : (decimal)GhJson.Number(operation["minimum"]);
        var max = operation["maximum"] is null ? Maximum : (decimal)GhJson.Number(operation["maximum"]);
        var accuracy = operation["integer"] is null ? Accuracy :
            GhJson.Flag(operation, "integer") ? SliderAccuracy.Integer : SliderAccuracy.Float;
        if (min > max || value < min || value > max ||
            accuracy != SliderAccuracy.Float && value != decimal.Truncate(value) ||
            accuracy == SliderAccuracy.Even && value % 2 != 0 ||
            accuracy == SliderAccuracy.Odd && value % 2 == 0)
            throw new GhException("invalid_value", "Slider value must fit its bounds and accuracy.", "design");
        return new(value, min, max, accuracy, DecimalPlaces);
    }

    public static bool IsControlPatch(JsonArray operations) => operations.Count > 0 &&
        operations.OfType<JsonObject>().Count() == operations.Count &&
        operations.OfType<JsonObject>().All(op => op["op"]?.GetValue<string>() == "set_slider");

    public static IReadOnlyDictionary<string, SliderState> Plan(JsonArray operations, Func<string, SliderState> read)
    {
        var states = new Dictionary<string, SliderState>();
        foreach (var op in operations.OfType<JsonObject>())
        {
            var id = GhJson.Text(op, "node");
            if (Guid.TryParse(id, out var guid)) id = guid.ToString();
            states[id] = (states.GetValueOrDefault(id) ?? read(id)).Change(op);
        }
        return states;
    }
}
