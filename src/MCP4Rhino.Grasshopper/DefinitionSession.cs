using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using MCP4Rhino.Logic.Grasshopper;
using Rhino;
using GH = Grasshopper.Instances;

namespace MCP4Rhino.Grasshopper;

internal sealed class DefinitionSession : IDisposable
{
    public string Id { get; } = Guid.NewGuid().ToString();
    public GH_Document Document { get; }
    public uint RhinoSerial { get; }
    public bool Owned { get; }
    public string? Label { get; }
    public long Revision { get; private set; }
    public string? ResultId { get; set; }
    public bool Muting { get; set; }
    public bool Faulted { get; set; }
    private readonly List<Action> _objectUnsubscribe = new();

    public DefinitionSession(GH_Document document, RhinoDoc rhino, bool owned, string? label = null)
    {
        Document = document; RhinoSerial = rhino.RuntimeSerialNumber; Owned = owned; Label = label;
        document.ObjectsAdded += Topology;
        document.ObjectsDeleted += Topology;
        document.UndoStateChanged += UndoChanged;
        document.SolutionStart += SolutionStarted;
        Rebind();
    }

    public void Changed()
    {
        if (Muting) return;
        Revision++;
        ResultId = null;
    }

    private void Topology(object? sender, GH_DocObjectEventArgs e) { Rebind(); Changed(); }
    public void SourceChanged(Guid id)
    {
        var affected = Document.Objects.Where(node => ComponentCatalog.Parameters(node, true).Any(port =>
            port.GetType().GetProperty("PersistentData")?.GetValue(port) is IGH_Structure tree &&
            tree.AllData(true).OfType<IGH_GeometricGoo>().Any(g => g.ReferenceID == id))).ToArray();
        if (affected.Length == 0) return;
        foreach (var node in affected.OfType<IGH_ActiveObject>()) node.ExpireSolution(false);
        Changed();
    }
    private void UndoChanged(object? sender, GH_DocUndoEventArgs e) { Rebind(); Changed(); }
    private void SolutionStarted(object? sender, GH_SolutionEventArgs e) => ResultId = null;

    public void Rebind()
    {
        foreach (var unsubscribe in _objectUnsubscribe) unsubscribe();
        _objectUnsubscribe.Clear();
        foreach (var node in Document.Objects)
        {
            IGH_DocumentObject.ObjectChangedEventHandler changed = (_, _) => Changed();
            node.ObjectChanged += changed;
            _objectUnsubscribe.Add(() => node.ObjectChanged -= changed);
            IGH_DocumentObject.AttributesChangedEventHandler layout = (_, _) => Changed();
            node.AttributesChanged += layout;
            _objectUnsubscribe.Add(() => node.AttributesChanged -= layout);
            if (node is not IGH_Component component) continue;
            GH_ComponentParamServer.ParameterChangedEventHandler ports = (_, _) => Changed();
            component.Params.ParameterChanged += ports;
            _objectUnsubscribe.Add(() => component.Params.ParameterChanged -= ports);
        }
    }

    public RhinoDoc RequireContext()
    {
        if (Faulted) throw new GhException("session_faulted", "Rollback failed. Save recovery data and reopen the definition.", "debugging");
        var rhino = RhinoDoc.FromRuntimeSerialNumber(RhinoSerial);
        if (rhino is null || !GH.DocumentServer.Contains(Document))
            throw new GhException("document_closed", "The bound Rhino or Grasshopper document was closed.");
        if (RhinoDoc.ActiveDoc?.RuntimeSerialNumber != RhinoSerial)
            throw new GhException("document_mismatch", "Activate the Rhino document bound to this session.");
        return rhino;
    }

    public bool IsClosed => RhinoDoc.FromRuntimeSerialNumber(RhinoSerial) is null || !GH.DocumentServer.Contains(Document);

    public object Summary() => new
    {
        session_id = Id, document_id = Document.RuntimeID, name = Label ?? Document.DisplayName, owned = Owned,
        revision = Revision, rhino_document = RhinoSerial, result_id = ResultId,
        modified = Document.IsModified, faulted = Faulted,
    };

    public void RequireResult(string id)
    {
        if (ResultId is null || ResultId != id)
            throw new GhException("stale_result", "Outputs expired. Inspect the definition and solve again.");
    }

    public void Dispose()
    {
        Document.ObjectsAdded -= Topology; Document.ObjectsDeleted -= Topology;
        Document.UndoStateChanged -= UndoChanged; Document.SolutionStart -= SolutionStarted;
        foreach (var unsubscribe in _objectUnsubscribe) unsubscribe();
        _objectUnsubscribe.Clear();
        ResultId = null;
    }
}
