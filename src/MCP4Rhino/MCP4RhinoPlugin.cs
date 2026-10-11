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
        var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.5.2";
        PluginLog.Info($"OnLoad LoadTime=AtStartup version={version} log={PluginLog.LogPath} autostart={McpHostBootstrap.AutostartEnabled}");
        RhinoApp.WriteLine($"MCP4Rhino {version} loaded.");

        TryLoadRui();

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

    private static void TryLoadRui()
    {
        try
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(dir))
            {
                RhinoApp.WriteLine("MCP4Rhino: Tools menu unavailable (plugin path unknown). Use MCP4RhinoStatus / MCP4Rhino commands.");
                return;
            }

            var ruiPath = Path.Combine(dir, "MCP4Rhino.rui");
            if (!File.Exists(ruiPath))
            {
                RhinoApp.WriteLine(
                    "MCP4Rhino: MCP4Rhino.rui missing next to plugin; Tools → MCP4Rhino unavailable. " +
                    "Use MCP4RhinoStatus / MCP4Rhino commands.");
                PluginLog.Info($"RUI missing at {ruiPath}");
                return;
            }

            var byPath = RhinoApp.ToolbarFiles.FindByPath(ruiPath);
            if (byPath is not null)
            {
                PluginLog.Info($"RUI already loaded from package path {ruiPath}");
                return;
            }

            var byName = RhinoApp.ToolbarFiles.FindByName("MCP4Rhino", ignoreCase: true);
            if (byName is not null)
            {
                var existing = byName.Path ?? "";
                if (!string.Equals(existing, ruiPath, StringComparison.OrdinalIgnoreCase))
                {
                    RhinoApp.WriteLine(
                        $"MCP4Rhino: Tools menu was loaded from a different path ({existing}). " +
                        $"Opening package RUI: {ruiPath}");
                }
                else
                {
                    PluginLog.Info("RUI already loaded (same path via name lookup)");
                    return;
                }
            }

            var opened = RhinoApp.ToolbarFiles.Open(ruiPath);
            if (opened is null)
            {
                RhinoApp.WriteLine(
                    "MCP4Rhino: failed to load Tools menu (RUI Open returned null). " +
                    "Use MCP4RhinoStatus / MCP4Rhino commands.");
                PluginLog.Info($"RUI Open returned null for {ruiPath}");
                return;
            }

            RhinoApp.WriteLine("MCP4Rhino: Tools → MCP4Rhino menu loaded.");
            PluginLog.Info($"RUI loaded from {ruiPath}");
        }
        catch (Exception ex)
        {
            PluginLog.Info($"RUI load skipped: {ex.Message}");
            RhinoApp.WriteLine("MCP4Rhino: Tools menu unavailable; use MCP4RhinoStatus / MCP4Rhino commands.");
        }
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
