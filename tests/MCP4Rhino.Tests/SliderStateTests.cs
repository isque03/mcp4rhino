using System.Text.Json.Nodes;
using MCP4Rhino.Logic.Grasshopper;
using Xunit;

namespace MCP4Rhino.Tests;

public class SliderStateTests
{
    private static JsonObject Op(string json) => JsonNode.Parse(json)!.AsObject();
    private static SliderState Initial => new(4, 0, 10, SliderAccuracy.Float);

    [Fact]
    public void RepeatedControlsUsePreviousPlannedBoundsAndDoNotMutateInputs()
    {
        var original = Initial;
        var reads = 0;
        var operations = JsonNode.Parse("""
            [{"op":"set_slider","node":"a","value":20,"minimum":15,"maximum":30},
            {"op":"set_slider","node":"a","value":25}]
            """)!.AsArray();
        var plan = SliderState.Plan(operations, _ => { reads++; return original; });
        Assert.Equal(1, reads);
        Assert.Equal(new SliderState(25, 15, 30, SliderAccuracy.Float), plan["a"]);
        Assert.Equal(Initial, original);
    }

    [Fact]
    public void InvalidLaterControlLeavesAllOriginalStatesUnchanged()
    {
        var originals = new Dictionary<string, SliderState> { ["a"] = Initial, ["b"] = Initial };
        var operations = JsonNode.Parse("""
            [{"op":"set_slider","node":"a","value":6},
            {"op":"set_slider","node":"b","value":100}]
            """)!.AsArray();
        Assert.Throws<GhException>(() => SliderState.Plan(operations, id => originals[id]));
        Assert.All(originals.Values, state => Assert.Equal(Initial, state));
    }

    [Theory]
    [InlineData("{\"value\":11}")]
    [InlineData("{\"value\":-1}")]
    [InlineData("{\"value\":5,\"minimum\":9,\"maximum\":8}")]
    [InlineData("{\"value\":2.5,\"integer\":true}")]
    public void InvalidBoundsAndAccuracyFailBeforeAssignment(string json) =>
        Assert.Equal("invalid_value", Assert.Throws<GhException>(() => Initial.Change(Op(json))).Code);

    [Theory]
    [InlineData(SliderAccuracy.Float, 2.5)]
    [InlineData(SliderAccuracy.Integer, 3)]
    [InlineData(SliderAccuracy.Even, 4)]
    [InlineData(SliderAccuracy.Odd, 5)]
    public void OmittedIntegerRetainsCompleteAccuracy(SliderAccuracy accuracy, double value)
    {
        var state = Initial with { Accuracy = accuracy };
        Assert.Equal(accuracy, state.Change(new JsonObject { ["value"] = value }).Accuracy);
        Assert.Equal(SliderAccuracy.Float, state.Change(Op("""{"value":2.5,"integer":false}""")).Accuracy);
        Assert.Equal(SliderAccuracy.Integer, state.Change(Op("""{"value":3,"integer":true}""")).Accuracy);
    }

    [Theory]
    [InlineData(SliderAccuracy.Even, 3)]
    [InlineData(SliderAccuracy.Odd, 4)]
    [InlineData(SliderAccuracy.Integer, 2.5)]
    public void NonstandardAccuracyIsValidated(SliderAccuracy accuracy, double value) =>
        Assert.Throws<GhException>(() => (Initial with { Accuracy = accuracy }).Change(new JsonObject { ["value"] = value }));

    [Fact]
    public void ControlFastPathRequiresOnlyExplicitSliderOperations()
    {
        Assert.True(SliderState.IsControlPatch(JsonNode.Parse("""
            [{"op":"set_slider","node":"n","value":1}]
            """)!.AsArray()));
        Assert.False(SliderState.IsControlPatch(new JsonArray()));
        Assert.False(SliderState.IsControlPatch(new JsonArray(1)));
        Assert.False(SliderState.IsControlPatch(JsonNode.Parse("""
            [{"op":"set_slider"},{"op":"remove"}]
            """)!.AsArray()));
    }

    [Fact]
    public void RequestRetryDoesNotApplyControlPlanTwice()
    {
        var ledger = new RequestLedger(); var writes = 0;
        var args = Op("""{"request_id":"slider-change","expected_revision":7,"operations":[{"op":"set_slider","node":"a","value":5}]}""");
        string Apply() { SliderState.Plan(args["operations"]!.AsArray(), _ => Initial); writes++; return "revision-8"; }
        Assert.Equal("revision-8", ledger.Execute("gh_apply_patch", args, Apply));
        Assert.Equal("revision-8", ledger.Execute("gh_apply_patch", (JsonObject)args.DeepClone(), Apply));
        Assert.Equal(1, writes);
    }

    [Fact]
    public void GuidSpellingDoesNotSplitRepeatedControlState()
    {
        var id = Guid.NewGuid().ToString(); var reads = 0;
        var operations = new JsonArray(new JsonObject { ["node"] = id.ToUpperInvariant(), ["value"] = 20, ["maximum"] = 30 },
            new JsonObject { ["node"] = id, ["value"] = 25 });
        var plan = SliderState.Plan(operations, _ => { reads++; return Initial; });
        Assert.Equal(1, reads);
        Assert.Equal(25, plan[id].Value);
    }

    [Fact]
    public void ChangingAccuracyRetainsCapturedPrecision()
    {
        var original = Initial with { DecimalPlaces = 7 };
        Assert.Equal(7, original.Change(Op("""{"value":3,"integer":true}""")).DecimalPlaces);
        Assert.Equal(7, original.Change(Op("""{"value":3.1234567,"integer":false}""")).DecimalPlaces);
    }
}
