using System.Drawing;
using Rhino.Display;

namespace MCP4Rhino.Tools;

/// <summary>A separate display preset; never modifies Rhino's built-in Shaded mode.</summary>
internal static class IllustrationDisplay
{
    private const string Name = "MCP4Rhino Illustration";

    public static void Apply(RhinoViewport viewport)
    {
        var mode = DisplayModeDescription.FindByName(Name);
        if (mode is null)
        {
            var id = DisplayModeDescription.CopyDisplayMode(DisplayModeDescription.ShadedId, Name);
            mode = DisplayModeDescription.GetDisplayMode(id)
                ?? throw new InvalidOperationException("Could not create illustration display mode.");
        }
        Configure(mode.DisplayAttributes);
        // Rhino can return false when an existing preset has no changed settings.
        DisplayModeDescription.UpdateDisplayMode(mode);
        viewport.DisplayMode = mode;
        viewport.ConstructionGridVisible = false;
        viewport.ConstructionAxesVisible = false;
        viewport.WorldAxesVisible = false;
    }

    private static void Configure(DisplayPipelineAttributes attributes)
    {
        attributes.SetFill(Color.FromArgb(177, 207, 223), Color.FromArgb(239, 246, 248));
        attributes.ShadingEnabled = true;
        attributes.LightingScheme = DisplayPipelineAttributes.LightingSchema.None;
        attributes.UseCustomObjectColor = false;
        attributes.SurfaceEdgeColorUsage = DisplayPipelineAttributes.SurfaceEdgeColorUse.SingleColorForAll;
        attributes.SurfaceNakedEdgeColorUsage = DisplayPipelineAttributes.SurfaceNakedEdgeColorUse.SingleColorForAll;
        attributes.ShowIsoCurves = false;
        attributes.ShowSurfaceEdges = true;
        attributes.SurfaceEdgeColor = Color.FromArgb(67, 77, 76);
        attributes.SurfaceNakedEdgeColor = Color.FromArgb(67, 77, 76);
        attributes.SurfaceEdgeThickness = 1;
        attributes.SurfaceNakedEdgeThickness = 1;
        attributes.UseSingleCurveColor = true;
        attributes.CurveColor = Color.FromArgb(77, 86, 82);
        attributes.ShadowsOn = false;
    }
}
