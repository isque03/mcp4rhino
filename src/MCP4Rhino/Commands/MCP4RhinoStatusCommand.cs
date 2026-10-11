using System.Diagnostics.CodeAnalysis;
using Rhino;
using Rhino.Commands;
using MCP4Rhino.Host;

namespace MCP4Rhino.Commands;

[ExcludeFromCodeCoverage]
[CommandStyle(Style.ScriptRunner)]
public class MCP4RhinoStatusCommand : Command
{
    public MCP4RhinoStatusCommand()
    {
        Instance = this;
    }

    public static MCP4RhinoStatusCommand Instance { get; private set; } = null!;

    public override string EnglishName => "MCP4RhinoStatus";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        PluginLog.Info("Command MCP4RhinoStatus invoked");
        McpHostBootstrap.WriteStatus();
        return Result.Success;
    }
}
