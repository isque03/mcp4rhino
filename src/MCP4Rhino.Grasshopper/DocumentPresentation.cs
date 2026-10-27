using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using System.Reflection;
using MCP4Rhino.Logic.Grasshopper;
using GH = Grasshopper.Instances;

namespace MCP4Rhino.Grasshopper;

internal static class DocumentPresentation
{
    public static GH_Canvas Activate(GH_Document document)
    {
        var canvas = GH.ActiveCanvas ?? throw new GhException("editor_unavailable", "No native canvas is available.", "capture");
        // The public EnableSolutions setter itself starts a solution when restored to true.
        // Preserve the raw gate without solver events; abort before touching the view on an incompatible SDK.
        var gate = typeof(GH_Document).GetField("m_enableSolutions", BindingFlags.Static | BindingFlags.NonPublic);
        if (gate?.FieldType != typeof(bool))
            throw new GhException("editor_unavailable", "This Grasshopper version cannot activate a document without an implicit solve.", "capture");
        var solutions = (bool)gate.GetValue(null)!;
        var enabled = document.Enabled;
        try
        {
            // Document assignment otherwise implicitly enables and solves even inspection-only definitions.
            gate.SetValue(null, false);
            canvas.Document = document;
            return canvas;
        }
        finally { document.Enabled = enabled; gate.SetValue(null, solutions); }
    }
}
