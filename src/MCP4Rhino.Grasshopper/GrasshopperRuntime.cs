using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using Grasshopper.Kernel;
using MCP4Rhino.Contracts;
using MCP4Rhino.Logic.Grasshopper;
using Rhino;
using Rhino.DocObjects;
using GH = Grasshopper.Instances;

namespace MCP4Rhino.Grasshopper;

/// <summary>Persistent application service. SDK access is confined to the injected UI dispatcher.</summary>
public sealed class GrasshopperRuntime : IGrasshopperRuntime
{
    private readonly IGrasshopperDispatcher _dispatcher;
    private readonly SolveQueue _jobs;
    private readonly ConcurrentDictionary<string, DefinitionSession> _sessions = new();
    private readonly RequestLedger _requests = new();
    private readonly object _requestGate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    private bool _disposed;
    private SolveContext? _activeSolve;

    public string ReadResource(JsonObject request)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_jobs.Busy) return Json(new { status = "busy", next_tool = "gh_get_job" });
        return (string)_dispatcher.Invoke(() => GrasshopperResourceReader.Read(request, _sessions.Values));
    }

    public GrasshopperRuntime(IGrasshopperDispatcher dispatcher, IGrasshopperComponentAuthorization authorization)
    {
        ComponentCatalog.Configure(authorization);
        _dispatcher = dispatcher; _jobs = new SolveQueue(dispatcher.Post);
        RhinoDoc.AddRhinoObject += GeometryChanged; RhinoDoc.DeleteRhinoObject += GeometryChanged;
        RhinoDoc.UndeleteRhinoObject += GeometryChanged;
        RhinoDoc.ReplaceRhinoObject += GeometryReplaced;
        RhinoDoc.ModifyObjectAttributes += AttributesChanged;
        RhinoDoc.DocumentPropertiesChanged += DocumentSettingsChanged;
    }

    public string ComponentLibraries()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_jobs.Busy) throw new GhException("busy", "Wait for the current Grasshopper solve before changing plugin permissions.");
        return (string)_dispatcher.Invoke(() => JsonSerializer.Serialize(ComponentCatalog.Libraries()));
    }

    public string Call(string tool, JsonObject arguments)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        GhToolCatalog.Validate(tool, arguments);
        if (tool == "gh_status") return Json(new { initialized = true, available = true, busy = _jobs.Busy,
            sessions = _sessions.Count, max_patch_operations = 100, max_page_size = 200, max_definition_objects = 2000, help_topic = "index" });
        if (tool == "gh_get_job") return Json(_jobs.Get(GhJson.Text(arguments, "job_id")));
        if (tool == "gh_cancel_job") return Json(_jobs.Cancel(GhJson.Text(arguments, "job_id")));
        if (tool == "gh_solve")
        {
            lock (_requestGate) return _requests.Execute(tool, arguments, () =>
                Json(Enqueue(Session(arguments), arguments)));
        }
        if (_jobs.Busy) throw new GhException("busy", "A solve is queued or running. Poll gh_get_job before accessing the canvas.");
        return (string)_dispatcher.Invoke(() =>
        {
            lock (_requestGate)
            {
                return IsMutation(tool) ? _requests.Execute(tool, arguments, () => Json(Dispatch(tool, arguments))) :
                    Json(Dispatch(tool, arguments));
            }
        });
    }

    private static bool IsMutation(string tool) => tool is "gh_open_document" or "gh_apply_patch" or
        "gh_save_document" or "gh_close_document" or "gh_bake" or "gh_show_document";

    private object Dispatch(string tool, JsonObject args) => tool switch
    {
        "gh_search_components" => ComponentCatalog.Search(args),
        "gh_describe_components" => ComponentCatalog.Describe(args),
        "gh_list_documents" => List(),
        "gh_open_document" => Open(args),
        "gh_inspect_document" => ResultReader.Inspect(Session(args), args),
        "gh_apply_patch" => Patch(Session(args), args),
        "gh_read_output" => ResultReader.Read(Session(args), args),
        "gh_get_solution_summary" => ResultReader.Summary(Session(args), args),
        "gh_capture_canvas" => CanvasCapture.Capture(Session(args), args),
        "gh_show_document" => Show(Session(args), args),
        "gh_bake" => ResultReader.Bake(Session(args), args),
        "gh_save_document" => Save(Session(args), args),
        "gh_close_document" => Close(Session(args), args),
        _ => throw new GhException("unknown_tool", $"Unknown Grasshopper tool {tool}.", "index"),
    };

    public object WithPreview(JsonObject context, Func<object> capture)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        GhSchema.Validate(CaptureContent.ContextSchema, context);
        if (_jobs.Busy) throw new GhException("busy", "Wait for the running solve before capturing preview.");
        return _dispatcher.Invoke(() =>
        {
            var session = Session(context);
            session.RequireContext(); session.Identity.Require(context, true);
            if (!ReferenceEquals(GH.ActiveCanvas?.Document, session.Document))
                throw new GhException("preview_not_active", "Use gh_show_document to explicitly activate this definition before viewport capture.", "capture");
            var identity = session.Identity;
            var result = capture();
            session.RequireContext(); session.Identity.Require(context, true);
            if (!ReferenceEquals(GH.ActiveCanvas?.Document, session.Document))
                throw new GhException("preview_not_active", "The preview document changed during capture.", "capture");
            return CaptureContent.Associate(result, identity);
        });
    }

    private static object Show(DefinitionSession session, JsonObject args)
    {
        session.RequireContext(); GhJson.ExpectRevision(session.Revision, args);
        var id = Rhino.PlugIns.PlugIn.IdFromName("Grasshopper");
        var plugin = RhinoApp.GetPlugInObject(id);
        var show = plugin?.GetType().GetMethod("ShowEditor") ??
            throw new GhException("editor_unavailable", "Grasshopper cannot show its editor on this installation.", "capture");
        show.Invoke(plugin, null);
        var canvas = DocumentPresentation.Activate(session.Document);
        if (GhJson.Flag(args, "fit"))
        {
            var region = CanvasCapture.Contents(session.Document);
            var frame = new CanvasFrame(canvas.Viewport.Width, canvas.Viewport.Height, "fit", region);
            canvas.Viewport.Zoom = (float)frame.Zoom(region);
            canvas.Viewport.MidPoint = new System.Drawing.PointF((float)(region.X + region.Width / 2), (float)(region.Y + region.Height / 2));
        }
        GH.InvalidateCanvas();
        return session.Summary();
    }

    private DefinitionSession Session(JsonObject args) => _sessions.GetValueOrDefault(GhJson.Text(args, "session_id")) ??
        throw new GhException("session_not_found", "Session not found. Open or explicitly attach a definition.");

    private object List() => new
    {
        documents = GH.DocumentServer.Cast<GH_Document>().Select(d => new { document_id = d.RuntimeID,
            name = d.DisplayName, path = d.FilePath, modified = d.IsModified }).ToArray(),
        sessions = _sessions.Values.Select(s => s.Summary()).ToArray(),
    };

    private object Open(JsonObject args)
    {
        PruneClosedSessions();
        var rhino = RhinoDoc.ActiveDoc ?? throw new GhException("document_closed", "Open a Rhino document first.");
        if (_sessions.Count >= 32) throw new GhException("limit_exceeded", "Close a session before opening more definitions.");
        var owned = GhJson.Text(args, "mode") != "attach";
        var document = SelectDocument(args);
        var existing = _sessions.Values.FirstOrDefault(s => ReferenceEquals(s.Document, document));
        if (existing is not null) return existing.Summary();
        try
        {
            if (owned) Register(document, args);
            var session = new DefinitionSession(document, rhino, owned, args["name"]?.GetValue<string>());
            _sessions[session.Id] = session;
            return session.Summary();
        }
        catch
        {
            if (owned) { GH.DocumentServer.RemoveDocument(document); document.Dispose(); }
            throw;
        }
    }

    private static GH_Document SelectDocument(JsonObject args) => GhJson.Text(args, "mode") switch
    {
        "create" => new GH_Document(), "open" => ReadDocument(GhJson.DefinitionPath(args)),
        "attach" => GH.DocumentServer.Cast<GH_Document>().FirstOrDefault(d => d.RuntimeID.ToString() == GhJson.Text(args, "document_id")) ??
            throw new GhException("document_not_found", "Use a document_id returned by gh_list_documents."),
        _ => throw new GhException("invalid_arguments", "mode must be create, open or attach."),
    };

    private static GH_Document ReadDocument(string path)
    {
        if (GH.DocumentServer.Cast<GH_Document>().Any(d => string.Equals(d.FilePath, path, StringComparison.OrdinalIgnoreCase)))
            throw new GhException("already_open", "Definition is already open. Explicitly attach its document_id.");
        return DefinitionArchive.Open(path);
    }

    private static void Register(GH_Document document, JsonObject args)
    {
        document.Enabled = false;
        GH.DocumentServer.AddDocument(document);
        // Opening is inspection-only: leave the document disabled until an explicit supported solve.
        if (GH.ActiveCanvas is not null) { DocumentPresentation.Activate(document); GH.InvalidateCanvas(); }
    }

    private object Patch(DefinitionSession session, JsonObject args)
    {
        var result = GraphEditor.Patch(session, args);
        var job = GhJson.Flag(args, "solve") ? Enqueue(session, new JsonObject { ["expected_revision"] = session.Revision }) : null;
        return new { session = session.Summary(), patch = result, job };
    }

    private JobStatus Enqueue(DefinitionSession session, JsonObject args)
    {
        GhJson.ExpectRevision(session.Revision, args);
        var revision = session.Revision;
        return _jobs.Enqueue(session.Id, context => Solve(session, revision, context), GhJson.Integer(args, "timeout_seconds", 60));
    }

    private object Solve(DefinitionSession session, long revision, SolveContext context)
    {
        var rhino = session.RequireContext();
        GhJson.ExpectRevision(session.Revision, new JsonObject { ["expected_revision"] = revision });
        var document = session.Document;
        ComponentCatalog.RequireAuthorized(document);
        GraphRules.RequireAcyclic(document.Objects.Select(n => n.InstanceGuid.ToString()), GraphEditor.Edges(document));
        if (document.SolutionDepth != 0 || !GH_Document.EnableSolutions)
            throw new GhException("solver_disabled_or_busy", "Enable Grasshopper's solver and wait for any existing solution.");
        context.Abort = () => _dispatcher.PostCancellation(() =>
        {
            if (ReferenceEquals(_activeSolve, context) && document.SolutionDepth > 0) document.RequestAbortSolution();
        });
        session.ResultId = null;
        if (context.CancellationRequested) return new { cancelled = true };
        document.Enabled = true;
        _activeSolve = context;
        try { document.NewSolution(false); }
        finally { _activeSolve = null; }
        GhJson.ExpectRevision(session.Revision, new JsonObject { ["expected_revision"] = revision });
        if (!context.CancellationRequested && !document.AbortRequested) session.ResultId = Guid.NewGuid().ToString();
        rhino.Views.Redraw();
        return ResultReader.SolveSummary(session);
    }

    private static object Save(DefinitionSession session, JsonObject args)
    {
        session.RequireContext(); GhJson.ExpectRevision(session.Revision, args);
        var path = GhJson.DefinitionPath(args);
        if (File.Exists(path) && !GhJson.Flag(args, "overwrite")) throw new GhException("file_exists", "Set overwrite:true to replace this file.");
        var io = new GH_DocumentIO { Document = session.Document };
        if (!io.SaveQuiet(path)) throw new GhException("save_failed", "Grasshopper could not save this definition.");
        return new { session = session.Summary(), path };
    }

    private object Close(DefinitionSession session, JsonObject args)
    {
        if (session.IsClosed)
        {
            GhJson.ExpectRevision(session.Revision, args);
            session.Dispose(); _sessions.TryRemove(session.Id, out _);
            return new { session_id = session.Id, closed = true, detached = true };
        }
        session.RequireContext(); GhJson.ExpectRevision(session.Revision, args);
        if (session.Owned && session.Document.IsModified && !GhJson.Flag(args, "discard"))
            throw new GhException("unsaved_changes", "Save the definition or explicitly pass discard:true.");
        session.Dispose(); _sessions.TryRemove(session.Id, out _);
        if (session.Owned) { GH.DocumentServer.RemoveDocument(session.Document); session.Document.Dispose(); }
        return new { session_id = session.Id, closed = session.Owned, detached = !session.Owned };
    }

    private void GeometryChanged(object? sender, RhinoObjectEventArgs e) => Invalidate(e.TheObject.Document.RuntimeSerialNumber, e.ObjectId);
    private void GeometryReplaced(object? sender, RhinoReplaceObjectEventArgs e) => Invalidate(e.Document.RuntimeSerialNumber, e.OldRhinoObject.Id);
    private void AttributesChanged(object? sender, RhinoModifyObjectAttributesEventArgs e) => Invalidate(e.Document.RuntimeSerialNumber, e.RhinoObject.Id);

    private void DocumentSettingsChanged(object? sender, DocumentEventArgs e)
    {
        foreach (var session in _sessions.Values.Where(s => s.RhinoSerial == e.DocumentSerialNumber))
        {
            session.Document.ExpireSolution(); session.Changed();
        }
    }

    private void Invalidate(uint serial, Guid id)
    {
        foreach (var session in _sessions.Values.Where(s => s.RhinoSerial == serial)) session.SourceChanged(id);
    }

    private static string Json(object value) => JsonSerializer.Serialize(value, JsonOptions);

    private void PruneClosedSessions()
    {
        foreach (var session in _sessions.Values.Where(s => s.IsClosed))
        {
            session.Dispose(); _sessions.TryRemove(session.Id, out _);
        }
    }

    public void Dispose()
    {
        _disposed = true; _jobs.Dispose();
        RhinoDoc.AddRhinoObject -= GeometryChanged; RhinoDoc.DeleteRhinoObject -= GeometryChanged;
        RhinoDoc.UndeleteRhinoObject -= GeometryChanged; RhinoDoc.ReplaceRhinoObject -= GeometryReplaced;
        RhinoDoc.ModifyObjectAttributes -= AttributesChanged;
        RhinoDoc.DocumentPropertiesChanged -= DocumentSettingsChanged;
        foreach (var session in _sessions.Values)
        {
            session.Dispose();
            // Registered documents belong to GH's UI lifecycle; let it prompt for unsaved work at shutdown.
        }
        _sessions.Clear();
    }
}
