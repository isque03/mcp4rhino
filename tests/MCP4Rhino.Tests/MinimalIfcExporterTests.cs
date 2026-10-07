using MCP4Rhino.Logic;
using MCP4Rhino.Logic.Models;
using Xunit;

namespace MCP4Rhino.Tests;

public class MinimalIfcExporterTests
{
    [Fact]
    public void Export_Contains_Project_Storey_Wall()
    {
        var levels = new[] { new LevelInfo { Name = "L1", Elevation = 0 } };
        var elements = new[]
        {
            new TaggedElement
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Wall A",
                Kind = "wall",
                Tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["mcp4:kind"] = "wall",
                    ["mcp4:level"] = "L1",
                    ["mcp4:ifc"] = "IFCWALLSTANDARDCASE",
                },
                BBox = new BBox(0, 0, 0, 10, 1, 8),
            },
        };

        var ifc = MinimalIfcExporter.Export(levels, elements, "test.ifc");
        Assert.Contains("IFCPROJECT", ifc);
        Assert.Contains("IFCBUILDINGSTOREY", ifc);
        Assert.Contains("IFCWALLSTANDARDCASE", ifc);
        Assert.Contains("IFC2X3", ifc);
        Assert.Contains("END-ISO-10303-21", ifc);
    }

    [Fact]
    public void Escape_Quotes()
    {
        Assert.Equal("O''Brien", MinimalIfcExporter.Escape("O'Brien"));
        var ifc = MinimalIfcExporter.Export([],
        [
            new TaggedElement
            {
                Id = "1",
                Name = "O'Brien",
                Kind = "proxy",
                BBox = new BBox(0, 0, 0, 1, 1, 1),
            },
        ], "a.ifc");
        Assert.Contains("O''Brien", ifc);
        Assert.Contains("IFCBUILDINGELEMENTPROXY", ifc);
    }

    [Fact]
    public void MapIfc_Kinds()
    {
        Assert.Equal("IFCWALLSTANDARDCASE", MinimalIfcExporter.MapIfc("wall"));
        Assert.Equal("IFCSPACE", MinimalIfcExporter.MapIfc("space"));
        Assert.Equal("IFCSLAB", MinimalIfcExporter.MapIfc("IFCSLAB"));
    }

    [Fact]
    public void Import_StoreyGuids()
    {
        var text = "#10=IFCBUILDINGSTOREY('ABC123',$,'L1',$,$,$,$,$,.ELEMENT.,0.);";
        var guids = IfcImportMetadata.FindStoreyGuids(text);
        Assert.Contains("ABC123", guids);
    }

    [Fact]
    public void Export_Metres_Flag()
    {
        var ifc = MinimalIfcExporter.Export([], [], "m.ifc", useMetres: true);
        Assert.Contains(".METRE.", ifc);
    }
}
