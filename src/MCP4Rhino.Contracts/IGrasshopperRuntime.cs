using System.Text.Json.Nodes;

namespace MCP4Rhino.Contracts;

/// <summary>Persistent SDK boundary. No Grasshopper or hot-reloaded Logic types cross it.</summary>
public interface IGrasshopperRuntime : IDisposable
{
    string Call(string tool, JsonObject arguments);
    object WithPreview(JsonObject context, Func<object> capture);
    string ReadResource(JsonObject request);
}

public interface IGrasshopperDispatcher
{
    object Invoke(Func<object> work);
    void Post(Action work);
    void PostCancellation(Action work);
}
