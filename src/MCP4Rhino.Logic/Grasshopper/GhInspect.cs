using System.Text.Json;
using System.Text.Json.Nodes;

namespace MCP4Rhino.Logic.Grasshopper;

/// <summary>One document object as the inspection tool sees it. No Grasshopper types, so it is unit-testable.</summary>
public sealed record GhNodeInfo(string Id, string Name, string Nickname, string Type, double X, double Y,
    string[] Groups, string[] Errors, string[] Warnings, int Inputs, int Outputs, JsonObject? Control = null)
{
    public bool HasDiagnostics => Errors.Length > 0 || Warnings.Length > 0;
}

public sealed record GhGroupInfo(string Id, string Name, string[] Members);

public enum GhInspectMode { Overview, Compact, Detail }

/// <summary>Parsed <c>gh_inspect_document</c> arguments.</summary>
public sealed record GhInspectOptions(GhInspectMode Mode, int Offset, int Limit, string[] Nodes, string? Group,
    string? Match, bool HasDiagnostics)
{
    public bool Filtered => Nodes.Length > 0 || Group is not null || Match is not null || HasDiagnostics;

    /// <summary>
    /// Default mode: <c>overview</c> when the call has no paging or filter arguments (cheap first call),
    /// otherwise <c>compact</c>. Full detail is only returned for <c>mode: "detail"</c>.
    /// </summary>
    public static GhInspectOptions Parse(JsonObject args)
    {
        var offsetGiven = args.ContainsKey("offset") || args.ContainsKey("limit");
        var nodes = args["nodes"] is JsonArray ids
            ? ids.Select(n => n?.GetValue<string>() ?? "").Where(s => s.Length > 0).ToArray() : [];
        var group = Clean(args["group"]); var match = Clean(args["match"]);
        var diagnostics = GhJson.Flag(args, "has_diagnostics");
        var filtered = nodes.Length > 0 || group is not null || match is not null || diagnostics;
        var mode = args["mode"]?.GetValue<string>() switch
        {
            null => offsetGiven || filtered ? GhInspectMode.Compact : GhInspectMode.Overview,
            "overview" => GhInspectMode.Overview,
            "compact" => GhInspectMode.Compact,
            "detail" => GhInspectMode.Detail,
            var other => throw new GhException("invalid_arguments", $"Unknown mode {other}. Use overview, compact or detail."),
        };
        return new GhInspectOptions(mode, GhJson.Offset(args), GhJson.PageSize(args), nodes, group, match, diagnostics);
    }

    private static string? Clean(JsonNode? value) => value?.GetValue<string>() is { Length: > 0 } s ? s : null;
}

/// <summary>
/// Pure logic for <c>gh_inspect_document</c>: filtering, overview aggregation, and size budgeting.
/// Every response stays under <see cref="MaxResponseChars"/> characters of JSON.
/// </summary>
public static class GhInspect
{
    /// <summary>Upper bound on the serialized size of one response (characters of JSON).</summary>
    public const int MaxResponseChars = 50_000;
    /// <summary>Strings longer than this are cut in detail mode.</summary>
    public const int MaxStringChars = 300;
    private const int MaxMessageChars = 160;

    public static IReadOnlyList<GhNodeInfo> Filter(IEnumerable<GhNodeInfo> nodes, IReadOnlyList<GhGroupInfo> groups,
        GhInspectOptions options)
    {
        var ids = options.Nodes.Length > 0 ? options.Nodes.Select(i => i.ToLowerInvariant()).ToHashSet() : null;
        HashSet<string>? members = null;
        if (options.Group is { } group)
        {
            var found = groups.Where(g => string.Equals(g.Id, group, StringComparison.OrdinalIgnoreCase) ||
                g.Name.Contains(group, StringComparison.OrdinalIgnoreCase)).ToArray();
            members = found.SelectMany(g => g.Members).Select(m => m.ToLowerInvariant()).ToHashSet();
        }
        return nodes.Where(n =>
            (ids is null || ids.Contains(n.Id.ToLowerInvariant())) &&
            (members is null || members.Contains(n.Id.ToLowerInvariant())) &&
            (options.Match is null || Contains(n.Name, options.Match) || Contains(n.Nickname, options.Match) ||
                Contains(n.Type, options.Match)) &&
            (!options.HasDiagnostics || n.HasDiagnostics)).ToArray();
    }

    private static bool Contains(string text, string part) => text.Contains(part, StringComparison.OrdinalIgnoreCase);

