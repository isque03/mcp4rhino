using System.Diagnostics.CodeAnalysis;
using Rhino;
using Rhino.Commands;
using MCP4Rhino.Host;

namespace MCP4Rhino.Commands;

/// <summary>
/// Always opens the Status dialog. Used by Tools → MCP4Rhino → Status… because RUI
/// macros invoke commands as <see cref="RunMode.Scripted"/> (so <c>MCP4RhinoStatus</c>
/// correctly skips the modal for true scripts).
/// </summary>
[ExcludeFromCodeCoverage]
public class MCP4RhinoStatusUICommand : Command
{
    public MCP4RhinoStatusUICommand()
    {
        Instance = this;
    }

    public static MCP4RhinoStatusUICommand Instance { get; private set; } = null!;

    public override string EnglishName => "MCP4RhinoStatusUI";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        PluginLog.Info("Command MCP4RhinoStatusUI invoked");
        McpHostBootstrap.WriteStatus();
        try
        {
            StatusDialog.ShowStatus();
        }
        catch (Exception ex)
        {
            PluginLog.Info($"Status dialog failed: {ex.Message}");
            RhinoApp.WriteLine($"MCP4Rhino: status dialog unavailable ({ex.Message}). See command-line summary above.");
            return Result.Failure;
        }
        return Result.Success;
    }
}
