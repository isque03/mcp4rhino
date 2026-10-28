using System.Text.Json.Nodes;
using Grasshopper.GUI.Base;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Undo;
using MCP4Rhino.Logic.Grasshopper;

namespace MCP4Rhino.Grasshopper;

internal static class SliderPatch
{
    public static object Apply(DefinitionSession session, JsonArray operations)
    {
        var before = new Dictionary<Guid, SliderState>();
        var after = SliderState.Plan(operations, id =>
        {
            var slider = RequireSlider(GraphEditor.Node(session.Document, id));
            before.TryAdd(slider.InstanceGuid, Read(slider));
            return Read(slider);
        }).ToDictionary(pair => Guid.Parse(pair.Key), pair => pair.Value);
        using var suppression = new SolutionSuppression();
        try
        {
            Restore(session, after);
            session.Document.UndoServer.PushUndoRecord("MCP Grasshopper slider patch", new SliderUndo(session, before, after));
            session.Document.Modified();
            return new { aliases = new Dictionary<string, string>() };
        }
        catch
        {
            try { Restore(session, before); }
            catch { session.Faulted = true; }
            throw;
        }
    }

    internal static GH_NumberSlider RequireSlider(IGH_DocumentObject node) => node is GH_NumberSlider slider &&
        ComponentCatalog.Supported(node) ? slider : throw new GhException("invalid_value", "set_slider requires a verified Number Slider.");

    internal static SliderState Read(GH_NumberSlider slider) => new(slider.Slider.Value,
        slider.Slider.Minimum, slider.Slider.Maximum, Enum.Parse<SliderAccuracy>(slider.Slider.Type.ToString()), slider.Slider.DecimalPlaces);

    internal static void Write(GH_NumberSlider slider, SliderState state)
    {
        // Widen first so disjoint old/new ranges cannot clamp the restored value.
        slider.Slider.Minimum = Math.Min(slider.Slider.Minimum, state.Minimum);
        slider.Slider.Maximum = Math.Max(slider.Slider.Maximum, state.Maximum);
        slider.Slider.Type = Enum.Parse<GH_SliderAccuracy>(state.Accuracy.ToString());
        slider.Slider.DecimalPlaces = state.DecimalPlaces;
        slider.SetSliderValue(state.Value);
        slider.Slider.Minimum = state.Minimum; slider.Slider.Maximum = state.Maximum;
        slider.ExpireSolution(false);
    }

    internal static void Restore(DefinitionSession session, IReadOnlyDictionary<Guid, SliderState> states)
    {
        using var suppression = new SolutionSuppression();
        // Resolve every target before changing any control, including during undo/redo.
        var targets = states.Select(pair => (slider: RequireSlider(GraphEditor.Node(session.Document, pair.Key.ToString())), pair.Value)).ToArray();
        session.Muting = true;
        try { foreach (var (slider, state) in targets) Write(slider, state); }
        finally { session.Muting = false; session.Changed(); global::Grasshopper.Instances.InvalidateCanvas(); }
    }
}

internal sealed class SliderUndo(DefinitionSession session, IReadOnlyDictionary<Guid, SliderState> before,
    IReadOnlyDictionary<Guid, SliderState> after) : GH_UndoAction
{
    public override bool ExpiresDisplay => true;
    // GH runs this recalculation AFTER Internal_Undo/Redo returns, outside our suppression scope.
    public override bool ExpiresSolution => false;
    protected override void Internal_Undo(GH_Document doc) => SliderPatch.Restore(session, before);
    protected override void Internal_Redo(GH_Document doc) => SliderPatch.Restore(session, after);
}
