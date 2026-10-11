using System.Diagnostics.CodeAnalysis;
using Rhino;
using Rhino.Commands;
using MCP4Rhino.Host;

namespace MCP4Rhino.Commands;

[ExcludeFromCodeCoverage]
[CommandStyle(Style.ScriptRunner)]
public class MCP4RhinoCommand : Command
{
    public MCP4RhinoCommand()
    {
        Instance = this;
    }

    public static MCP4RhinoCommand Instance { get; private set; } = null!;

    public override string EnglishName => "MCP4Rhino";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        PluginLog.Info("Command MCP4Rhino invoked");
        return McpHostBootstrap.TryStart(quiet: false) ? Result.Success : Result.Failure;
    }
}
