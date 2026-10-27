using System.Collections.Concurrent;

namespace MCP4Rhino.Contracts;

/// <summary>Opaque IDs shared by the host and collectible tool assemblies for this process lifetime.</summary>
public static class DocumentResourceIds
{
    private static readonly ConcurrentDictionary<uint, string> Ids = new();
    public static string InstanceId { get; } = Guid.NewGuid().ToString();
    public static string For(uint serial) => Ids.GetOrAdd(serial, _ => Guid.NewGuid().ToString());
    public static bool Matches(string id, uint activeSerial) => For(activeSerial) == id;
}
