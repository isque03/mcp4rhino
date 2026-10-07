using System.Globalization;
using System.Text;
using MCP4Rhino.Logic.Models;

namespace MCP4Rhino.Logic;

public static class MinimalIfcExporter
{
    public static string Export(
        IReadOnlyList<LevelInfo> levels,
        IReadOnlyList<TaggedElement> elements,
        string fileName,
        bool useMetres = false)
    {
        var sb = new StringBuilder();
        var id = 1;
        int Next() => id++;
        sb.AppendLine("ISO-10303-21;");
        sb.AppendLine("HEADER;");
        sb.AppendLine("FILE_DESCRIPTION(('MCP4Rhino minimal IFC2X3'),'2;1');");
        sb.AppendLine($"FILE_NAME('{Escape(fileName)}','{DateTime.UtcNow:yyyy-MM-dd}',('MCP4Rhino'),('MCP4Rhino'),'MCP4Rhino','MCP4Rhino','');");
        sb.AppendLine("FILE_SCHEMA(('IFC2X3'));");
        sb.AppendLine("ENDSEC;");
        sb.AppendLine("DATA;");

        var projectId = Next();
        var siteId = Next();
        var buildingId = Next();
        var owner = Next();
        var app = Next();
        var org = Next();
        var person = Next();
        var unitAssign = Next();
        var geomCtx = Next();
        sb.AppendLine($"#{person}=IFCPERSON($,$,'Agent',$,$,$,$,$);");
        sb.AppendLine($"#{org}=IFCORGANIZATION($,'MCP4Rhino',$,$,$);");
        sb.AppendLine($"#{owner}=IFCPERSONANDORGANIZATION(#{person},#{org},$);");
        sb.AppendLine($"#{app}=IFCAPPLICATION(#{org},'0.3','MCP4Rhino','MCP4Rhino');");
        var units = useMetres ? ".METRE." : ".FOOT.";
        sb.AppendLine($"#{unitAssign}=IFCUNITASSIGNMENT((IFCSIUNIT(*,.LENGTHUNIT.,$,{units})));");
        sb.AppendLine($"#{geomCtx}=IFCGEOMETRICREPRESENTATIONCONTEXT($,'Model',3,1.E-05,IFCAXIS2PLACEMENT3D(IFCCARTESIANPOINT((0.,0.,0.)),$,$),$);");
        sb.AppendLine($"#{projectId}=IFCPROJECT('{Guid.NewGuid():N}',#{owner},'Project',$,$,$,$,({geomCtx}),#{unitAssign});");
        sb.AppendLine($"#{siteId}=IFCSITE('{Guid.NewGuid():N}',#{owner},'Site',$,$,IFCLOCALPLACEMENT($,IFCAXIS2PLACEMENT3D(IFCCARTESIANPOINT((0.,0.,0.)),$,$)),$,$,.ELEMENT.,$,$,$,$,$);");
        sb.AppendLine($"#{buildingId}=IFCBUILDING('{Guid.NewGuid():N}',#{owner},'Building',$,$,IFCLOCALPLACEMENT($,IFCAXIS2PLACEMENT3D(IFCCARTESIANPOINT((0.,0.,0.)),$,$)),$,$,.ELEMENT.,$,$,$);");
        sb.AppendLine($"#{Next()}=IFCRELAGGREGATES('{Guid.NewGuid():N}',#{owner},$,$,#{projectId},(#{siteId}));");
        sb.AppendLine($"#{Next()}=IFCRELAGGREGATES('{Guid.NewGuid():N}',#{owner},$,$,#{siteId},(#{buildingId}));");

        var storeyLevels = levels.Count > 0 ? levels : [new LevelInfo { Name = "L1", Elevation = 0 }];
        var storeyIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var lv in storeyLevels)
        {
            var sid = Next();
            storeyIds[lv.Name] = sid;
            sb.AppendLine($"#{sid}=IFCBUILDINGSTOREY('{Guid.NewGuid():N}',#{owner},'{Escape(lv.Name)}',$,$,IFCLOCALPLACEMENT($,IFCAXIS2PLACEMENT3D(IFCCARTESIANPOINT((0.,0.,{F(lv.Elevation)})),$,$)),$,$,.ELEMENT.,{F(lv.Elevation)});");
        }
        var storeyList = string.Join(",", storeyIds.Values.Select(v => $"#{v}"));
        sb.AppendLine($"#{Next()}=IFCRELAGGREGATES('{Guid.NewGuid():N}',#{owner},$,$,#{buildingId},({storeyList}));");

