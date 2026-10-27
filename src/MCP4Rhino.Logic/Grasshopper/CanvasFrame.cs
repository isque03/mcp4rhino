using System.Text.Json.Nodes;

namespace MCP4Rhino.Logic.Grasshopper;

public sealed record CanvasRectangle(double X, double Y, double Width, double Height)
{
    public void Validate()
    {
        if (!new[] { X, Y, Width, Height }.All(double.IsFinite) || Width <= 0 || Height <= 0 ||
            new[] { X, Y, Width, Height }.Any(v => Math.Abs(v) > 1e7))
            throw new GhException("invalid_arguments", "Canvas rectangle requires finite x/y and positive width/height within ±10000000.");
    }
}

public sealed record CanvasFrame(int Width, int Height, string Framing, CanvasRectangle? Rectangle)
{
    public static CanvasFrame Parse(JsonObject args)
    {
        var width = GhJson.Integer(args, "width", 1600); var height = GhJson.Integer(args, "height", 1000);
        if (width is < 64 or > 4096 || height is < 64 or > 4096)
            throw new GhException("invalid_arguments", "Capture width/height must be between 64 and 4096.");
        var framing = args["framing"]?.GetValue<string>() ?? "current";
        if (!new[] { "current", "fit", "rectangle" }.Contains(framing))
            throw new GhException("invalid_arguments", "Framing must be current, fit or rectangle.");
        CanvasRectangle? rectangle = null;
        if (args["rectangle"] is { } supplied)
        {
            var values = GhJson.Array(supplied).Select(GhJson.Number).ToArray();
            if (values.Length != 4 || framing != "rectangle")
                throw new GhException("invalid_arguments", "Supply [x,y,width,height] only with framing:rectangle.");
            rectangle = new(values[0], values[1], values[2], values[3]); rectangle.Validate();
        }
        if (framing == "rectangle" && rectangle is null)
            throw new GhException("invalid_arguments", "framing:rectangle requires rectangle:[x,y,width,height].");
        return new(width, height, framing, rectangle);
    }

    public double Zoom(CanvasRectangle region)
    {
        region.Validate();
        var zoom = Math.Min(Width / region.Width, Height / region.Height);
        if (!double.IsFinite(zoom) || zoom is < 0.000001 or > 100)
            throw new GhException("invalid_arguments", "Canvas framing requires a zoom between 0.000001 and 100. Request a larger rectangle.");
        return zoom;
    }

    public CanvasRectangle VisibleRegion(CanvasRectangle region)
    {
        var zoom = Zoom(region);
        return new(region.X + region.Width / 2 - Width / zoom / 2,
            region.Y + region.Height / 2 - Height / zoom / 2, Width / zoom, Height / zoom);
    }
}
