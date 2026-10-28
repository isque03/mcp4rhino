using System.Diagnostics.CodeAnalysis;
using MCP4Rhino.Host;
using Rhino;
using Rhino.Commands;

namespace MCP4Rhino.Commands;

[ExcludeFromCodeCoverage]
public sealed class MCP4RhinoGrasshopperCommand : Command
{
    public override string EnglishName => "MCP4RhinoGrasshopper";

    protected override Result RunCommand(RhinoDoc doc, RunMode mode)
    {
        try { GrasshopperComponentAuthorization.Instance.ShowPreferences(); return Result.Success; }
        catch (Exception ex)
        {
            RhinoApp.WriteLine($"MCP4Rhino: could not open Grasshopper plugin settings ({ex.Message}).");
            return Result.Failure;
        }
    }
}
