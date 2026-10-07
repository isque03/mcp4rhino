namespace MCP4Rhino.Logic;

/// <summary>Pure helpers for curve split/trim/join parameter handling (no RhinoDoc).</summary>
public static class CurveOps
{
    /// <summary>Sort and dedupe split parameters (exact equality).</summary>
    public static double[] NormalizeSplitParameters(IEnumerable<double> parameters)
    {
        if (parameters is null) throw new ArgumentNullException(nameof(parameters));
        var arr = parameters.Distinct().OrderBy(t => t).ToArray();
        if (arr.Length == 0) throw new ArgumentException("Split parameters must be non-empty.");
        return arr;
    }

    /// <summary>Return ordered domain [min, max].</summary>
    public static (double T0, double T1) OrderedDomain(double a, double b) =>
        a <= b ? (a, b) : (b, a);

    /// <summary>
    /// Index of the uniquely nearest segment by midpoint distance.
    /// Throws if empty, nearest distance &gt; <paramref name="maxDistance"/>, or the two nearest
    /// distances differ by ≤ <paramref name="ambiguityTol"/>.
    /// Pass <paramref name="maxDistance"/> as <see cref="double.PositiveInfinity"/> to skip the far check.
    /// </summary>
    public static int SelectUnambiguousNearest(
        IReadOnlyList<double> distances,
        double ambiguityTol,
        double maxDistance = double.PositiveInfinity)
    {
        if (distances is null || distances.Count == 0)
            throw new ArgumentException("No segments to choose from.");
        if (ambiguityTol < 0) throw new ArgumentException("ambiguityTol must be ≥ 0.");
        if (maxDistance < 0) throw new ArgumentException("maxDistance must be ≥ 0.");

        var ranked = distances
            .Select((d, i) => (d, i))
            .OrderBy(x => x.d)
            .ToList();

        if (ranked[0].d > maxDistance)
            throw new ArgumentException(
                "keep_point is too far from all trimmed segments; place it near the segment to keep.");

        if (ranked.Count >= 2 && Math.Abs(ranked[0].d - ranked[1].d) <= ambiguityTol)
            throw new ArgumentException(
                "Ambiguous keep_point: multiple segments are equally near; provide a clearer keep_point.");

        return ranked[0].i;
    }
}
