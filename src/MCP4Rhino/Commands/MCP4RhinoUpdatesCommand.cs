using System.Diagnostics.CodeAnalysis;
using Rhino;
using Rhino.Commands;
using MCP4Rhino.Host;

namespace MCP4Rhino.Commands;

[ExcludeFromCodeCoverage]
[CommandStyle(Style.ScriptRunner)]
public class MCP4RhinoUpdatesCommand : Command
{
    public MCP4RhinoUpdatesCommand()
    {
        Instance = this;
    }

    public static MCP4RhinoUpdatesCommand Instance { get; private set; } = null!;

    public override string EnglishName => "MCP4RhinoUpdates";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        PluginLog.Info("Command MCP4RhinoUpdates invoked");
        StatusDialog.OpenPackageManager();
        return Result.Success;
    }
}
