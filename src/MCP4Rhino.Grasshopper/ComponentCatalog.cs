using System.Text.Json.Nodes;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using MCP4Rhino.Logic.Grasshopper;
using GH = Grasshopper.Instances;

namespace MCP4Rhino.Grasshopper;

internal static class ComponentCatalog
{
    private static readonly Dictionary<Guid, JsonObject> Descriptions = new();
    private static readonly ComponentPolicy Policy = new(Environment.GetEnvironmentVariable(ComponentPolicy.EnvironmentVariable));
    public static bool Supported(IGH_DocumentObject node) => ComponentSupport.Allows(
        node.GetType().Assembly.GetName().Name ?? "", node.GetType().FullName ?? "", node.Obsolete);

    public static bool Supported(IGH_ObjectProxy proxy) => proxy.Type is { } type && ComponentSupport.Allows(
        type.Assembly.GetName().Name ?? "", type.FullName ?? "", proxy.Obsolete);

    private static ComponentIdentity Identity(Type? type, bool obsolete) => new(type?.Assembly.GetName().Name ?? "",
        type?.Assembly.FullName ?? "", type?.FullName ?? "", obsolete, type is not null);

    public static ComponentPermission Permission(IGH_DocumentObject node) => Permission(node.GetType(), node.Obsolete);
    private static ComponentPermission Permission(IGH_ObjectProxy proxy) => Permission(proxy.Type, proxy.Obsolete || !proxy.SDKCompliant);

    private static ComponentPermission Permission(Type? type, bool obsolete)
    {
        var permission = Policy.Classify(Identity(type, obsolete));
        // Known script/cluster base classes remain restricted even when a plugin renames its subclass.
        for (var parent = type?.BaseType; parent is not null; parent = parent.BaseType)
        {
            var inherited = Policy.Classify(Identity(parent, false));
            if (inherited.SupportLevel == "restricted") return inherited;
        }
        return permission;
    }

    public static object Search(JsonObject args)
    {
        var query = args["query"]?.GetValue<string>() ?? "";
        var matches = GH.ComponentServer.ObjectProxies.Where(p =>
            (p.Desc.Name + " " + p.Desc.Description + " " + p.Desc.Category + " " + p.Type?.FullName).Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(Supported).ThenBy(p => p.Desc.Name).ToArray();
        var offset = GhJson.Offset(args);
        var page = matches.Skip(offset).Take(GhJson.PageSize(args)).Select(DescribeProxy).ToArray();
        return new { total = matches.Length, offset, next_offset = offset + page.Length < matches.Length ? offset + page.Length : (int?)null, components = page };
    }

    private static object DescribeProxy(IGH_ObjectProxy proxy) => new
    {
        component_id = proxy.Guid, name = proxy.Desc.Name, description = proxy.Desc.Description,
        category = proxy.Desc.Category, subcategory = proxy.Desc.SubCategory,
        library = GH.ComponentServer.FindAssembly(proxy.LibraryGuid)?.Name,
        type = proxy.Type?.FullName, supported = Supported(proxy), obsolete = proxy.Obsolete,
        assembly_identity = proxy.Type?.Assembly.FullName, library_id = proxy.LibraryGuid,
        support_level = Permission(proxy).SupportLevel, authorized = Permission(proxy).Authorized,
        permission_required = Permission(proxy).SupportLevel == "available" && !Permission(proxy).Authorized,
        restriction_reason = Permission(proxy).Reason, authorization_configuration_invalid = Policy.InvalidConfiguration,
    };

    public static object Describe(JsonObject args) => GhJson.Array(args["component_ids"]).Take(50)
        .Select(id => DescribeType(Guid.Parse(id!.GetValue<string>()))).ToArray();

    private static object DescribeType(Guid id)
    {
        var proxy = GH.ComponentServer.EmitObjectProxy(id) ?? throw new GhException("component_not_found", $"Unknown component type {id}.");
        // Constructors execute plugin code. Metadata discovery must not grant permission to run them.
        if (!Permission(proxy).Authorized) return new { descriptor = DescribeProxy(proxy), inputs = Array.Empty<object>(), outputs = Array.Empty<object>(),
            ports_status = Permission(proxy).SupportLevel == "restricted" ? "restricted" : "authorization_required" };
        var node = Create(id);
        try
        {
            var description = new { descriptor = DescribeProxy(proxy), inputs = Ports(node, true), outputs = Ports(node, false), settings = Settings(node) };
            Descriptions[id] = System.Text.Json.JsonSerializer.SerializeToNode(description)!.AsObject();
            return description;
        }
        finally { (node as IDisposable)?.Dispose(); }
    }