        foreach (var obj in elements)
        {
            if (obj.BBox is not { IsValid: true } bb) continue;
            var ifcType = MapIfc(obj.Tags.GetValueOrDefault("mcp4:ifc") ?? obj.Kind);
            var level = obj.Tags.GetValueOrDefault("mcp4:level") ?? storeyLevels[0].Name;
            if (!storeyIds.TryGetValue(level, out var storeyId)) storeyId = storeyIds.Values.First();

            var prodId = Next();
            var shapeId = Next();
            var boxId = Next();
            var placeId = Next();
            var ptId = Next();
            var cx = (bb.MinX + bb.MaxX) / 2;
            var cy = (bb.MinY + bb.MaxY) / 2;
            var cz = bb.MinZ;
            var dx = Math.Max(1e-3, bb.SizeX);
            var dy = Math.Max(1e-3, bb.SizeY);
            var dz = Math.Max(1e-3, bb.SizeZ);
            sb.AppendLine($"#{ptId}=IFCCARTESIANPOINT(({F(cx)},{F(cy)},{F(cz)}));");
            sb.AppendLine($"#{placeId}=IFCAXIS2PLACEMENT3D(#{ptId},$,$);");
            sb.AppendLine($"#{boxId}=IFCBOUNDINGBOX(IFCCARTESIANPOINT(({-dx / 2},{ -dy / 2},0.)),{F(dx)},{F(dy)},{F(dz)});");
            sb.AppendLine($"#{shapeId}=IFCSHAPEREPRESENTATION(#{geomCtx},'Box','BoundingBox',(#{boxId}));");
            var name = Escape(obj.Name ?? obj.Kind);
            sb.AppendLine($"#{prodId}={ifcType}('{Guid.NewGuid():N}',#{owner},'{name}',$,$,IFCLOCALPLACEMENT($,#{placeId}),IFCPRODUCTDEFINITIONSHAPE($,$,(#{shapeId})),$);");
            sb.AppendLine($"#{Next()}=IFCRELCONTAINEDINSPATIALSTRUCTURE('{Guid.NewGuid():N}',#{owner},$,$,(#{prodId}),#{storeyId});");
        }

        sb.AppendLine("ENDSEC;");
        sb.AppendLine("END-ISO-10303-21;");
        return sb.ToString();
    }

    public static string MapIfc(string kind) => kind.ToUpperInvariant() switch
    {
        "WALL" or "IFCWALL" or "IFCWALLSTANDARDCASE" => "IFCWALLSTANDARDCASE",
        "SLAB" or "IFCSLAB" => "IFCSLAB",
        "ROOF" or "IFCROOF" => "IFCROOF",
        "DOOR" or "IFCDOOR" => "IFCDOOR",
        "WINDOW" or "IFCWINDOW" => "IFCWINDOW",
        "COLUMN" or "IFCCOLUMN" => "IFCCOLUMN",
        "BEAM" or "IFCBEAM" => "IFCBEAM",
        "STAIR" or "IFCSTAIR" => "IFCSTAIR",
        "RAMP" or "IFCRAMP" => "IFCRAMP",
        "SPACE" or "IFCSPACE" => "IFCSPACE",
        "RAILING" or "IFCRAILING" => "IFCRAILING",
        "CURTAIN_WALL" or "IFCCURTAINWALL" => "IFCCURTAINWALL",
        _ => kind.StartsWith("IFC", StringComparison.OrdinalIgnoreCase) ? kind.ToUpperInvariant() : "IFCBUILDINGELEMENTPROXY",
    };

    public static string Escape(string s) => s.Replace("'", "''");
    private static string F(double v) => v.ToString("G", CultureInfo.InvariantCulture);
}

public static class IfcImportMetadata
{
    public static string[] FindStoreyGuids(string ifcText) =>
        System.Text.RegularExpressions.Regex.Matches(ifcText, @"IFCBUILDINGSTOREY\('([^']*)'")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToArray();
}
