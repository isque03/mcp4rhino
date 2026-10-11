using System.Reflection;
using Rhino;
using Rhino.PlugIns;
using MCP4Rhino.Host;

namespace MCP4Rhino;

/// <summary>
/// MCP4Rhino — Rhino plugin embedding a local MCP HTTP server for AI agents.
/// </summary>
public class MCP4RhinoPlugin : PlugIn
{
    private bool _idleHooked;

    public MCP4RhinoPlugin()
    {
        Instance = this;
    }

    public static MCP4RhinoPlugin Instance { get; private set; } = null!;

    // AtStartup so Idle auto-start can run without the user invoking a command first.
    // Actual HTTP bind is deferred to Idle to avoid beach-balling during Rhino launch.
    public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

    protected override LoadReturnCode OnLoad(ref string errorMessage)
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.5.1";
        PluginLog.Info($"OnLoad LoadTime=AtStartup version={version} log={PluginLog.LogPath} autostart={McpHostBootstrap.AutostartEnabled}");
        RhinoApp.WriteLine($"MCP4Rhino {version} loaded.");

        if (McpHostBootstrap.AutostartEnabled)
        {
            RhinoApp.WriteLine("MCP4Rhino: will start the AI connection when Rhino is idle…");
            HookIdleOnce();
        }
        else
        {
            RhinoApp.WriteLine("MCP4Rhino: auto-start off (MCP4RHINO_AUTOSTART). Run MCP4Rhino to start.");
        }

        return LoadReturnCode.Success;
    }

    private void HookIdleOnce()
    {
        if (_idleHooked) return;
        _idleHooked = true;
        RhinoApp.Idle += OnFirstIdle;
    }

    private void OnFirstIdle(object? sender, EventArgs e)
    {
        RhinoApp.Idle -= OnFirstIdle;
        _idleHooked = false;

        if (!McpHostBootstrap.AutostartEnabled || McpHost.IsRunning)
            return;

        PluginLog.Info("Idle auto-start begin");
        McpHostBootstrap.TryStart(quiet: false);
    }

    protected override void OnShutdown()
    {
        try
        {
            if (_idleHooked)
            {
                RhinoApp.Idle -= OnFirstIdle;
                _idleHooked = false;
            }
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
