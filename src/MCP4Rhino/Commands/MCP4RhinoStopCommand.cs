using Rhino;
using Rhino.Commands;
using MCP4Rhino.Host;

namespace MCP4Rhino.Commands;

[CommandStyle(Style.ScriptRunner)]
public class MCP4RhinoStopCommand : Command
{
    public MCP4RhinoStopCommand()
    {
        Instance = this;
    }

    public static MCP4RhinoStopCommand Instance { get; private set; } = null!;

    public override string EnglishName => "MCP4RhinoStop";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        PluginLog.Info("Command MCP4RhinoStop invoked");
        if (!McpHost.IsRunning)
        {
            PluginLog.Info("MCP server is not running");
            return Result.Nothing;
        }

        McpHost.Stop();
        PluginLog.Info("MCP server stopped");
        return Result.Success;
    }
}
