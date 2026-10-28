using System.Text.Json;

namespace MCP4Rhino.Logic.Grasshopper;

public sealed record ComponentIdentity(string AssemblyName, string AssemblyIdentity, string TypeName,
    bool Obsolete = false, bool Compiled = true);

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
    public const string EnvironmentVariable = "MCP4RHINO_GH_TRUSTED_ASSEMBLIES";
    private readonly HashSet<string> _trusted = new(StringComparer.Ordinal);
    public bool InvalidConfiguration { get; }
    private static readonly string[] Risks = ["ScriptComponents", "Scripting", ".Script", "_Script", "ScriptComponent", "ScriptBase",
        "Python", "CSharp", "Cluster", "Timer", "ReadFile", "WriteFile", ".File", "_File",
        "Stream", "Network", "Socket", "Http", "Hops", "Compute", "Galapagos"];

    public ComponentPolicy(string? configuration = null)
    {
        if (string.IsNullOrWhiteSpace(configuration)) return;
        try
        {
            var identities = JsonSerializer.Deserialize<string[]>(configuration);
            if (identities is null || identities.Any(string.IsNullOrWhiteSpace)) { InvalidConfiguration = true; return; }
            _trusted.UnionWith(identities);
        }
        catch (JsonException) { InvalidConfiguration = true; }
    }

    public ComponentPermission Classify(ComponentIdentity identity)
    {
        if (!identity.Compiled || string.IsNullOrWhiteSpace(identity.TypeName) ||
            string.IsNullOrWhiteSpace(identity.AssemblyIdentity)) return Restricted("Missing compiled type metadata; user objects are restricted.");
        if (identity.Obsolete) return Restricted("Obsolete components are restricted.");
        if (ComponentSupport.Allows(identity.AssemblyName, identity.TypeName)) return new("verified", true, null);
        if (Risks.Any(risk => (identity.AssemblyName + "." + identity.TypeName).Contains(risk, StringComparison.OrdinalIgnoreCase)))
            return Restricted("Known scripting, embedded-graph, scheduling or external-I/O type is restricted.");
        var authorized = _trusted.Contains(identity.AssemblyIdentity);
        return new("available", authorized, authorized ? null :
            $"Authorize this exact assembly identity locally in {EnvironmentVariable} before restarting Rhino. Discovery is not execution permission.");
    }

    private static ComponentPermission Restricted(string reason) => new("restricted", false, reason);
}
