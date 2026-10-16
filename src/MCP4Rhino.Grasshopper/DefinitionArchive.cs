using GH_IO.Serialization;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Undo;

namespace MCP4Rhino.Grasshopper;

internal static class DefinitionArchive
{
    public static byte[] Capture(GH_Document document)
    {
        var archive = new GH_Archive();
        if (!archive.AppendObject(document, "Definition")) throw new IOException("Cannot serialize definition.");
        return archive.Serialize_Binary();
    }

    public static void Restore(GH_Document document, byte[] bytes)
    {
        var archive = new GH_Archive();
        if (!archive.Deserialize_Binary(bytes)) throw new IOException("Cannot deserialize definition.");
        foreach (var node in document.Objects.ToArray()) document.RemoveObject(node, false);
        if (!archive.ExtractObject(document, "Definition")) throw new IOException("Cannot restore definition.");
    }

    public static GH_Document Clone(byte[] bytes)
    {
        var doc = new GH_Document { Enabled = false };
        try { Restore(doc, bytes); doc.Enabled = false; return doc; }
        catch { doc.Dispose(); throw; }
    }
}

internal sealed class DefinitionUndo(DefinitionSession session, byte[] before, byte[] after) : GH_UndoAction
{
    public override bool ExpiresDisplay => true;
    public override bool ExpiresSolution => true;
    protected override void Internal_Undo(GH_Document doc) => Restore(doc, before);
    protected override void Internal_Redo(GH_Document doc) => Restore(doc, after);

    private void Restore(GH_Document doc, byte[] bytes)
    {
        session.Muting = true;
        var enabled = doc.Enabled;
        try { doc.Enabled = false; DefinitionArchive.Restore(doc, bytes); }
        finally { doc.Enabled = enabled; session.Rebind(); session.Muting = false; session.Changed(); }
    }
}
