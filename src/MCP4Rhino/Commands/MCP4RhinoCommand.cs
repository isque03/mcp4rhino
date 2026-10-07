using Rhino;
using Rhino.Commands;
using MCP4Rhino.Host;

namespace MCP4Rhino.Commands;

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
        RhinoApp.WriteLine("MCP4Rhino: command received…");

        try
        {
            PluginLog.Info("Command MCP4Rhino invoked");

            if (McpHost.IsRunning)
            {
                RhinoApp.WriteLine($"MCP4Rhino: already running at {McpHost.Endpoint}");
                return Result.Success;
            }

            ToolLoader.EnsureLoaded();
            RhinoApp.WriteLine($"MCP4Rhino: tools ← {ToolLoader.LoadedFrom}");

            var port = McpHost.DefaultPort;
            var env = Environment.GetEnvironmentVariable("MCP4RHINO_PORT");
            if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env, out var parsed) && parsed is > 0 and < 65536)
                port = parsed;

            RhinoApp.WriteLine($"MCP4Rhino: starting HTTP MCP on port {port}…");
            if (!McpHost.Start(port))
            {
                RhinoApp.WriteLine("MCP4Rhino: FAILED to start (port in use or bind error). See ~/Library/Logs/MCP4Rhino/mcp4rhino.log");
                return Result.Failure;
            }

            RhinoApp.WriteLine($"MCP server started: http://localhost:{McpHost.Port}");
            RhinoApp.WriteLine($"Connect: npx mcp-remote {McpHost.Endpoint}");
            RhinoApp.WriteLine("Hot reload tools: MCP4RhinoReload (after rebuilding MCP4Rhino.Tools)");
            return Result.Success;
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"MCP4Rhino: exception: {ex.GetType().Name}: {ex.Message}");
            PluginLog.Info($"Command exception: {ex}");
            return Result.Failure;
        }
    }
}
