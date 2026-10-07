using System.Text.Json.Serialization;

namespace MCP4Rhino.Logic.Models;

public sealed class TaggedElement
{
    public required string Id { get; init; }
    public string? Name { get; init; }
    public required string Kind { get; init; }
    public Dictionary<string, string> Tags { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public BBox? BBox { get; init; }
}

public readonly record struct BBox(double MinX, double MinY, double MinZ, double MaxX, double MaxY, double MaxZ)
{
    public bool IsValid => MaxX >= MinX && MaxY >= MinY && MaxZ >= MinZ;

    public BBox Inflate(double amount) => new(
        MinX - amount, MinY - amount, MinZ - amount,
        MaxX + amount, MaxY + amount, MaxZ + amount);

    public bool Intersects(BBox other) =>
        MinX <= other.MaxX && MaxX >= other.MinX &&
        MinY <= other.MaxY && MaxY >= other.MinY &&
        MinZ <= other.MaxZ && MaxZ >= other.MinZ;

    public double SizeX => MaxX - MinX;
    public double SizeY => MaxY - MinY;
    public double SizeZ => MaxZ - MinZ;
}

public sealed class LevelInfo
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("elevation")]
    public double Elevation { get; init; }
}
