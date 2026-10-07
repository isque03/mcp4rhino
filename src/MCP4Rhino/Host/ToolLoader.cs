using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.Loader;
using MCP4Rhino.Contracts;

namespace MCP4Rhino.Host;

/// <summary>
/// Loads MCP4Rhino.Tools.dll in a collectible ALC so it can be hot-reloaded.
/// </summary>
[ExcludeFromCodeCoverage]
public static class ToolLoader
{
    private static readonly object Gate = new();
    private static CollectibleAlc? _alc;
    private static IMcpToolBridge? _bridge;
    private static string? _loadedFrom;
    private static string? _shadowDir;

    public static IMcpToolBridge? Bridge
    {
        get { lock (Gate) return _bridge; }
    }

    public static string? LoadedFrom
    {
        get { lock (Gate) return _loadedFrom; }
    }

    public static bool IsLoaded
    {
        get { lock (Gate) return _bridge is not null; }
    }

    public static void EnsureLoaded()
    {
        lock (Gate)
        {
            if (_bridge is not null)
                return;
            LoadUnlocked(ResolveSourcePath());
        }
    }

    public static void Reload()
    {
        lock (Gate)
        {
            var source = ResolveSourcePath();
            UnloadUnlocked();
            LoadUnlocked(source);
        }
    }

    public static void Unload()
    {
        lock (Gate) UnloadUnlocked();
    }

    private static void LoadUnlocked(string sourceDll)
    {
        if (!File.Exists(sourceDll))
            throw new FileNotFoundException($"Tools assembly not found: {sourceDll}");

        var sourceDir = Path.GetDirectoryName(sourceDll)!;
        _shadowDir = Path.Combine(Path.GetTempPath(), "MCP4RhinoHot", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(_shadowDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            // Keep the hot folder lean: tools + contracts + drawing; host/rhp stay in default context.
            if (name.Equals("MCP4Rhino.rhp", StringComparison.OrdinalIgnoreCase))
                continue;
            if (name.Equals("MCP4Rhino.dll", StringComparison.OrdinalIgnoreCase))
                continue;
            File.Copy(file, Path.Combine(_shadowDir, name), overwrite: true);
        }

        var shadowTools = Path.Combine(_shadowDir, "MCP4Rhino.Tools.dll");
        if (!File.Exists(shadowTools))
            File.Copy(sourceDll, shadowTools, overwrite: true);

        _alc = new CollectibleAlc(sourceDir);
        var asm = _alc.LoadFromAssemblyPath(shadowTools);
        var type = asm.GetType("MCP4Rhino.Tools.RhinoToolBridge")
            ?? throw new TypeLoadException("MCP4Rhino.Tools.RhinoToolBridge not found.");
        _bridge = (IMcpToolBridge)(Activator.CreateInstance(type)
            ?? throw new InvalidOperationException("Failed to create RhinoToolBridge."));
        _loadedFrom = sourceDll;
        PluginLog.Info($"ToolLoader loaded {_loadedFrom} (shadow={_shadowDir})");
    }

    private static void UnloadUnlocked()
    {
        _bridge = null;
        var alc = _alc;
        _alc = null;
        var shadow = _shadowDir;
        _shadowDir = null;
        _loadedFrom = null;

        if (alc is not null)
        {
            alc.Unload();
            // Encourage collectible unload before next load.
            for (var i = 0; i < 3; i++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        if (shadow is not null)
        {
            try
            {
                Directory.Delete(shadow, recursive: true);
            }
            catch
            {
                // file locks may linger briefly
            }
        }

        PluginLog.Info("ToolLoader unloaded tools ALC");
    }

    public static string ResolveSourcePath()
    {
        var env = Environment.GetEnvironmentVariable("MCP4RHINO_TOOLS_PATH");
        if (!string.IsNullOrWhiteSpace(env))
        {
            if (Directory.Exists(env))
                return Path.Combine(env, "MCP4Rhino.Tools.dll");
            return env;
        }

        // Beside the host plugin (yak package / Plug-ins folder).
        var hostDir = Path.GetDirectoryName(typeof(ToolLoader).Assembly.Location)!;
        var beside = Path.Combine(hostDir, "MCP4Rhino.Tools.dll");
        if (File.Exists(beside))
            return beside;

        // Dev fallback: repo build output.
        var dev = Path.GetFullPath(Path.Combine(
            hostDir, "..", "..", "..", "..", "..",
            "MCP4Rhino.Tools", "bin", "Release", "net8.0", "MCP4Rhino.Tools.dll"));
        if (File.Exists(dev))
            return dev;

        throw new FileNotFoundException(
            "MCP4Rhino.Tools.dll not found. Set MCP4RHINO_TOOLS_PATH or install the yak package with tools beside the .rhp.");
    }

    private sealed class CollectibleAlc : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public CollectibleAlc(string pluginDir) : base(isCollectible: true)
        {
            var toolsPath = Path.Combine(pluginDir, "MCP4Rhino.Tools.dll");
            _resolver = new AssemblyDependencyResolver(toolsPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Prefer default context for host + contracts + Rhino.
            // Prefer default context for host + contracts + logic + Rhino.
            if (assemblyName.Name is "MCP4Rhino" or "MCP4Rhino.Contracts" or "MCP4Rhino.Logic"
                or "RhinoCommon" or "Rhino.UI")
                return null;

            var path = _resolver.ResolveAssemblyToPath(assemblyName);
            return path is not null ? LoadFromAssemblyPath(path) : null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path is not null ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
        }
    }
}
