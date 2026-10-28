using System.Text.Json.Nodes;

namespace MCP4Rhino.Contracts;

/// <summary>Persistent SDK boundary. No Grasshopper or hot-reloaded Logic types cross it.</summary>
public interface IGrasshopperRuntime : IDisposable
{
    string Call(string tool, JsonObject arguments);
    object WithPreview(JsonObject context, Func<object> capture);
    string ReadResource(JsonObject request);
    string ComponentLibraries();
}

public interface IGrasshopperComponentAuthorization
{
    bool IsTrustedAssembly(string assemblyIdentity);
}

public sealed record GrasshopperComponentLibrary(string AssemblyIdentity, string Name, string Version,
    int AvailableComponents, int RestrictedComponents);

public interface IGrasshopperDispatcher
{
    object Invoke(Func<object> work);
    void Post(Action work);
    void PostCancellation(Action work);
}
