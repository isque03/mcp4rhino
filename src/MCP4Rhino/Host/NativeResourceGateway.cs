using System.Text.Json.Nodes;
using MCP4Rhino.Contracts;
using MCP4Rhino.Logic.Protocol;
using MCP4Rhino.Logic.Resources;
using Rhino;

namespace MCP4Rhino.Host;

/// <summary>Only read interfaces are reachable here. One pending native read, with a bounded HTTP wait.</summary>
internal sealed class NativeResourceGateway : IMcpResourceGateway
{
    private readonly SemaphoreSlim _pending = new(1, 1);

    public JsonObject Read(ResourceAddress address)
    {
        if (!_pending.Wait(0)) return Busy();
        var expired = 0;
        var task = Task.Run(() =>
        {
            try { return UiThread.Invoke(() => Volatile.Read(ref expired) == 0 ? ReadOnUi(address) : Busy()); }
            finally { _pending.Release(); }
        });
        _ = task.ContinueWith(failed => _ = failed.Exception, TaskContinuationOptions.OnlyOnFaulted);
        if (Task.WhenAny(task, Task.Delay(500)).GetAwaiter().GetResult() != task)
        { Interlocked.Exchange(ref expired, 1); return Busy(); }
        return task.GetAwaiter().GetResult();
    }

    private static JsonObject ReadOnUi(ResourceAddress address)
    {
        string json;
        if (address.Area == "server")
            return new JsonObject { ["status"] = "available", ["instance_id"] = DocumentResourceIds.InstanceId,
                ["rhino_version"] = RhinoApp.Version.ToString(), ["transport"] = "local HTTP POST; JSON response mode",
                ["grasshopper"] = GrasshopperHost.ResourceStatus() };
        if (address.Area == "grasshopper") json = GrasshopperHost.ReadResource(address.Arguments);
        else if (ToolLoader.Bridge is { } bridge) json = bridge.ReadResource(address.Arguments);
        else return new JsonObject { ["status"] = "unavailable", ["reason"] = "tools_not_loaded", ["next_tool"] = "get_document_info" };
        var data = JsonNode.Parse(json)!.AsObject();
        if (data["error"] is { } error)
            throw new McpRpcException(error["code"]!.GetValue<int>(), error["message"]!.GetValue<string>());
        return data;
    }

    private static JsonObject Busy() => new() { ["status"] = "busy", ["reason"] = "native_read_pending_or_ui_unavailable", ["retry"] = "Read the resource again after Rhino is responsive." };
}
