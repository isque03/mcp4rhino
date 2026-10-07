namespace MCP4Rhino.Logic;

/// <summary>Pure helpers for surface edge/face pick, continuity, loft options, and replace commit lists (no RhinoDoc).</summary>
public static class SurfaceOps
{
    public enum Continuity
    {
        Position = 0,
        Tangency = 1,
        Curvature = 2,
    }

    public enum LoftStyle
    {
        Normal = 0,
        Loose = 1,
        Straight = 2,
        Tight = 3,
        Uniform = 4,
    }

    /// <summary>Parse continuity: position|g0|tangency|g1|curvature|g2 (case-insensitive).</summary>
    public static Continuity ParseContinuity(string? value, Continuity fallback = Continuity.Tangency)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return value.Trim().ToLowerInvariant() switch
        {
            "position" or "g0" or "0" => Continuity.Position,
            "tangency" or "tangent" or "g1" or "1" => Continuity.Tangency,
            "curvature" or "g2" or "2" => Continuity.Curvature,
            _ => throw new ArgumentException(
                $"Unknown continuity '{value}' (use position|tangency|curvature)."),
        };
    }

    /// <summary>Parse loft style: normal|loose|straight|tight|uniform.</summary>
    public static LoftStyle ParseLoftStyle(string? value, LoftStyle fallback = LoftStyle.Normal)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return value.Trim().ToLowerInvariant() switch
        {
            "normal" => LoftStyle.Normal,
            "loose" => LoftStyle.Loose,
            "straight" or "straightsections" => LoftStyle.Straight,
            "tight" => LoftStyle.Tight,
            "uniform" => LoftStyle.Uniform,
            _ => throw new ArgumentException(
                $"Unknown loft style '{value}' (use normal|loose|straight|tight|uniform)."),
        };
    }

    /// <summary>
    /// Select edge index by nearest distance. Throws on empty, far, or ambiguous picks.
    /// </summary>
    public static int SelectEdgeIndex(
        IReadOnlyList<double> distances,
        double ambiguityTol,
        double maxDistance = double.PositiveInfinity) =>
        CurveOps.SelectUnambiguousNearest(distances, ambiguityTol, maxDistance);

    /// <summary>
    /// Select face index by nearest distance. Throws on empty, far, or ambiguous picks.
    /// </summary>
    public static int SelectFaceIndex(
        IReadOnlyList<double> distances,
        double ambiguityTol,
        double maxDistance = double.PositiveInfinity) =>
        CurveOps.SelectUnambiguousNearest(distances, ambiguityTol, maxDistance);

    /// <summary>Validate edge_index is in [0, count).</summary>
    public static int RequireEdgeIndex(int edgeIndex, int edgeCount)
    {
        if (edgeCount <= 0) throw new ArgumentException("Surface has no edges.");
        if (edgeIndex < 0 || edgeIndex >= edgeCount)
            throw new ArgumentException($"edge_index {edgeIndex} out of range [0, {edgeCount}).");
        return edgeIndex;
    }

    /// <summary>Validate face_index is in [0, count).</summary>
    public static int RequireFaceIndex(int faceIndex, int faceCount)
    {
        if (faceCount <= 0) throw new ArgumentException("Surface has no faces.");
        if (faceIndex < 0 || faceIndex >= faceCount)
            throw new ArgumentException($"face_index {faceIndex} out of range [0, {faceCount}).");
        return faceIndex;
    }

    /// <summary>
    /// Curve cutters for trim/split must be planar. Call with <c>curve.TryGetPlane(...)</c> result.
    /// </summary>
    public static void RequirePlanarCutter(bool tryGetPlaneSucceeded)
    {
        if (!tryGetPlaneSucceeded)
            throw new ArgumentException(
                "Curve cutter must be planar (or pass a surface/brep cutter).");
    }

    /// <summary>
    /// Ordered replacement pieces for fillet/chamfer trim: primary strips then trimmed A then trimmed B.
    /// Empty primary throws; null trim arrays are skipped.
    /// </summary>
    public static IReadOnlyList<T> CollectTrimReplacePieces<T>(
        IReadOnlyList<T>? primary,
        IReadOnlyList<T>? trimA,
        IReadOnlyList<T>? trimB)
    {
        if (primary is null || primary.Count == 0)
            throw new ArgumentException("Fillet/chamfer produced no primary surfaces.");
        var list = new List<T>(primary.Count + (trimA?.Count ?? 0) + (trimB?.Count ?? 0));
        list.AddRange(primary);
        if (trimA is { Count: > 0 }) list.AddRange(trimA);
        if (trimB is { Count: > 0 }) list.AddRange(trimB);
        return list;
    }

    /// <summary>Require a positive radius/distance.</summary>
    public static double RequirePositive(double value, string name)
    {
        if (value <= 0) throw new ArgumentException($"{name} must be positive.");
        return value;
    }

    /// <summary>Require at least <paramref name="min"/> curve ids.</summary>
    public static void RequireCount(int count, int min, string what)
    {
        if (count < min) throw new ArgumentException($"{what} needs at least {min} id(s); got {count}.");
    }
}
