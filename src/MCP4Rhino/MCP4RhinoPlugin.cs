using System.Reflection;
using Rhino.PlugIns;
using MCP4Rhino.Host;

namespace MCP4Rhino;

/// <summary>
/// MCP4Rhino — Rhino plugin embedding a local MCP HTTP server for AI agents.
/// </summary>
public class MCP4RhinoPlugin : PlugIn
{
    public MCP4RhinoPlugin()
    {
        Instance = this;
    }

    public static MCP4RhinoPlugin Instance { get; private set; } = null!;

    // WhenNeeded: avoid loading ASP.NET/Roslyn deps during Rhino startup (can beach-ball on Mac).
    public override PlugInLoadTime LoadTime => PlugInLoadTime.WhenNeeded;

    protected override LoadReturnCode OnLoad(ref string errorMessage)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.4.0";
        PluginLog.Info($"OnLoad LoadTime=WhenNeeded version={version} log={PluginLog.LogPath}");
        Rhino.RhinoApp.WriteLine($"MCP4Rhino {version} loaded. MCP4Rhino / MCP4RhinoReload / tool mcp4rhino_reload.");
        return LoadReturnCode.Success;
    }

    protected override void OnShutdown()
    {
        try
        {
            PluginLog.Info("OnShutdown");
            McpHost.Stop();
        }
        catch
        {
            // ignore shutdown races
        }
        base.OnShutdown();
    }
}
