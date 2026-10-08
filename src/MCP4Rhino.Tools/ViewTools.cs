using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Host;
using Rhino;
using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace MCP4Rhino.Tools;

/// <summary>
/// Viewport inspect / switch / pan / orbit / zoom helpers for MCP tools.
/// </summary>
internal static class ViewTools
{
    public static string ListViews() => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var activeId = doc.Views.ActiveView?.ActiveViewport?.Id ?? Guid.Empty;
        var views = new List<object>();
        foreach (var view in doc.Views)
        {
            if (view?.ActiveViewport is null) continue;
            var vp = view.ActiveViewport;
            views.Add(new
            {
                name = vp.Name,
                active = vp.Id == activeId,
                projection = ProjectionName(vp),
                camera = PointArray(vp.CameraLocation),
                target = PointArray(vp.CameraTarget),
                up = VectorArray(vp.CameraUp),
            });
        }
        return JsonSerializer.Serialize(new { count = views.Count, views });
    });

    public static string GetView(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var (view, vp) = Resolve(doc, args);
        return JsonSerializer.Serialize(CameraState(view, vp));
    });

    public static string SetActiveView(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var name = args["view"]?.GetValue<string>()
            ?? throw new ArgumentException("view name is required");
        var (view, vp) = Resolve(doc, name);
        doc.Views.ActiveView = view;
        view.Redraw();
        return JsonSerializer.Serialize(CameraState(view, vp));
    });

    public static string SetIllustrationStyle(JsonObject args) => UiThread.Invoke(() =>
    {
        var (view, viewport) = Resolve(RequireDoc(), args);
        IllustrationDisplay.Apply(viewport);
        view.Redraw();
        return JsonSerializer.Serialize(new { view = viewport.Name, style = "MCP4Rhino Illustration" });
    });

    public static string SetView(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var (view, vp) = Resolve(doc, args);

        var camera = RequirePoint(args, "camera");
        var target = RequirePoint(args, "target");
        vp.SetCameraLocations(target, camera);

        if (TryPoint(args, "up", out var up) || TryVector(args, "up", out up))
        {
            var upVec = new Vector3d(up.X, up.Y, up.Z);
            if (upVec.Unitize())
                vp.CameraUp = upVec;
        }

        if (args["perspective"]?.GetValue<bool>() == true)
            vp.ChangeToPerspectiveProjection(true, 50);
        else if (args["perspective"]?.GetValue<bool>() == false)
            vp.ChangeToParallelProjection(true);

        view.Redraw();
        return JsonSerializer.Serialize(CameraState(view, vp));
    });

    public static string OrbitView(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var (view, vp) = Resolve(doc, args);

        var yawDeg = Num(args, "yaw", 0);
        var pitchDeg = Num(args, "pitch", 0);
        var target = vp.CameraTarget;

        if (Math.Abs(yawDeg) > 1e-9)
            vp.Rotate(RhinoMath.ToRadians(yawDeg), Vector3d.ZAxis, target);

        if (Math.Abs(pitchDeg) > 1e-9)
        {
            // Clamp pitch so we don't flip through the poles.
            var dir = vp.CameraLocation - target;
            var horiz = new Vector3d(dir.X, dir.Y, 0);
            var currentPitch = horiz.Length < 1e-9
                ? (dir.Z >= 0 ? 90.0 : -90.0)
                : RhinoMath.ToDegrees(Math.Atan2(dir.Z, horiz.Length));
            var nextPitch = Math.Clamp(currentPitch + pitchDeg, -89.0, 89.0);
            var delta = nextPitch - currentPitch;
            if (Math.Abs(delta) > 1e-9)
            {
                var right = vp.CameraX;
                if (!right.Unitize())
                    right = Vector3d.XAxis;
                vp.Rotate(RhinoMath.ToRadians(delta), right, target);
            }
        }

        view.Redraw();
        return JsonSerializer.Serialize(CameraState(view, vp));
    });

    public static string PanView(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var (view, vp) = Resolve(doc, args);

        var rightAmt = Num(args, "right", 0);
        var upAmt = Num(args, "up", 0);
        var right = vp.CameraX;
        var up = vp.CameraUp;
        if (!right.Unitize()) right = Vector3d.XAxis;
        if (!up.Unitize()) up = Vector3d.ZAxis;

        var delta = right * rightAmt + up * upAmt;
        var cam = vp.CameraLocation + delta;
        var tgt = vp.CameraTarget + delta;
        vp.SetCameraLocations(tgt, cam);
        view.Redraw();
        return JsonSerializer.Serialize(CameraState(view, vp));
    });

    public static string ZoomView(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var (view, vp) = Resolve(doc, args);

        var factor = Num(args, "factor", 1);
        if (factor <= 1e-6)
            throw new ArgumentException("factor must be > 0");

        // factor > 1 => zoom in (dolly toward target)
        var target = vp.CameraTarget;
        var cam = vp.CameraLocation;
        var dir = cam - target;
        var newCam = target + dir * (1.0 / factor);
        vp.SetCameraLocations(target, newCam);
        view.Redraw();
        return JsonSerializer.Serialize(CameraState(view, vp));
    });

    public static string ZoomExtents(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var (view, vp) = Resolve(doc, args);

        BoundingBox box = BoundingBox.Empty;
        var ids = args["ids"]?.GetValue<string>();
        var layer = args["layer"]?.GetValue<string>();

        if (!string.IsNullOrWhiteSpace(ids))
        {
            foreach (var id in ParseIds(ids))
            {
                var obj = doc.Objects.FindId(id);
                if (obj?.Geometry is null) continue;
                var b = obj.Geometry.GetBoundingBox(true);
                if (b.IsValid) box.Union(b);
            }
        }
        else
        {
            foreach (var obj in doc.Objects)
            {
                if (obj is null || obj.IsDeleted || obj.Geometry is null) continue;
                if (!string.IsNullOrWhiteSpace(layer))
                {
                    var layerName = doc.Layers[obj.Attributes.LayerIndex]?.FullPath ?? "";
                    if (layerName.IndexOf(layer, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }
                var b = obj.Geometry.GetBoundingBox(true);
                if (b.IsValid) box.Union(b);
            }
        }

        if (!box.IsValid)
        {
            // Empty doc — still succeed with current camera.
            view.Redraw();
            return JsonSerializer.Serialize(CameraState(view, vp));
        }

        vp.ZoomBoundingBox(box);
        view.Redraw();
        return JsonSerializer.Serialize(CameraState(view, vp));
    });

    private static object CameraState(RhinoView view, RhinoViewport vp) => new
    {
        name = vp.Name,
        active = ReferenceEquals(view, view.Document?.Views.ActiveView) ||
                 view.Document?.Views.ActiveView?.ActiveViewport?.Id == vp.Id,
        projection = ProjectionName(vp),
        camera = PointArray(vp.CameraLocation),
        target = PointArray(vp.CameraTarget),
        up = VectorArray(vp.CameraUp),
        direction = VectorArray(vp.CameraDirection),
        lens_length = vp.IsPerspectiveProjection ? vp.Camera35mmLensLength : (double?)null,
    };

    private static (RhinoView view, RhinoViewport vp) Resolve(RhinoDoc doc, JsonObject args)
    {
        var name = args["view"]?.GetValue<string>();
        return Resolve(doc, name);
    }

    private static (RhinoView view, RhinoViewport vp) Resolve(RhinoDoc doc, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            var active = doc.Views.ActiveView
                ?? throw new InvalidOperationException("No active view.");
            return (active, active.ActiveViewport);
        }

        foreach (var view in doc.Views)
        {
            if (view?.ActiveViewport is null) continue;
            if (string.Equals(view.ActiveViewport.Name, name, StringComparison.OrdinalIgnoreCase))
                return (view, view.ActiveViewport);
        }

        throw new ArgumentException($"View not found: {name}");
    }

    private static string ProjectionName(RhinoViewport vp) =>
        vp.IsPerspectiveProjection ? "perspective" :
        vp.IsParallelProjection ? "parallel" : "other";

    private static double[] PointArray(Point3d p) => [p.X, p.Y, p.Z];
    private static double[] VectorArray(Vector3d v) => [v.X, v.Y, v.Z];

    private static Point3d RequirePoint(JsonObject args, string key)
    {
        if (!TryPoint(args, key, out var p))
            throw new ArgumentException($"{key} is required as [x,y,z] or {{x,y,z}}");
        return p;
    }

    private static bool TryPoint(JsonObject args, string key, out Point3d point)
    {
        point = Point3d.Origin;
        var node = args[key];
        if (node is null) return false;

        if (node is JsonArray arr && arr.Count >= 3)
        {
            point = new Point3d(
                arr[0]!.GetValue<double>(),
                arr[1]!.GetValue<double>(),
                arr[2]!.GetValue<double>());
            return true;
        }

        if (node is JsonObject obj)
        {
            point = new Point3d(
                obj["x"]?.GetValue<double>() ?? obj["X"]?.GetValue<double>() ?? 0,
                obj["y"]?.GetValue<double>() ?? obj["Y"]?.GetValue<double>() ?? 0,
                obj["z"]?.GetValue<double>() ?? obj["Z"]?.GetValue<double>() ?? 0);
            return true;
        }

        return false;
    }

    private static bool TryVector(JsonObject args, string key, out Point3d point) =>
        TryPoint(args, key, out point);

    private static double Num(JsonObject args, string key, double fallback)
    {
        var n = args[key];
        return n is null ? fallback : n.GetValue<double>();
    }

    private static List<Guid> ParseIds(string ids)
    {
        var trimmed = ids.Trim();
        IEnumerable<string> parts = trimmed.StartsWith('[')
            ? JsonSerializer.Deserialize<string[]>(trimmed) ?? []
            : trimmed.Split([',', ';', '\n', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var result = new List<Guid>();
        foreach (var p in parts)
        {
            if (!Guid.TryParse(p, out var g))
                throw new ArgumentException($"Invalid GUID: {p}");
            result.Add(g);
        }
        return result;
    }

    private static RhinoDoc RequireDoc() =>
        RhinoDoc.ActiveDoc ?? throw new InvalidOperationException("No active Rhino document.");
}
