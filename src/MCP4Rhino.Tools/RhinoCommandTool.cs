using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Contracts;
using MCP4Rhino.Host;
using MCP4Rhino.Logic;
using MCP4Rhino.Logic.Telemetry;
using Rhino;
using static MCP4Rhino.Tools.ToolHelpers;

namespace MCP4Rhino.Tools;

internal static class RhinoCommandTool
{
    public static object Call(JsonObject args) => UiThread.Invoke(() =>
    {
        var doc = RequireDoc();
        var script = Str(args, "script") ?? throw new ArgumentException("script required");
        var commandName = RhinoCommandName.Resolve(script,
            Rhino.Commands.Command.LookupCommandId, Rhino.Commands.Command.LookupCommandName);
        var before = doc.Objects.Count;
        var ok = RhinoApp.RunScript(script, Bool(args, "echo"));
        doc.Views.Redraw();
        var text = JsonSerializer.Serialize(new { ok, script, object_count_before = before, object_count_after = doc.Objects.Count });
        return new ToolCallResult(McpContent.TextOnly(text), new(RhinoCommandName: commandName));
    });
}
