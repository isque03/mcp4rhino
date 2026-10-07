using System.Diagnostics.CodeAnalysis;
using Rhino;
using Rhino.Commands;
using MCP4Rhino.Host;

namespace MCP4Rhino.Commands;

[ExcludeFromCodeCoverage]
[CommandStyle(Style.ScriptRunner)]
public class MCP4RhinoReloadCommand : Command
{
    public MCP4RhinoReloadCommand()
    {
        Instance = this;
    }

    public static MCP4RhinoReloadCommand Instance { get; private set; } = null!;

    public override string EnglishName => "MCP4RhinoReload";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        RhinoApp.WriteLine("MCP4Rhino: hot-reloading tools…");
        try
        {
            // Keep HTTP server running — only swap the collectible tools assembly.
            ToolLoader.Reload();
            RhinoApp.WriteLine($"MCP4Rhino: tools loaded from {ToolLoader.LoadedFrom}");
            if (McpHost.IsRunning)
                RhinoApp.WriteLine($"MCP4Rhino: MCP still on {McpHost.Endpoint} (no restart needed)");
            return Result.Success;
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"MCP4RhinoReload failed: {ex.Message}");
            PluginLog.Info($"Reload failed: {ex}");
            return Result.Failure;
        }
    }
}
