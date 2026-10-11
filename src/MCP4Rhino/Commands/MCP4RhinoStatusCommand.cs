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

        // Scripted / RUI-macro script runners must not block on a modal (F1).
        if (mode != RunMode.Interactive)
            return Result.Success;

        try
        {
            StatusDialog.ShowStatus();
        }
        catch (Exception ex)
        {
            PluginLog.Info($"Status dialog failed: {ex.Message}");
            RhinoApp.WriteLine($"MCP4Rhino: status dialog unavailable ({ex.Message}). See command-line summary above.");
        }
        return Result.Success;
    }
}
