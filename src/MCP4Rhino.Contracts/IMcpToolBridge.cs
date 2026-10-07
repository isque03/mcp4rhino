using System.Text.Json.Nodes;

namespace MCP4Rhino.Contracts;

/// <summary>
/// Implemented by the hot-reloadable tools assembly.
/// </summary>
public interface IMcpToolBridge
{
    object[] ListTools();

    object CallTool(JsonNode? @params);
}
