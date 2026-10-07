using System.Globalization;
using MCP4Rhino.Logic.Models;

namespace MCP4Rhino.Logic;

public sealed class TakeoffRow
{
    public required string Level { get; init; }
    public required string Kind { get; init; }
    public double Count { get; init; }
    public double Area { get; init; }
    public double Length { get; init; }
}

public static class QuantityTakeoff
{
    public static IReadOnlyList<TakeoffRow> Compute(
        IReadOnlyList<TaggedElement> elements,
        string? levelFilter = null,
        string? kindFilter = null)
    {
        var groups = new Dictionary<string, (double count, double area, double length)>(StringComparer.OrdinalIgnoreCase);
        foreach (var obj in elements)
        {
            if (!string.IsNullOrWhiteSpace(kindFilter) &&
                !string.Equals(obj.Kind, kindFilter, StringComparison.OrdinalIgnoreCase))
                continue;
            var level = obj.Tags.GetValueOrDefault("mcp4:level") ?? "(none)";
            if (!string.IsNullOrWhiteSpace(levelFilter) &&
                !string.Equals(level, levelFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            var key = $"{level}|{obj.Kind}";
            groups.TryGetValue(key, out var agg);
            agg.count++;
            if (double.TryParse(obj.Tags.GetValueOrDefault("mcp4:area"), NumberStyles.Float, CultureInfo.InvariantCulture, out var area))
                agg.area += area;
            if (double.TryParse(obj.Tags.GetValueOrDefault("mcp4:height"), NumberStyles.Float, CultureInfo.InvariantCulture, out var len))
                agg.length += len;
            if (obj.BBox is { IsValid: true } bb && obj.Kind is "wall" or "beam")
                agg.length += Math.Max(bb.SizeX, bb.SizeY);
            groups[key] = agg;
        }

        return groups.Select(kv =>
        {
            var parts = kv.Key.Split('|');
            return new TakeoffRow
            {
                Level = parts[0],
                Kind = parts[1],
                Count = kv.Value.count,
                Area = kv.Value.area,
                Length = kv.Value.length,
            };
        }).ToArray();
    }
}

public sealed class ClashHit
{
    public required string A { get; init; }
    public required string B { get; init; }
    public required string Type { get; init; }
}

public static class BboxClash
{
    public static IReadOnlyList<ClashHit> Detect(
        IReadOnlyList<(string Id, BBox Box)> setA,
        IReadOnlyList<(string Id, BBox Box)> setB,
        double clearance = 0)
    {
        var clashes = new List<ClashHit>();
        foreach (var (ia, ba0) in setA)
        {
            var ba = clearance > 0 ? ba0.Inflate(clearance) : ba0;
            foreach (var (ib, bb0) in setB)
            {
                if (ia == ib) continue;
                var bb = clearance > 0 ? bb0.Inflate(clearance) : bb0;
                if (ba.Intersects(bb))
                    clashes.Add(new ClashHit { A = ia, B = ib, Type = clearance > 0 ? "soft" : "hard" });
            }
        }
        return clashes;
    }
}