    public static int Size(JsonNode? node) => node?.ToJsonString().Length ?? 0;

    public static JsonObject CompactRow(GhNodeInfo n) => new()
    {
        ["node"] = n.Id, ["name"] = n.Name, ["nickname"] = n.Nickname, ["type"] = n.Type,
        ["position"] = new JsonArray(Math.Round(n.X, 1), Math.Round(n.Y, 1)),
        ["groups"] = new JsonArray(n.Groups.Select(g => (JsonNode)JsonValue.Create(g)!).ToArray()),
        ["errors"] = n.Errors.Length, ["warnings"] = n.Warnings.Length,
        ["inputs"] = n.Inputs, ["outputs"] = n.Outputs,
    };

    /// <summary>Overview: counts, groups, likely user inputs, and nodes with diagnostics. No per-node port detail.</summary>
    public static JsonObject Overview(JsonNode session, IReadOnlyList<GhNodeInfo> nodes, IReadOnlyList<GhGroupInfo> groups)
    {
        var inputs = nodes.Where(n => n.Control is not null).ToArray();
        var issues = nodes.Where(n => n.HasDiagnostics).ToArray();
        for (var cap = 100; ; cap /= 2)
        {
            var truncated = false;
            JsonArray Take<T>(IReadOnlyList<T> items, Func<T, JsonNode> map)
            {
                if (items.Count > cap) truncated = true;
                return new JsonArray(items.Take(cap).Select(map).ToArray());
            }
            JsonObject Counts(IEnumerable<string> keys)
            {
                var all = keys.GroupBy(k => k).OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal).ToArray();
                if (all.Length > cap) truncated = true;
                var result = new JsonObject();
                foreach (var g in all.Take(cap)) result[g.Key] = g.Count();
                return result;
            }
            var result = new JsonObject
            {
                ["session"] = session.DeepClone(), ["mode"] = "overview", ["total"] = nodes.Count,
                ["counts_by_type"] = Counts(nodes.Select(n => n.Type)),
                ["counts_by_name"] = Counts(nodes.Select(n => n.Name)),
                ["groups_total"] = groups.Count,
                ["groups"] = Take(groups, g => new JsonObject { ["id"] = g.Id, ["name"] = g.Name, ["members"] = g.Members.Length }),
                ["inputs_total"] = inputs.Length,
                ["inputs"] = Take(inputs, n => new JsonObject { ["node"] = n.Id, ["name"] = n.Name, ["nickname"] = n.Nickname,
                    ["control"] = n.Control!.DeepClone() }),
                ["diagnostic_nodes_total"] = issues.Length,
                ["diagnostic_nodes"] = Take(issues, n => new JsonObject { ["node"] = n.Id, ["name"] = n.Name,
                    ["nickname"] = n.Nickname, ["errors"] = Messages(n.Errors), ["warnings"] = Messages(n.Warnings) }),
            };
            if (Size(result) + 600 <= MaxResponseChars || cap <= 1)
            {
                result["truncated"] = truncated;
                result["next"] = new JsonObject
                {
                    ["hint"] = "Page nodes with mode compact, or pass nodes, group, match or has_diagnostics to filter. Use mode detail for the nodes you will patch.",
                    ["arguments"] = new JsonObject { ["mode"] = "compact", ["offset"] = 0 },
                };
                return result;
            }
        }
    }

    private static JsonArray Messages(string[] messages) =>
        new(messages.Take(3).Select(m => (JsonNode)JsonValue.Create(Cut(m, MaxMessageChars))!).ToArray());

    public static string Cut(string text, int max) =>
        text.Length <= max ? text : text[..max] + $"…[truncated {text.Length - max} chars]";

    /// <summary>
    /// A page of compact or detail rows. Rows are added until <c>limit</c> or the size budget is reached.
    /// <c>next_offset</c> is set when more matching nodes remain; <c>truncated</c> is true when the budget
    /// (not the limit) ended the page. <c>next</c> holds the arguments for the following call.
    /// </summary>
    public static JsonObject Page(JsonNode session, IReadOnlyList<GhNodeInfo> matches, int totalNodes, GhInspectOptions options,
        Func<GhNodeInfo, JsonNode> describe)
    {
        var detail = options.Mode == GhInspectMode.Detail;
        var rows = new JsonArray();
        var used = 1500 + Size(session);
        var index = options.Offset;
        var cutByBudget = false;
        while (index < matches.Count && rows.Count < options.Limit)
        {
            var node = matches[index];
            JsonNode row = detail ? FitDetail(node, describe(node), MaxResponseChars - used) : CompactRow(node);
            var size = Size(row) + 1;
            if (used + size > MaxResponseChars && rows.Count > 0) { cutByBudget = true; break; }
            rows.Add(row); used += size; index++;
        }
        var result = new JsonObject
        {
            ["session"] = session.DeepClone(), ["mode"] = detail ? "detail" : "compact", ["total"] = totalNodes,
            ["matched"] = matches.Count, ["offset"] = options.Offset, ["nodes"] = rows,
            ["truncated"] = cutByBudget,
        };
        if (index < matches.Count)
        {
            result["next_offset"] = index;
            result["next"] = NextArguments(options, index);
        }
        else result["next_offset"] = null;
        return result;
    }

    private static JsonObject NextArguments(GhInspectOptions o, int offset)
    {
        var next = new JsonObject { ["mode"] = o.Mode.ToString().ToLowerInvariant(), ["offset"] = offset, ["limit"] = o.Limit };
        if (o.Nodes.Length > 0) next["nodes"] = new JsonArray(o.Nodes.Select(n => (JsonNode)JsonValue.Create(n)!).ToArray());
        if (o.Group is not null) next["group"] = o.Group;
        if (o.Match is not null) next["match"] = o.Match;
        if (o.HasDiagnostics) next["has_diagnostics"] = true;
        return next;
    }

    /// <summary>Caps long strings, and falls back to a marked stub when one node is still over its share.</summary>
    private static JsonNode FitDetail(GhNodeInfo node, JsonNode full, int room)
    {
        var capped = CapStrings(TrimLists(full));
        if (Size(capped) <= Math.Max(room, MaxResponseChars / 2)) return capped;
        var stub = CompactRow(node);
        stub["detail_truncated"] = true;
        stub["note"] = "Detail for this node is too large. Fetch it alone with nodes:[id], mode detail.";
        return stub;
    }

    public const int MaxPorts = 50;
    public const int MaxSamples = 5;
    public const int MaxSources = 20;

    /// <summary>
    /// Caps long lists in a full node description: ports per side, persistent-value samples and wire sources.
    /// Each cut adds a marker with the real count, for example <c>inputs_total</c> or <c>sample_truncated</c>.
    /// </summary>
    public static JsonNode TrimLists(JsonNode node)
    {
        if (node is not JsonObject o) return node;
        foreach (var side in new[] { "inputs", "outputs" })
        {
            if (o[side] is not JsonArray ports) continue;
            if (ports.Count > MaxPorts)
            {
                o[side + "_total"] = ports.Count;
                while (ports.Count > MaxPorts) ports.RemoveAt(ports.Count - 1);
            }
            foreach (var port in ports.OfType<JsonObject>())
            {
                if (port["sources"] is JsonArray sources && sources.Count > MaxSources)
                {
                    port["sources_total"] = sources.Count;
                    while (sources.Count > MaxSources) sources.RemoveAt(sources.Count - 1);
                }
                if (port["persistent"] is JsonObject persistent && persistent["sample"] is JsonArray sample && sample.Count > MaxSamples)
                {
                    persistent["sample_truncated"] = true;
                    while (sample.Count > MaxSamples) sample.RemoveAt(sample.Count - 1);
                }
            }
        }
        return node;
    }

    /// <summary>Cuts strings longer than <see cref="MaxStringChars"/> with a visible marker.</summary>
    public static JsonNode CapStrings(JsonNode node)
    {
        switch (node)
        {
            case JsonObject o:
                foreach (var (key, child) in o.ToArray())
                    if (child is not null && !ReferenceEquals(CapStrings(child), child)) o[key] = CapStrings(child);
                return o;
            case JsonArray a:
                for (var i = 0; i < a.Count; i++)
                    if (a[i] is { } child && !ReferenceEquals(CapStrings(child), child)) a[i] = CapStrings(child);
                return a;
            case JsonValue value when value.TryGetValue<string>(out var s) && s.Length > MaxStringChars:
                return JsonValue.Create(Cut(s, MaxStringChars))!;
            default: return node;
        }
    }

    /// <summary>Converts any serializable object to a JsonNode for capping.</summary>
    public static JsonNode ToNode(object value) => JsonSerializer.SerializeToNode(value) ?? new JsonObject();
}