    public static object ReadDescriptor(Guid id)
    {
        var proxy = GH.ComponentServer.ObjectProxies.FirstOrDefault(p => p.Guid == id) ??
            throw new MCP4Rhino.Logic.Protocol.McpRpcException(-32002, "Installed component not found");
        var authorized = Permission(proxy).Authorized;
        return new { descriptor = DescribeProxy(proxy), ports_status = Descriptions.ContainsKey(id) ? "cached" :
            authorized ? "not_described" : Permission(proxy).SupportLevel == "restricted" ? "restricted" : "authorization_required",
            contract = Descriptions.GetValueOrDefault(id)?.DeepClone(),
            next_tool = Descriptions.ContainsKey(id) || !authorized ? null : "gh_describe_components" };
    }

    public static IGH_DocumentObject Create(Guid id)
    {
        var proxy = GH.ComponentServer.EmitObjectProxy(id) ?? throw new GhException("component_not_found", $"Unknown type {id}.");
        Permission(proxy).Require();
        var node = GH.ComponentServer.EmitObject(id) ?? throw new GhException("component_not_found", $"Cannot create {id}.");
        try
        {
            Permission(node).Require();
            node.CreateAttributes();
            return node;
        }
        catch { (node as IDisposable)?.Dispose(); throw; }
    }

    public static IEnumerable<IGH_Param> Parameters(IGH_DocumentObject node, bool input) => node switch
    {
        GH_NumberSlider when input => Array.Empty<IGH_Param>(),
        IGH_Component component => input ? component.Params.Input : component.Params.Output,
        IGH_Param parameter => new[] { parameter },
        _ => Array.Empty<IGH_Param>(),
    };

    public static object[] Ports(IGH_DocumentObject node, bool input, bool resource = false) => Parameters(node, input).Take(resource ? 200 : int.MaxValue)
        .Select((p, i) => (object)new
        {
            port = (input ? "in:" : "out:") + i, parameter_id = p.InstanceGuid, name = p.Name, nickname = p.NickName,
            description = p.Description, type = p.TypeName, access = p.Access.ToString(), optional = p.Optional,
            data_type = p.Type.AssemblyQualifiedName, parameter_type = p.GetType().AssemblyQualifiedName,
            mapping = p.DataMapping.ToString(), simplify = p.Simplify, reverse = p.Reverse,
            sources = p.Sources.Take(resource ? 200 : int.MaxValue).Select(Source).ToArray(),
            persistent = ValueCodec.PersistentSummary(p, !resource),
        }).ToArray();

    private static object Source(IGH_Param parameter)
    {
        var owner = parameter.Attributes.GetTopLevel.DocObject;
        return new { node = owner.InstanceGuid, parameter_id = parameter.InstanceGuid,
            port = "out:" + Array.IndexOf(Parameters(owner, false).ToArray(), parameter) };
    }

    public static object? Settings(IGH_DocumentObject node) => node is GH_NumberSlider slider ? new
    {
        value = slider.CurrentValue, raw_value = slider.Slider.Value, minimum = slider.Slider.Minimum, maximum = slider.Slider.Maximum,
        accuracy = slider.Slider.Type.ToString(),
        decimal_places = slider.Slider.DecimalPlaces,
    } : null;

    public static IGH_Param Port(IGH_DocumentObject node, string token, bool input)
    {
        var prefix = input ? "in:" : "out:";
        if (!token.StartsWith(prefix) || !int.TryParse(token[prefix.Length..], out var index) || index < 0)
            throw new GhException("invalid_port", $"Expected a discovered {prefix} port.");
        return Parameters(node, input).ElementAtOrDefault(index) ?? throw new GhException("invalid_port", $"Port {token} does not exist.");
    }

    public static void RequireAuthorized(GH_Document document)
    {
        foreach (var node in document.Objects)
        {
            var permission = Permission(node);
            var code = permission.SupportLevel == "restricted" ? "restricted_component" : "component_authorization_required";
            if (!permission.Authorized) throw new GhException(code,
                $"{code}: {node.Name} ({node.ComponentGuid}; {node.GetType().Assembly.FullName}): {permission.Reason} " +
                "Inspection and slider-only patches with solve:false remain available.", "workflow");
        }
    }
}
