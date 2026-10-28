using System.Diagnostics.CodeAnalysis;
using Eto.Forms;
using MCP4Rhino.Contracts;
using MCP4Rhino.Logic.Grasshopper;

namespace MCP4Rhino.Host;

/// <summary>Human-owned plugin permissions. MCP has no mutation entry point for these settings.</summary>
[ExcludeFromCodeCoverage]
internal sealed class GrasshopperComponentAuthorization : IGrasshopperComponentAuthorization
{
    private const string SettingsKey = "GrasshopperTrustedAssemblies";
    public static GrasshopperComponentAuthorization Instance { get; } = new();
    private ComponentTrust? _trust;
    private ComponentTrust Current => _trust ??= ComponentTrust.Load(MCP4RhinoPlugin.Instance.Settings.GetString(SettingsKey, "[]"));

    public bool IsTrustedAssembly(string assemblyIdentity) => Current.Allows(assemblyIdentity);

    public void ShowPreferences(Control? owner = null)
    {
        var libraries = GrasshopperHost.ComponentLibraries();
        var trust = Current;
        var selected = GrasshopperPluginsDialog.Show(libraries, trust, owner);
        if (selected is null) return;
        var visible = libraries.Where(library => library.AvailableComponents > 0).Select(library => library.AssemblyIdentity);
        var next = trust.ReplaceVisible(visible, selected);
        // Persist first: failed writes must not grant permissions just for the current process.
        MCP4RhinoPlugin.Instance.Settings.SetString(SettingsKey, next.ToJson());
        _trust = next;
    }
}
