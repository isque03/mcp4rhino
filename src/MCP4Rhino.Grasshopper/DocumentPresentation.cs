using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using MCP4Rhino.Logic.Grasshopper;
using GH = Grasshopper.Instances;

namespace MCP4Rhino.Grasshopper;

internal static class DocumentPresentation
{
    public static GH_Canvas Activate(GH_Document document)
    {
        var canvas = GH.ActiveCanvas ?? throw new GhException("editor_unavailable", "No native canvas is available.", "capture");
        using var suppression = new SolutionSuppression();
        var enabled = document.Enabled;
        try
        {
            // Document assignment otherwise implicitly enables and solves even inspection-only definitions.
            canvas.Document = document;
            return canvas;
        }
        finally { document.Enabled = enabled; }
    }
}
