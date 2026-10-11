using System.Diagnostics.CodeAnalysis;
using Rhino;

namespace MCP4Rhino.Host;

/// <summary>
/// Shared start path for the <c>MCP4Rhino</c> command and Idle auto-start.
/// </summary>
[ExcludeFromCodeCoverage]
public static class McpHostBootstrap
{
    public static bool AutostartEnabled
    {
        get
        {
            var env = Environment.GetEnvironmentVariable("MCP4RHINO_AUTOSTART");
            if (string.IsNullOrWhiteSpace(env)) return true;
            return env.Trim() switch
            {
                "0" => false,
                "false" => false,
                "False" => false,
                "FALSE" => false,
                "no" => false,
                "No" => false,
                "NO" => false,
                "off" => false,
                "Off" => false,
                "OFF" => false,
                _ => true,
            };
        }
    }

    public static int ResolvePort()
    {
        var env = Environment.GetEnvironmentVariable("MCP4RHINO_PORT");
        if (!string.IsNullOrWhiteSpace(env) && int.TryParse(env, out var parsed) && parsed is > 0 and < 65536)
            return parsed;
        return McpHost.DefaultPort;
    }

    /// <summary>
    /// Load tools and start the HTTP MCP host if not already running.
    /// </summary>
    /// <returns><c>true</c> if the server is running afterward.</returns>
    public static bool TryStart(bool quiet = false)
    {
        if (McpHost.IsRunning)
        {
            if (!quiet)
                RhinoApp.WriteLine($"MCP4Rhino: already running at {McpHost.Endpoint}");
            return true;
        }

        try
        {
            ToolLoader.EnsureLoaded();
            var port = ResolvePort();
            if (!quiet)
                RhinoApp.WriteLine($"MCP4Rhino: starting on port {port}…");

            if (!McpHost.Start(port))
            {
                RhinoApp.WriteLine(
                    "MCP4Rhino: FAILED to start (port in use or bind error). " +
                    $"See log: {PluginLog.LogPath}");
                return false;
            }

            RhinoApp.WriteLine($"MCP4Rhino ready: {McpHost.Endpoint}");
            RhinoApp.WriteLine("Connect Claude or Cursor to that URL (see README). Run MCP4RhinoStatus anytime.");
            return true;
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"MCP4Rhino: exception: {ex.GetType().Name}: {ex.Message}");
            PluginLog.Info($"McpHostBootstrap.TryStart exception: {ex}");
            return false;
        }
    }

    public static void WriteStatus()
    {
        RhinoApp.WriteLine($"MCP4Rhino status: {(McpHost.IsRunning ? "running" : "stopped")}");
        if (McpHost.IsRunning)
            RhinoApp.WriteLine($"  endpoint: {McpHost.Endpoint}");
        else
            RhinoApp.WriteLine($"  endpoint (when started): http://127.0.0.1:{ResolvePort()}/mcp");
        RhinoApp.WriteLine($"  autostart: {(AutostartEnabled ? "on" : "off")} (env MCP4RHINO_AUTOSTART)");
        RhinoApp.WriteLine($"  log: {PluginLog.LogPath}");
        if (!string.IsNullOrEmpty(ToolLoader.LoadedFrom))
            RhinoApp.WriteLine($"  tools: {ToolLoader.LoadedFrom}");
    }
}
