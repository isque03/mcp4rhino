using System.Diagnostics.CodeAnalysis;
using MCP4Rhino.Logic.Models;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace MCP4Rhino.Tools;

[ExcludeFromCodeCoverage]
internal static class ElementMapper
{
    public static TaggedElement FromRhino(RhinoObject obj)
    {
        var tags = ToolHelpers.ReadTags(obj);
        var kind = tags.GetValueOrDefault(ToolHelpers.KindKey) ?? "unknown";
        BBox? bbox = null;
        var bb = obj.Geometry?.GetBoundingBox(true) ?? BoundingBox.Empty;
        if (bb.IsValid)
            bbox = new BBox(bb.Min.X, bb.Min.Y, bb.Min.Z, bb.Max.X, bb.Max.Y, bb.Max.Z);
        return new TaggedElement
        {
            Id = obj.Id.ToString(),
            Name = obj.Name,
            Kind = kind,
            Tags = tags,
            BBox = bbox,
        };
    }

    public static IReadOnlyList<TaggedElement> TaggedInDoc(Rhino.RhinoDoc doc)
    {
        var list = new List<TaggedElement>();
        foreach (var obj in doc.Objects)
        {
            if (obj is null || obj.IsDeleted) continue;
            var kind = obj.Attributes.GetUserString(ToolHelpers.KindKey);
            if (string.IsNullOrWhiteSpace(kind)) continue;
            list.Add(FromRhino(obj));
        }
        return list;
    }
}
