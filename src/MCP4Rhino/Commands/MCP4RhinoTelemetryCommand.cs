using System.Diagnostics.CodeAnalysis;
using Rhino;
using Rhino.Commands;
using MCP4Rhino.Host;

namespace MCP4Rhino.Commands;

/// <summary>Opens the usage-data choice so it can be changed after the first prompt.</summary>
[ExcludeFromCodeCoverage]
[CommandStyle(Style.ScriptRunner)]
public class MCP4RhinoTelemetryCommand : Command
{
    public MCP4RhinoTelemetryCommand()
    {
        Instance = this;
    }

    public static MCP4RhinoTelemetryCommand Instance { get; private set; } = null!;

    public override string EnglishName => "MCP4RhinoTelemetry";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        PluginLog.Info("Command MCP4RhinoTelemetry invoked");
        try
        {
            TelemetryHost.ShowPreferences();
        }
        catch (Exception ex)
        {
            PluginLog.Info($"Telemetry dialog failed: {ex.Message}");
            RhinoApp.WriteLine($"MCP4Rhino: usage data dialog unavailable ({ex.Message}).");
            return Result.Failure;
        }

        RhinoApp.WriteLine($"MCP4Rhino: {TelemetryHost.StatusLine()}");
        return Result.Success;
    }
}
