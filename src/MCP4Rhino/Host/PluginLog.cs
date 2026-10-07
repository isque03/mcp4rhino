using System.Text;

namespace MCP4Rhino.Host;

/// <summary>
/// Append-only log so we can prove WhenNeeded load timing vs MCP start.
/// Path: ~/Library/Logs/MCP4Rhino/mcp4rhino.log (macOS) or %LOCALAPPDATA%\MCP4Rhino\mcp4rhino.log
/// </summary>
public static class PluginLog
{
    private static readonly object Gate = new();
    private static string? _path;

    public static string LogPath
    {
        get
        {
            if (_path is not null)
                return _path;

            string dir;
            if (OperatingSystem.IsMacOS())
                dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Logs", "MCP4Rhino");
            else
                dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MCP4Rhino");

            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, "mcp4rhino.log");
            return _path;
        }
    }

    public static void Info(string message)
    {
        var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [pid={Environment.ProcessId}] {message}";
        try
        {
            Rhino.RhinoApp.WriteLine($"MCP4Rhino: {message}");
        }
        catch
        {
            // Rhino UI may not be ready
        }

        lock (Gate)
        {
            try
            {
                File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
            }
            catch
            {
                // never break Rhino for logging
            }
        }
    }
}
