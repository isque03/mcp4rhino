using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Undo;

namespace MCP4Rhino.Grasshopper;

internal static class DefinitionArchive
{
    public static GH_Document Open(string path)
    {
        using var suppression = new SolutionSuppression();
        var archive = new GH_Archive();
        var document = new GH_Document { Enabled = false };
        try
        {
            if (!archive.ReadFromFile(path) || !archive.ExtractObject(document, "Definition"))
                throw new IOException("Cannot load the complete definition. Check installed dependencies.");
            RequireClean(archive);
            document.Enabled = false; document.FilePath = path;
            return document;
        }
        catch { document.Dispose(); throw; }
    }

    private static void RequireClean(GH_Archive archive)
    {
        if (archive.MessageCount(false, true, true) != 0)
            throw new IOException("Grasshopper reported archive warnings/errors. Refusing a potentially incomplete definition; open it manually and resolve dependencies.");
    }

    public static byte[] Capture(GH_Document document)
    {
        var archive = new GH_Archive();
        if (!archive.AppendObject(document, "Definition")) throw new IOException("Cannot serialize definition.");
        RequireClean(archive);
        return archive.Serialize_Binary();
    }

    public static void Restore(GH_Document document, byte[] bytes)
    {
        var archive = new GH_Archive();
        if (!archive.Deserialize_Binary(bytes)) throw new IOException("Cannot deserialize definition.");
        foreach (var node in document.Objects.ToArray()) document.RemoveObject(node, false);
        if (!archive.ExtractObject(document, "Definition")) throw new IOException("Cannot restore definition.");
        RequireClean(archive);
    }

    public static GH_Document Clone(byte[] bytes)
    {
        var doc = new GH_Document { Enabled = false };
        try { Restore(doc, bytes); doc.Enabled = false; return doc; }
        catch { doc.Dispose(); throw; }
    }

    public static void RequireSameGraph(GH_Document original, GH_Document clone)
    {
        if (!Fingerprint(original).SequenceEqual(Fingerprint(clone)))
            throw new IOException("Archive round trip changed node, port or wire identities; refusing to patch.");
    }

    private static IEnumerable<string> Fingerprint(GH_Document document) => document.Objects.OrderBy(n => n.InstanceGuid).SelectMany(node =>
        new[] { $"{node.InstanceGuid}/{node.ComponentGuid}/{node.GetType().AssemblyQualifiedName}" }.Concat(
            new[] { true, false }.SelectMany(input => ComponentCatalog.Parameters(node, input).Select((port, index) =>
                $"{input}/{index}/{port.InstanceGuid}/{port.GetType().AssemblyQualifiedName}/{port.Type.AssemblyQualifiedName}/{port.Access}/" +
                string.Join(",", port.Sources.Select(source => source.InstanceGuid))))));
}

internal sealed class DefinitionUndo(DefinitionSession session, byte[] before, byte[] after) : GH_UndoAction
{
    public override bool ExpiresDisplay => true;
    public override bool ExpiresSolution => false;
    protected override void Internal_Undo(GH_Document doc) => Restore(doc, before);
    protected override void Internal_Redo(GH_Document doc) => Restore(doc, after);

    private void Restore(GH_Document doc, byte[] bytes)
    {
        using var suppression = new SolutionSuppression();
        ComponentCatalog.RequireAuthorized(doc);
        session.Muting = true;
        var enabled = doc.Enabled;
        try { doc.Enabled = false; DefinitionArchive.Restore(doc, bytes); }
        catch { session.Faulted = true; throw; }
        finally { doc.Enabled = enabled; session.Rebind(); session.Muting = false; session.Changed(); }
    }
}
