namespace MCP4Rhino.Logic.Grasshopper;

public sealed record ComponentIdentity(string AssemblyName, string AssemblyIdentity, string TypeName,
    bool Obsolete = false, bool Compiled = true)
{
    public bool BuiltIn { get; init; }
}

public sealed record ComponentPermission(string SupportLevel, bool Authorized, string? Reason)
{
    public void Require()
    {
        if (!Authorized) throw new GhException(SupportLevel == "restricted" ? "restricted_component" :
            "component_authorization_required", Reason ?? "Local component authorization is required.", "workflow");
    }
}

/// <summary>Local execution policy, independent of catalog discovery. Assembly identities are not signatures.</summary>
public sealed class ComponentPolicy
{
    public const string PreferencesCommand = "MCP4RhinoGrasshopper";
    private readonly Func<string, bool> _isTrusted;
    private static readonly string[] Risks = ["ScriptComponents", "Scripting", ".Script", "_Script", "ScriptComponent", "ScriptBase",
        "Python", "CSharp", "Cluster", "Timer", "ReadFile", "WriteFile", ".File", "_File",
        "Stream", "Network", "Socket", "Http", "Hops", "Compute", "Galapagos"];

    public ComponentPolicy(Func<string, bool>? isTrusted = null) => _isTrusted = isTrusted ?? (_ => false);

    public ComponentPermission Classify(ComponentIdentity identity)
    {
        if (!identity.Compiled || string.IsNullOrWhiteSpace(identity.TypeName) ||
            string.IsNullOrWhiteSpace(identity.AssemblyIdentity)) return Restricted("Missing compiled type metadata; user objects are restricted.");
        if (identity.Obsolete) return Restricted("Obsolete components are restricted.");
        if (ComponentSupport.Allows(identity.AssemblyName, identity.TypeName)) return new("verified", true, null);
        if (Risks.Any(risk => (identity.AssemblyName + "." + identity.TypeName).Contains(risk, StringComparison.OrdinalIgnoreCase)))
            return Restricted("Known scripting, embedded-graph, scheduling or external-I/O type is restricted.");
        var authorized = identity.BuiltIn || _isTrusted(identity.AssemblyIdentity);
        return new("available", authorized, authorized ? null :
            $"In Rhino, open Tools > MCP4Rhino > Grasshopper plugins… ({PreferencesCommand}) and enable this plugin. Changes apply immediately.");
    }

    private static ComponentPermission Restricted(string reason) => new("restricted", false, reason);
}
