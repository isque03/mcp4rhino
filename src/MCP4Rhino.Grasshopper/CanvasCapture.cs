using System.Drawing;
using System.Drawing.Imaging;
using System.Reflection;
using System.Text.Json.Nodes;
using Grasshopper.GUI.Canvas;
using Grasshopper.Kernel;
using MCP4Rhino.Logic.Grasshopper;
using GH = Grasshopper.Instances;

namespace MCP4Rhino.Grasshopper;

internal static class CanvasCapture
{
    public static object Capture(DefinitionSession session, JsonObject args)
    {
        session.RequireContext(); session.Identity.Require(args);
        var frame = CanvasFrame.Parse(args);
        foreach (var node in session.Document.Objects) node.Attributes.PerformLayout();
        var region = Region(session.Document, frame);
        var visible = frame.VisibleRegion(region);
        var outside = session.Document.Objects.Count(node => !new RectangleF((float)visible.X, (float)visible.Y,
            (float)visible.Width, (float)visible.Height).Contains(node.Attributes.Bounds));
        var path = Destination(args);
        try
        {
            Render(session.Document, frame, region, path, GhJson.Flag(args, "overwrite"));
            session.Identity.Require(args);
            return new { path, width = frame.Width, height = frame.Height,
                requested_width = frame.Width, requested_height = frame.Height,
                mime_type = "image/png", method = "native_canvas_tile", framing = frame.Framing,
                rectangle = visible, requested_rectangle = region, zoom = frame.Zoom(region),
                objects_outside_frame = outside, truncated = outside > 0, identity = session.Identity };
        }
        catch (GhException) { throw; }
        catch (Exception ex)
        {
            var cause = ex.InnerException ?? ex;
            throw new GhException("canvas_capture_failed", $"Native canvas rendering is unavailable: {cause.Message}", "capture");
        }
    }

    public static CanvasRectangle Region(GH_Document document, CanvasFrame frame)
    {
        if (frame.Rectangle is { } rectangle) return rectangle;
        if (frame.Framing == "fit") return Contents(document);
        if (GH.ActiveCanvas is not { } canvas || !ReferenceEquals(canvas.Document, document))
            throw new GhException("canvas_not_active", "Current framing requires the requested document active. Use framing:fit or rectangle.", "capture");
        var region = canvas.Viewport.VisibleRegion;
        return new(region.X, region.Y, region.Width, region.Height);
    }

    public static CanvasRectangle Contents(GH_Document document)
    {
        if (document.Objects.Count == 0) return new(0, 0, 800, 500);
        foreach (var node in document.Objects) node.Attributes.PerformLayout();
        var bounds = document.Objects.Select(n => n.Attributes.Bounds).Aggregate(RectangleF.Union);
        return new(bounds.X - 40, bounds.Y - 40, Math.Max(1, bounds.Width) + 80, Math.Max(1, bounds.Height) + 80);
    }

    private static void Render(GH_Document document, CanvasFrame frame, CanvasRectangle region, string path, bool overwrite)
    {
        var viewport = new GH_Viewport { Width = frame.Width, Height = frame.Height, Zoom = (float)frame.Zoom(region) };
        viewport.Target = new Point((int)Math.Round(frame.Width / 2d - (region.X + region.Width / 2) * viewport.Zoom),
            (int)Math.Round(frame.Height / 2d - (region.Y + region.Height / 2) * viewport.Zoom));
        viewport.ComputeProjection();
        // A detached native canvas avoids switching documents or capturing editor/tutorial overlays.
        // Reflection avoids a compile dependency on Windows Forms; Rhino supplies the platform UI implementation.
        var canvas = (GH_Canvas)Activator.CreateInstance(typeof(GH_Canvas))!;
        var documentField = typeof(GH_Canvas).GetField("_document", BindingFlags.Instance | BindingFlags.NonPublic);
        try
        {
            // The public Document setter enables/solves/promotes documents and autosaves on detach.
            // Bind only the render context on this disposable canvas; fail closed if this SDK detail changes.
            if (documentField?.FieldType != typeof(GH_Document))
                throw new GhException("canvas_capture_unavailable", "The installed native canvas cannot bind a read-only render context.", "capture");
            documentField.SetValue(canvas, document);
            using var drawingProbe = new Bitmap(1, 1);
            var render = typeof(GH_Canvas).GetMethod("GenerateHiResImageTile", new[] { typeof(GH_Viewport), typeof(Color) }) ??
                throw new GhException("canvas_capture_unavailable", "The installed Grasshopper does not expose native canvas rendering.", "capture");
            using var bitmap = (Bitmap?)render.Invoke(canvas, new object[] { viewport, Color.White });
            if (bitmap is null || bitmap.Width != frame.Width || bitmap.Height != frame.Height)
                throw new GhException("unsupported_dimensions", "The native renderer did not produce the requested dimensions.", "capture");
            using var output = new FileStream(path, overwrite ? FileMode.Create : FileMode.CreateNew, FileAccess.Write);
            bitmap.Save(output, ImageFormat.Png);
        }
        finally { documentField?.SetValue(canvas, null); ((IDisposable)(object)canvas).Dispose(); }
    }

    private static string Destination(JsonObject args)
    {
        var path = args["path"]?.GetValue<string>() ?? Path.Combine(Path.GetTempPath(), "MCP4Rhino", $"canvas-{Guid.NewGuid():N}.png");
        path = Path.GetFullPath(path);
        if (!Path.GetExtension(path).Equals(".png", StringComparison.OrdinalIgnoreCase))
            throw new GhException("invalid_path", "Canvas captures require a .png path.", "capture");
        if (File.Exists(path) && !GhJson.Flag(args, "overwrite"))
            throw new GhException("file_exists", "Set overwrite:true to replace an existing capture.", "capture");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return path;
    }
}
