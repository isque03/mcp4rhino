using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Eto.Drawing;
using Eto.Forms;
using MCP4Rhino.Logic;
using Rhino;
using Rhino.UI;

namespace MCP4Rhino.Host;

/// <summary>Simple Eto dialog so users can see Running/Stopped and act on it.</summary>
[ExcludeFromCodeCoverage]
public sealed class StatusDialog : Dialog
{
    private readonly Label _state = new()
    {
        Font = new Font(SystemFont.Bold, 14),
        TextAlignment = TextAlignment.Left,
    };
    private readonly Label _version = LeftLabel();
    private readonly Label _endpoint = LeftLabel();
    private readonly Label _autostart = LeftLabel();
    private readonly Label _telemetry = LeftLabel();
    private readonly Label _log = LeftLabel();
    private readonly Button _start = new() { Text = "Start" };
    private readonly Button _stop = new() { Text = "Stop" };
    private Action? _afterClose;

    private static Label LeftLabel() => new() { TextAlignment = TextAlignment.Left };

    public StatusDialog()
    {
        Title = "MCP4Rhino Status";
        ClientSize = new Size(520, 410);
        Resizable = false;
        Padding = 12;

        Closed += (_, _) =>
        {
            var action = _afterClose;
            _afterClose = null;
            action?.Invoke();
        };

        _start.Click += (_, _) =>
        {
            McpHostBootstrap.TryStart(quiet: false);
            RefreshFields();
        };
        _stop.Click += (_, _) =>
        {
            if (McpHost.IsRunning)
            {
                McpHost.Stop();
                RhinoApp.WriteLine("MCP4Rhino: stopped.");
            }
            RefreshFields();
        };

        var copy = new Button { Text = "Copy endpoint" };
        copy.Click += (_, _) =>
        {
            var url = McpHost.IsRunning ? McpHost.Endpoint : SupportInfo.ConfiguredEndpoint();
            Clipboard.Instance.Text = url;
            RhinoApp.WriteLine($"MCP4Rhino: copied {url}");
        };

        var updates = new Button { Text = "Check for updates…" };
        updates.Click += (_, _) => CloseThen(OpenPackageManager);

        var logBtn = new Button { Text = "Open log folder" };
        logBtn.Click += (_, _) => CloseThen(OpenLogFolder);

        var issue = new Button { Text = "Report issue…" };
        issue.Click += (_, _) => CloseThen(() => OpenUrl(SupportInfo.NewIssueUrl));

        var usage = new Button { Text = "Usage data…" };
        usage.Click += (_, _) =>
        {
            TelemetryHost.ShowPreferences(this);
            RefreshFields();
        };

        var close = new Button { Text = "Close" };
        var grasshopper = new Button { Text = "Grasshopper plugins…" };
        grasshopper.Click += (_, _) => CloseThen(() => RhinoApp.RunScript("_MCP4RhinoGrasshopper", false));
        close.Click += (_, _) => Close();
        DefaultButton = close;

        Content = new StackLayout
        {
            Spacing = 8,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            VerticalContentAlignment = VerticalAlignment.Top,
            Items =
            {
                _state,
                _version,
                _endpoint,
                _autostart,
                _telemetry,
                _log,
                new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Items = { _start, _stop, copy },
                },
                new StackLayout
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    Items = { updates, logBtn, issue, close },
                },
                usage,
                grasshopper,
            },
        };

        RefreshFields();
    }

    private void CloseThen(Action action)
    {
        _afterClose = action;
        Close();
    }

    public static void ShowStatus()
    {
        using var dlg = new StatusDialog();
        dlg.ShowModal(RhinoEtoApp.MainWindow);
    }

    private void RefreshFields()
    {
        var running = McpHost.IsRunning;
        _state.Text = running ? "State: Running" : "State: Stopped";
        _state.TextColor = running ? Colors.DarkGreen : Colors.DarkRed;

        _version.Text = $"Version: {McpJsonRpc.ServerVersion}";
        _endpoint.Text = running
            ? $"Endpoint: {McpHost.Endpoint}"
            : $"Endpoint (when started): {SupportInfo.ConfiguredEndpoint()}";
        _autostart.Text = $"Autostart: {(McpHostBootstrap.AutostartEnabled ? "on" : "off")} (MCP4RHINO_AUTOSTART)";
        _telemetry.Text = TelemetryHost.StatusLine();
        _log.Text = $"Log: {PluginLog.LogPath}";

        _start.Enabled = !running;
        _stop.Enabled = running;
    }

    internal static void OpenPackageManager()
    {
        try
        {
            RhinoApp.RunScript("_PackageManager", false);
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"MCP4Rhino: could not open Package Manager: {ex.Message}");
        }
    }

    internal static void OpenLogFolder()
    {
        try
        {
            var dir = Path.GetDirectoryName(PluginLog.LogPath);
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", dir);
            else
                RhinoApp.WriteLine($"MCP4Rhino: open this log folder manually: {dir}");
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"MCP4Rhino: could not open log folder: {ex.Message}");
        }
    }

    internal static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"MCP4Rhino: could not open browser: {ex.Message}");
            RhinoApp.WriteLine($"  {url}");
        }
    }
}
