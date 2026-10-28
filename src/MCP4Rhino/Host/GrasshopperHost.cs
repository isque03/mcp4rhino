using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Nodes;
using MCP4Rhino.Contracts;
using MCP4Rhino.Logic;
using MCP4Rhino.Logic.Grasshopper;
using Rhino;

namespace MCP4Rhino.Host;

/// <summary>Persistent, lazy runtime ownership; never loaded into the collectible tools context.</summary>
[ExcludeFromCodeCoverage]
public static class GrasshopperHost
{
    private static IGrasshopperRuntime? _runtime;
    private static readonly object Gate = new();

    public static JsonObject ResourceStatus()
    {
        var status = _runtime is { } runtime
            ? JsonNode.Parse(runtime.ReadResource(new JsonObject { ["kind"] = "capabilities" }))!.AsObject()
            : new JsonObject { ["status"] = "uninitialized", ["initialized"] = false };
        status["installed"] = Rhino.PlugIns.PlugIn.IdFromName("Grasshopper") != Guid.Empty;
        return status;
    }

    public static string ReadResource(JsonObject request) => _runtime is { } runtime ? runtime.ReadResource(request) :
        new JsonObject { ["status"] = "uninitialized", ["next_tool"] = "gh_search_components",
            ["detail"] = "Use an explicit discovery tool to initialize Grasshopper; resource reads do not load it." }.ToJsonString();

    public static GrasshopperComponentLibrary[] ComponentLibraries() =>
        JsonSerializer.Deserialize<GrasshopperComponentLibrary[]>(Runtime().ComponentLibraries()) ?? [];

    public static object Call(string name, JsonObject args)
    {
        try
        {
            GhToolCatalog.Validate(name, args);
            var json = name == "gh_help" ? GhHelp.Json(args["topic"]?.GetValue<string>() ?? "index") :
                name == "gh_status" && _runtime is null ? JsonSerializer.Serialize(new
                {
                    initialized = false, available = File.Exists(GrasshopperAssemblyPath()),
                    license_can_save = RhinoApp.CanSave,
                    help_topic = "quickstart", version = McpJsonRpc.ServerVersion,
                }) : Runtime().Call(name, args);
            if (name == "gh_capture_canvas")
            {
                var metadata = JsonNode.Parse(json)!.AsObject();
                return CaptureContent.FromPng(metadata, File.ReadAllBytes(metadata["path"]!.GetValue<string>()));
            }
            return McpContent.TextOnly(json);
        }
        catch (Exception ex) { return Error(ex); }
    }

    public static object CaptureViewport(JsonObject context, Func<object> capture)
    {
        try
        {
            GhSchema.Validate(CaptureContent.ContextSchema, context);
            return Runtime().WithPreview(context, capture);
        }
        catch (Exception ex) { return Error(ex); }
    }

    private static object Error(Exception ex)
    {
        while (ex is TargetInvocationException { InnerException: not null } wrapped) ex = wrapped.InnerException!;
        return new
        {
            content = new[] { new { type = "text", text = JsonSerializer.Serialize(new
            {
                code = (ex as GhException)?.Code ?? "grasshopper_error", message = ex.Message,
                help_topic = (ex as GhException)?.HelpTopic ?? "debugging",
            }) } }, isError = true,
        };
    }

    private static IGrasshopperRuntime Runtime()
    {
        if (Volatile.Read(ref _runtime) is { } ready) return ready;
        // Never hold Gate while waiting for the UI: native preferences can initialize GH too.
        return UiThread.Invoke(() =>
        {
            lock (Gate)
            {
                if (_runtime is not null) return _runtime;
                var runtime = LoadRuntime();
                Volatile.Write(ref _runtime, runtime);
                return runtime;
            }
        });
    }

    private static IGrasshopperRuntime LoadRuntime()
    {
        var id = Rhino.PlugIns.PlugIn.IdFromName("Grasshopper");
        if (id == Guid.Empty) id = new Guid("b45a29b1-4343-4035-989e-044e8580d9cf");
        if (!Rhino.PlugIns.PlugIn.LoadPlugIn(id))
            throw new GhException("grasshopper_unavailable", "Rhino could not load Grasshopper. Check plugin availability and check_rhino_license (including view-only mode).", "quickstart");
        var plugin = RhinoApp.GetPlugInObject(id) ??
            throw new GhException("grasshopper_unavailable", "Grasshopper's scripting interface is unavailable.", "quickstart");
        var loadEditor = plugin.GetType().GetMethod("LoadEditor") ??
            throw new GhException("grasshopper_unavailable", "Grasshopper does not expose LoadEditor.", "quickstart");
        loadEditor.Invoke(plugin, null);
        AssemblyLoadContext.Default.Resolving += ResolveSdk;
        var directory = Path.GetDirectoryName(typeof(GrasshopperHost).Assembly.Location)!;
        var assembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(directory, "MCP4Rhino.Grasshopper.dll"));
        var type = assembly.GetType("MCP4Rhino.Grasshopper.GrasshopperRuntime", throwOnError: true)!;
        return (IGrasshopperRuntime)Activator.CreateInstance(type, new Dispatcher(), GrasshopperComponentAuthorization.Instance)!;
    }

    private static Assembly? ResolveSdk(AssemblyLoadContext context, AssemblyName name) =>
        name.Name is "Grasshopper" or "GH_IO" ? AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == name.Name) : null;

    private static string GrasshopperAssemblyPath() => Path.Combine(Rhino.Runtime.HostUtils.RhinoAssemblyDirectory,
        "ManagedPlugIns", "GrasshopperPlugin.rhp", "Grasshopper.dll");

    public static void Shutdown()
    {
        lock (Gate)
        {
            _runtime?.Dispose();
            _runtime = null;
            AssemblyLoadContext.Default.Resolving -= ResolveSdk;
        }
    }

    private sealed class Dispatcher : IGrasshopperDispatcher
    {
        public object Invoke(Func<object> work) => UiThread.Invoke(work);

        public void PostCancellation(Action work) => RhinoApp.InvokeOnUiThread(work);

        public void Post(Action work)
        {
            // InvokeOnUiThread can execute inline on the UI thread. Idle guarantees an actual queue boundary.
            EventHandler? idle = null;
            idle = (_, _) => { RhinoApp.Idle -= idle; work(); };
            RhinoApp.Idle += idle;
        }
    }
}
