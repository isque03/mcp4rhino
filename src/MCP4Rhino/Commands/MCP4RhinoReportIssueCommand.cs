using System.Diagnostics.CodeAnalysis;
using Rhino;
using Rhino.Commands;
using MCP4Rhino.Host;
using MCP4Rhino.Logic;

namespace MCP4Rhino.Commands;

[ExcludeFromCodeCoverage]
[CommandStyle(Style.ScriptRunner)]
public class MCP4RhinoReportIssueCommand : Command
{
    public MCP4RhinoReportIssueCommand()
    {
        Instance = this;
    }

    public static MCP4RhinoReportIssueCommand Instance { get; private set; } = null!;

    public override string EnglishName => "MCP4RhinoReportIssue";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        PluginLog.Info("Command MCP4RhinoReportIssue invoked");
        StatusDialog.OpenUrl(SupportInfo.NewIssueUrl);
        return Result.Success;
    }
}
