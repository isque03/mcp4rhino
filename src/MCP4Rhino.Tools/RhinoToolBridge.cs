using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using MCP4Rhino.Contracts;

namespace MCP4Rhino.Tools;

[ExcludeFromCodeCoverage]
public sealed class RhinoToolBridge : IMcpToolBridge
{
    public object[] ListTools() => RhinoToolCatalog.ListTools();

    public object CallTool(JsonNode? @params) => RhinoToolCatalog.CallTool(@params);
}
