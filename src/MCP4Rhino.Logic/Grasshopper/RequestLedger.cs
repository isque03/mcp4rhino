using System.Text.Json.Nodes;

namespace MCP4Rhino.Logic.Grasshopper;

/// <summary>Mutation receipts survive tool reload. The caller serializes mutations.</summary>
public sealed class RequestLedger
{
    private readonly Dictionary<string, (JsonNode Arguments, string Result)> _receipts = new();

    public string Execute(string tool, JsonObject arguments, Func<string> mutation)
    {
        var key = tool + ":" + GhJson.Text(arguments, "request_id");
        if (_receipts.TryGetValue(key, out var previous))
        {
            if (!JsonNode.DeepEquals(previous.Arguments, arguments))
                throw new GhException("request_conflict", "request_id was already used with different arguments.");
            return previous.Result;
        }
        if (_receipts.Count >= 10000)
            throw new GhException("limit_exceeded", "Session receipt limit reached. Save and reopen the definition.");
        var result = mutation();
        _receipts.Add(key, (arguments.DeepClone(), result));
        return result;
    }
}
